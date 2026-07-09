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

        public VRNetConfig Config => _config;
        public VRNetPlayerRegistry Registry { get; } = new();
        public string PlayerName { get; set; }

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
        public event Action OnDiscoveryTimeout;
        public event Action OnPlayersChanged;

        DiscoveryBroadcaster _broadcaster;
        DiscoveryListener _listener;
        ReconnectController _reconnect;
        bool _callbacksHooked;
        Coroutine _clientRoutine;
        IPAddress _foundAddress;
        DiscoveryMessage _foundMessage;

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
            HookCallbacks(nm);
            ApplyTransportSettings(nm, listenAll: true);
            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback = Approve;
            if (!nm.StartServer())
            {
                Status($"Failed to start server on port {_config.serverPort}");
                return;
            }
            _broadcaster.StartBroadcasting(
                DiscoveryMessage.Create(_config.teamId, _config.serverName, _config.serverPort, _config.appVersion),
                _config.discoveryPort, _config.discoveryIntervalSeconds);
            Status($"Server '{_config.serverName}' up for {_config.teamId} on port {_config.serverPort}");
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var result = ConnectionValidator.Validate(request.Payload, _config.teamId, _config.appVersion);
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
            Status($"{record.PlayerName} lost connection (grace {_config.avatarDespawnGraceSeconds}s)");
            StartCoroutine(DespawnAfterGrace(clientId, record.PlayerGuid));
        }

        IEnumerator DespawnAfterGrace(ulong clientId, string playerGuid)
        {
            yield return new WaitForSeconds(_config.avatarDespawnGraceSeconds);
            if (Registry.TryGetByGuid(playerGuid, out var record) && record.Connected)
                yield break; // rejoined during grace; stale avatar was already despawned in Approve

            var avatar = VRNetAvatar.FindByOwner(clientId);
            if (avatar != null && avatar.NetworkObject.IsSpawned)
                avatar.NetworkObject.Despawn(true);
        }

        // ---------- Client ----------

        public void StartClientAuto()
        {
            CancelClientRoutine();
            _clientRoutine = StartCoroutine(ClientAutoRoutine());
        }

        public void StartClient(string address, int port = -1)
        {
            CancelClientRoutine();
            _clientRoutine = StartCoroutine(ClientConnectRoutine(address, port));
        }

        void CancelClientRoutine()
        {
            if (_clientRoutine != null) StopCoroutine(_clientRoutine);
            _clientRoutine = null;
            _listener.StopListening();
        }

        IEnumerator ClientAutoRoutine()
        {
            var nm = NetworkManager.Singleton;
            if (nm.IsClient || nm.IsServer) nm.Shutdown();
            while (nm.ShutdownInProgress) yield return null;

            Status($"Searching for {_config.teamId} server...");
            _foundAddress = null;
            _listener.StartListening(_config.discoveryPort, _config.teamId);

            float deadline = Time.realtimeSinceStartup + _config.discoveryTimeoutSeconds;
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
            if (nm.IsClient || nm.IsServer) nm.Shutdown();
            while (nm.ShutdownInProgress) yield return null;
            ConnectNow(address, port);
            _clientRoutine = null;
        }

        bool ConnectNow(string address, int port)
        {
            var nm = NetworkManager.Singleton;
            HookCallbacks(nm);
            ApplyTransportSettings(nm, listenAll: false,
                address: address, port: port > 0 ? port : _config.serverPort);
            // Must match the server: NGO clients only serialize ConnectionData into the
            // connection request when their own NetworkConfig.ConnectionApproval is true.
            // Without this the approval-enabled server rejects every join with
            // "Incomplete connection request message given config".
            nm.NetworkConfig.ConnectionApproval = true;
            nm.NetworkConfig.ConnectionData = new ConnectionPayload
            {
                playerGuid = PlayerGuidOverride ?? PlayerIdentity.GetOrCreate(),
                playerName = PlayerName,
                teamId = _config.teamId,
                appVersion = _config.appVersion
            }.ToBytes();
            if (!nm.StartClient())
            {
                Status("Failed to start client connection");
                return false;
            }
            Status($"Connecting to {address}...");
            return true;
        }

        public void StopSession()
        {
            _reconnect.Enabled = false;
            CancelClientRoutine();
            StopAllCoroutines();
            _broadcaster.StopBroadcasting();
            Registry.Clear();
            OnPlayersChanged?.Invoke();
            NetworkManager.Singleton.Shutdown();
            Status("Session stopped");
        }

        // ---------- Shared ----------

        void ApplyTransportSettings(NetworkManager nm, bool listenAll, string address = null, int port = 0)
        {
            var transport = nm.GetComponent<UnityTransport>();
            transport.DisconnectTimeoutMS = _config.disconnectTimeoutMs;
            transport.ConnectTimeoutMS = _config.connectTimeoutMs;
            transport.MaxConnectAttempts = _config.maxConnectAttempts;
            if (listenAll)
                transport.SetConnectionData("0.0.0.0", (ushort)_config.serverPort, "0.0.0.0");
            else
                transport.SetConnectionData(address, (ushort)port);
        }

        void HookCallbacks(NetworkManager nm)
        {
            if (_callbacksHooked) return;
            _callbacksHooked = true;

            nm.OnClientConnectedCallback += clientId =>
            {
                if (nm.IsServer)
                {
                    OnPlayersChanged?.Invoke();
                }
                else if (clientId == nm.LocalClientId)
                {
                    Status("Connected");
                    OnConnected?.Invoke();
                }
            };
            nm.OnClientDisconnectCallback += clientId =>
            {
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
            };
        }

        void Status(string message)
        {
            Debug.Log($"[VRNet] {message}");
            OnStatusChanged?.Invoke(message);
        }
    }
}
