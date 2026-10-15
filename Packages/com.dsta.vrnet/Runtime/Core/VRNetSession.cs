using System;
using System.Collections;
using System.Net;
using DSTA.VRNet.Avatar;
using DSTA.VRNet.Discovery;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace DSTA.VRNet.Core
{
    /// <summary>
    /// Facade for the whole library. Put this next to NGO's NetworkManager, assign a
    /// VRNetConfig, then call StartServer() (tablet) or StartClientAuto() (headset).
    /// Games register their XR rig with SetLocalRig().
    /// </summary>
    public class VRNetSession : MonoBehaviour
    {
        public static VRNetSession Instance { get; private set; }

        [SerializeField] VRNetConfig _config;

        VRNetConfig _runtimeConfig;

        /// <summary>Session-owned settings. Editing these never changes the assigned asset.</summary>
        public VRNetConfig Config
        {
            get
            {
                if (_runtimeConfig == null)
                {
                    _runtimeConfig = _config != null ? Instantiate(_config) : ScriptableObject.CreateInstance<VRNetConfig>();
                    _runtimeConfig.hideFlags = HideFlags.DontSave;
                }
                return _runtimeConfig;
            }
        }

        public VRNetPlayerRegistry Registry { get; } = new();
        public string PlayerName { get; set; }
        public bool IsServerRunning => _networkManager != null && _networkManager.IsServer && !_networkManager.ShutdownInProgress;
        public bool IsClientConnected => _networkManager != null && _networkManager.IsConnectedClient && !_networkManager.ShutdownInProgress;

        /// <summary>
        /// Optional override for the persistent player identity. Leave null on devices
        /// (PlayerIdentity supplies a per-device GUID). Needed when several instances
        /// share one machine and PlayerPrefs store, e.g. Multiplayer Play Mode virtual
        /// players, which would otherwise all present the same identity.
        /// </summary>
        public string PlayerGuidOverride { get; set; }

        public event Action<string> OnStatusChanged;
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action OnStopped;
        public event Action OnDiscoveryTimeout;
        public event Action OnPlayersChanged;

        DiscoveryBroadcaster _broadcaster;
        DiscoveryListener _listener;
        ReconnectController _reconnect;
        NetworkManager _networkManager;
        Coroutine _clientRoutine;
        IPAddress _foundAddress;
        DiscoveryMessage _foundMessage;
        bool _useDiscovery = true;
        string _clientAddress;
        int _clientPort = -1;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            PlayerName = SystemInfo.deviceName;
            _broadcaster = gameObject.AddComponent<DiscoveryBroadcaster>();
            _listener = gameObject.AddComponent<DiscoveryListener>();
            _listener.OnServerFound += HandleServerFound;
            _reconnect = gameObject.AddComponent<ReconnectController>();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                CleanupSession();
                if (_listener != null) _listener.OnServerFound -= HandleServerFound;
                LocalRig.Clear();
                Instance = null;
            }
            if (_runtimeConfig != null)
            {
                if (Application.isPlaying) Destroy(_runtimeConfig);
                else DestroyImmediate(_runtimeConfig);
                _runtimeConfig = null;
            }
        }

        void HandleServerFound(IPAddress address, DiscoveryMessage message)
        {
            _foundAddress = address;
            _foundMessage = message;
        }

        public static void SetLocalRig(Transform root, Transform head, Transform leftHand, Transform rightHand)
            => LocalRig.Set(root, head, leftHand, rightHand);

        // ---------- Server ----------

        public void StartServer()
        {
            var nm = NetworkManager.Singleton;
            if (!CanStart(nm)) return;
            CancelClientRoutine();
            HookCallbacks(nm);
            if (!ApplyTransportSettings(nm, listenAll: true))
            {
                UnhookCallbacks();
                return;
            }
            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback = Approve;
            if (!nm.StartServer())
            {
                UnhookCallbacks();
                Status($"Failed to start server on port {Config.serverPort}");
                return;
            }
            _broadcaster.StartBroadcasting(
                DiscoveryMessage.Create(Config.teamId, Config.serverName, Config.serverPort, Config.appVersion),
                Config.discoveryPort, Config.discoveryIntervalSeconds);
            Status($"Server '{Config.serverName}' up for {Config.teamId} on port {Config.serverPort}");
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var result = ConnectionValidator.Validate(request.Payload, Config.teamId, Config.appVersion);
            response.Approved = result.Approved;
            response.Reason = result.Reason;
            response.CreatePlayerObject = result.Approved;
            if (!result.Approved)
            {
                Status($"Rejected connection: {result.Reason}");
                return;
            }

            // A returning PlayerGuid means a stale avatar may still be frozen in-world.
            if (Registry.TryGetByGuid(result.Payload.playerGuid, out var existing))
            {
                var stale = VRNetAvatar.FindByOwner(existing.ClientId);
                if (stale != null && stale.NetworkObject.IsSpawned)
                    stale.NetworkObject.Despawn(true);
            }

            var kind = Registry.RecordApproved(request.ClientNetworkId, result.Payload);
            Status(kind == JoinKind.Rejoin
                ? $"{result.Payload.playerName} rejoined"
                : $"{result.Payload.playerName} joined");
        }

        void OnServerClientDisconnected(ulong clientId)
        {
            var record = Registry.RecordDisconnected(clientId);
            OnPlayersChanged?.Invoke();
            if (record == null) return;
            Status($"{record.PlayerName} lost connection (grace {Config.avatarDespawnGraceSeconds}s)");
            StartCoroutine(DespawnAfterGrace(clientId, record.PlayerGuid));
        }

        IEnumerator DespawnAfterGrace(ulong clientId, string playerGuid)
        {
            yield return new WaitForSeconds(Config.avatarDespawnGraceSeconds);
            if (Registry.TryGetByGuid(playerGuid, out var record) && record.Connected)
                yield break; // rejoined during grace; stale avatar was already despawned in Approve

            var avatar = VRNetAvatar.FindByOwner(clientId);
            if (avatar != null && avatar.NetworkObject.IsSpawned)
                avatar.NetworkObject.Despawn(true);
        }

        // ---------- Client ----------

        public void StartClientAuto()
        {
            _useDiscovery = true;
            CancelClientRoutine();
            _clientRoutine = StartCoroutine(ClientAutoRoutine());
        }

        public void StartClient(string address, int port = -1)
        {
            _useDiscovery = false;
            _clientAddress = address;
            _clientPort = port > 0 ? port : Config.serverPort;
            CancelClientRoutine();
            _clientRoutine = StartCoroutine(ClientConnectRoutine(address, port));
        }

        internal void RetryClientConnection()
        {
            if (_useDiscovery) StartClientAuto();
            else StartClient(_clientAddress, _clientPort);
        }

        void CancelClientRoutine()
        {
            if (_clientRoutine != null) StopCoroutine(_clientRoutine);
            _clientRoutine = null;
            if (_listener != null) _listener.StopListening();
        }

        IEnumerator ClientAutoRoutine()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Status("No NetworkManager is available");
                yield break;
            }
            UnhookCallbacks();
            if (nm.IsClient || nm.IsServer) nm.Shutdown();
            while (nm.ShutdownInProgress) yield return null;

            Status($"Searching for {Config.teamId} server...");
            _foundAddress = null;
            _listener.StartListening(Config.discoveryPort, Config.teamId);

            float deadline = Time.realtimeSinceStartup + Config.discoveryTimeoutSeconds;
            while (_foundAddress == null && Time.realtimeSinceStartup < deadline)
                yield return null;

            _listener.StopListening();

            if (_foundAddress == null)
            {
                _clientRoutine = null;
                Status("No server found. Check Wi-Fi/team ID, retry, or enter IP manually.");
                OnDiscoveryTimeout?.Invoke();
                yield break;
            }
            _clientRoutine = null;
            if (!ConnectNow(_foundAddress.ToString(), _foundMessage.port))
                OnDiscoveryTimeout?.Invoke();
        }

        IEnumerator ClientConnectRoutine(string address, int port)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Status("No NetworkManager is available");
                yield break;
            }
            UnhookCallbacks();
            if (nm.IsClient || nm.IsServer) nm.Shutdown();
            while (nm.ShutdownInProgress) yield return null;
            ConnectNow(address, port);
            _clientRoutine = null;
        }

        bool ConnectNow(string address, int port)
        {
            var nm = NetworkManager.Singleton;
            if (!CanStart(nm)) return false;
            HookCallbacks(nm);
            if (!ApplyTransportSettings(nm, listenAll: false,
                address: address, port: port > 0 ? port : Config.serverPort))
            {
                UnhookCallbacks();
                return false;
            }
            // Must match the server: NGO clients only serialize ConnectionData into the
            // connection request when their own NetworkConfig.ConnectionApproval is true.
            // Without this the approval-enabled server rejects every join with
            // "Incomplete connection request message given config".
            nm.NetworkConfig.ConnectionApproval = true;
            nm.NetworkConfig.ConnectionData = new ConnectionPayload
            {
                playerGuid = string.IsNullOrEmpty(PlayerGuidOverride) ? PlayerIdentity.GetOrCreate() : PlayerGuidOverride,
                playerName = PlayerName,
                teamId = Config.teamId,
                appVersion = Config.appVersion
            }.ToBytes();
            if (!nm.StartClient())
            {
                UnhookCallbacks();
                Status("Failed to start client connection");
                return false;
            }
            Status($"Connecting to {address}...");
            return true;
        }

        public void StopSession()
        {
            CleanupSession();
            OnStopped?.Invoke();
            OnPlayersChanged?.Invoke();
            Status("Session stopped");
        }

        void CleanupSession()
        {
            if (_reconnect != null)
            {
                _reconnect.Enabled = false;
                _reconnect.CancelRetry();
            }
            CancelClientRoutine();
            StopAllCoroutines();
            if (_broadcaster != null) _broadcaster.StopBroadcasting();
            _foundAddress = null;
            var nm = _networkManager;
            UnhookCallbacks();
            Registry.Clear();
            if (nm != null) nm.Shutdown();
        }

        // ---------- Shared ----------

        bool CanStart(NetworkManager nm)
        {
            if (nm == null)
            {
                Status("No NetworkManager is available");
                return false;
            }
            if (nm.ShutdownInProgress || nm.IsClient || nm.IsServer)
            {
                Status("Wait for the current session to stop before starting another");
                return false;
            }
            return true;
        }

        bool ApplyTransportSettings(NetworkManager nm, bool listenAll, string address = null, int port = 0)
        {
            var transport = nm.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[VRNet] No UnityTransport on the NetworkManager. Add a Unity Transport component next to NetworkManager.");
                return false;
            }
            transport.DisconnectTimeoutMS = Config.disconnectTimeoutMs;
            transport.ConnectTimeoutMS = Config.connectTimeoutMs;
            transport.MaxConnectAttempts = Config.maxConnectAttempts;
            if (listenAll)
                transport.SetConnectionData("0.0.0.0", (ushort)Config.serverPort, "0.0.0.0");
            else
                transport.SetConnectionData(address, (ushort)port);
            return true;
        }

        void HookCallbacks(NetworkManager nm)
        {
            if (_networkManager == nm) return;
            UnhookCallbacks();
            _networkManager = nm;
            nm.OnClientConnectedCallback += HandleClientConnected;
            nm.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        void UnhookCallbacks()
        {
            var nm = _networkManager;
            _networkManager = null;
            if (nm == null) return;
            nm.OnClientConnectedCallback -= HandleClientConnected;
            nm.OnClientDisconnectCallback -= HandleClientDisconnected;
            if (nm.ConnectionApprovalCallback == Approve)
                nm.ConnectionApprovalCallback = null;
        }

        void HandleClientConnected(ulong clientId)
        {
            var nm = _networkManager;
            if (nm == null) return;
            if (nm.IsServer)
            {
                OnPlayersChanged?.Invoke();
            }
            else if (clientId == nm.LocalClientId)
            {
                Status("Connected");
                OnConnected?.Invoke();
            }
        }

        void HandleClientDisconnected(ulong clientId)
        {
            var nm = _networkManager;
            if (nm == null) return;
            if (nm.IsServer)
            {
                OnServerClientDisconnected(clientId);
            }
            else if (clientId == nm.LocalClientId)
            {
                var reason = string.IsNullOrEmpty(nm.DisconnectReason) ? "connection lost" : nm.DisconnectReason;
                Status($"Disconnected: {reason}");
                OnDisconnected?.Invoke();
            }
        }

        void Status(string message)
        {
            Debug.Log($"[VRNet] {message}");
            OnStatusChanged?.Invoke(message);
        }
    }
}
