using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using DSTA.VRNet.Avatar;
using DSTA.VRNet.Core;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class DemoNetworkIntegrationTests
{
    const string SnapshotKey = "VRNet.Demo.Loopback.EditorSnapshot";
    const string XrAsset = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
    const string GuidA = "d357c36d-efc6-4d02-b5f8-a7dbb0f99819";
    const string GuidB = "f71d4bf5-c492-4864-853d-b58d18025439";
    List<NetworkManager> _managers = new();
    Type _stateType;

    [Serializable] class EditorSnapshot
    {
        public SceneSnapshot[] scenes;
        public List<XrSnapshot> xr = new();
    }
    [Serializable] class SceneSnapshot { public string path; public bool loaded; public bool active; }
    [Serializable] class XrSnapshot { public long localId; public bool enabled; }

    [UnityTest]
    public IEnumerator Loopback_ReplicatesPoseAndNightAcrossLateJoinRejoinAndRestart()
    {
        PrepareEditor();
        yield return new EnterPlayMode();
        // Create captured scenario locals after the domain reload. Unity's EditMode
        // runner cannot restore compiler-generated closure objects across this yield.
        yield return RunLoopbackScenario();
    }

    [UnityTest]
    public IEnumerator FailedDirectStart_NotifiesAndRetriesUntilStopped()
    {
        PrepareEditor();
        yield return new EnterPlayMode();
        yield return RunFailedStartScenario();
    }

    IEnumerator RunFailedStartScenario()
    {
        _managers = new List<NetworkManager>();
        var core = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VRNetCore.prefab"));
        var manager = core.GetComponent<NetworkManager>();
        var session = core.GetComponent<VRNetSession>();
        _managers.Add(manager);
        manager.SetSingleton();
        int failures = 0;
        session.OnClientStartFailed += () => failures++;
        yield return null;
        void ExpectInvalidAddress()
        {
            // NGO 2.4 logs endpoint parsing and hostname validation separately;
            // newer supported Editors resolve NGO 2.13, which emits one message.
            var netcodeVersion = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UnityTransport).Assembly).version;
            if (netcodeVersion == "2.4.0")
            {
                LogAssert.Expect(LogType.Error, new Regex("Invalid network endpoint:.*"));
                LogAssert.Expect(LogType.Error, new Regex("Target server network address.*Fully Qualified Domain Name.*"));
            }
            else LogAssert.Expect(LogType.Error, new Regex("(Invalid server address:|Provided connection address).*"));
            LogAssert.Expect(LogType.Error, new Regex(".*shutting down due to network transport start failure.*"));
        }
        ExpectInvalidAddress();
        session.StartClient("not a valid address");
        yield return Until(() => failures == 1, "initial failed-start notification");
        Assert.That(session.GetComponent<ReconnectController>().Enabled, Is.False,
            "The initial connection remains under the caller's control.");

        // Reconnect is enabled by the first successful join. Exercise that same
        // state with a real transport that cannot start, then stop a pending retry.
        session.GetComponent<ReconnectController>().Enabled = true;
        ExpectInvalidAddress();
        ExpectInvalidAddress();
        session.StartClient("not a valid address");
        yield return Until(() => failures == 3, "automatic retry after a direct start failure");
        session.StopSession();
        yield return new WaitForSecondsRealtime(2.5f);
        Assert.That(failures, Is.EqualTo(3), "Intentional stop must cancel pending retries.");
        Assert.That(manager.IsListening, Is.False);
        Assert.That(session.GetComponent<ReconnectController>().Enabled, Is.False);
    }

    IEnumerator RunLoopbackScenario()
    {
        _managers = new List<NetworkManager>();
        _stateType = Type.GetType("DemoSessionState, Assembly-CSharp");
        Assert.That(_stateType, Is.Not.Null);
        var corePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VRNetCore.prefab");
        Assert.That(corePrefab, Is.Not.Null);
        var core = Object.Instantiate(corePrefab);
        Assert.That(core, Is.Not.Null, "VRNetCore prefab instantiation failed.");
        core.name = "Loopback Server";
        var server = core.GetComponent<NetworkManager>();
        var session = core.GetComponent<VRNetSession>();
        Assert.That(server, Is.Not.Null, "The VRNetCore prefab root must contain NetworkManager.");
        Assert.That(session, Is.Not.Null, "The VRNetCore prefab root must contain VRNetSession.");
        _managers.Add(server);
        server.SetSingleton();
        session.Config.serverPort = FreeUdpPort();
        session.Config.discoveryPort = FreeUdpPort();
        session.Config.avatarDespawnGraceSeconds = 30f;
        Assert.That(server.NetworkConfig.EnableSceneManagement, Is.False,
            "The two demo entry scenes must connect without NGO loading the server scene.");
        session.StartServer();
        Assert.That(session.IsServerRunning, Is.True, "Server startup failed.");
        yield return Until(() => State(server) != null, "server session-state spawn");
        Assert.That(IsNight(State(server)), Is.False);

        var first = CreateClient(server, session, GuidA, "Trainee A");
        Assert.That(first.StartClient(), Is.True);
        yield return Until(() => first.IsConnectedClient && State(first) != null, "first client connection and initial state");
        Assert.That(IsNight(State(first)), Is.False);
        Assert.That(Toggle(State(server)), Is.True);
        yield return Until(() => IsNight(State(first)), "night propagation to connected client");

        var second = CreateClient(server, session, GuidB, "Trainee B");
        Assert.That(second.StartClient(), Is.True);
        yield return Until(() => second.IsConnectedClient && State(second) != null, "late client connection");
        Assert.That(IsNight(State(second)), Is.True, "Late join must receive current night state without another click.");
        Assert.That(Toggle(State(first)), Is.False, "Clients must not author instructor state.");
        var settleUntil = Time.realtimeSinceStartup + 0.2f;
        while (Time.realtimeSinceStartup < settleUntil) yield return null;
        Assert.That(IsNight(State(server)) && IsNight(State(first)) && IsNight(State(second)), Is.True);
        Assert.That(session.Registry.ConnectedPlayers.Count(), Is.EqualTo(2));

        var firstId = first.LocalClientId;
        yield return Until(() => Avatar(server, firstId) != null && Avatar(first, firstId) != null && Avatar(second, firstId) != null,
            "first trainee avatar on owner, server and peer");
        var rig = new GameObject("Loopback Rig");
        var head = Child(rig, "Head", new Vector3(2.5f, 1.7f, -1.2f));
        var left = Child(rig, "Left", new Vector3(2.1f, 1.2f, -0.9f));
        var right = Child(rig, "Right", new Vector3(2.9f, 1.3f, -0.8f));
        rig.transform.position = new Vector3(2f, 0f, -1f);
        VRNetSession.SetLocalRig(rig.transform, head, left, right);
        yield return Until(() => PoseMatches(Avatar(server, firstId), rig.transform, head, left, right)
            && PoseMatches(Avatar(second, firstId), rig.transform, head, left, right), "owner pose reaching server and peer");

        var frozen = Avatar(server, firstId);
        first.Shutdown();
        yield return Until(() => !first.IsListening && !first.ShutdownInProgress
            && session.Registry.TryGetByGuid(GuidA, out var record) && !record.Connected, "disconnect roster update");
        Assert.That(frozen, Is.Not.Null, "Disconnected avatar should remain during grace time.");
        var frozenHead = frozen.Head.position;
        rig.transform.position += Vector3.right * 3f;
        settleUntil = Time.realtimeSinceStartup + 0.2f;
        while (Time.realtimeSinceStartup < settleUntil) yield return null;
        Assert.That(Vector3.Distance(frozen.Head.position, frozenHead), Is.LessThan(0.03f),
            "A dedicated server must not move an orphaned avatar from the registered local rig.");

        Assert.That(first.StartClient(), Is.True);
        yield return Until(() => first.IsConnectedClient && State(first) != null
            && session.Registry.TryGetByGuid(GuidA, out var record) && record.Connected, "same-identity reconnect");
        Assert.That(first.LocalClientId, Is.Not.EqualTo(firstId));
        Assert.That(session.Registry.AllPlayers.Count(), Is.EqualTo(2), "Rejoin must reuse the persistent roster entry.");
        Assert.That(session.Registry.ConnectedPlayers.Count(), Is.EqualTo(2));
        Assert.That(IsNight(State(first)), Is.True, "Rejoin must receive current state.");
        yield return Until(() => frozen == null || !frozen.IsSpawned, "stale avatar replacement on rejoin");

        first.Shutdown();
        second.Shutdown();
        session.StopSession();
        yield return Until(() => _managers.All(manager => !manager.IsListening && !manager.ShutdownInProgress), "all transports stopped");
        Assert.That(session.Registry.AllPlayers.Count(), Is.Zero);
        session.StartServer();
        Assert.That(session.IsServerRunning, Is.True);
        yield return Until(() => State(server) != null, "new server state after restart");
        Assert.That(IsNight(State(server)), Is.False, "A new instructor session starts in day mode.");
        Assert.That(first.StartClient(), Is.True);
        yield return Until(() => first.IsConnectedClient && State(first) != null, "client reconnect after server restart");
        Assert.That(IsNight(State(first)), Is.False);
        Assert.That(session.Registry.AllPlayers.Count(), Is.EqualTo(1));
        Object.Destroy(rig);
    }

    NetworkManager CreateClient(NetworkManager server, VRNetSession session, string guid, string playerName)
    {
        var go = new GameObject("Loopback " + playerName);
        var transport = go.AddComponent<UnityTransport>();
        var client = go.AddComponent<NetworkManager>();
        _managers.Add(client);
        client.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
            EnableSceneManagement = false,
            ConnectionApproval = true,
            PlayerPrefab = server.NetworkConfig.PlayerPrefab,
            TickRate = server.NetworkConfig.TickRate,
            ConnectionData = new ConnectionPayload
            {
                playerGuid = guid, playerName = playerName,
                teamId = session.Config.teamId, appVersion = session.Config.appVersion
            }.ToBytes()
        };
        client.NetworkConfig.Prefabs.NetworkPrefabsLists.AddRange(server.NetworkConfig.Prefabs.NetworkPrefabsLists);
        transport.SetConnectionData("127.0.0.1", (ushort)session.Config.serverPort);
        transport.ConnectTimeoutMS = 500;
        transport.MaxConnectAttempts = 5;
        // NGO InstantiateNetworkPrefab assigns NetworkManagerOwner to the receiving
        // manager before spawn. Only the server uses the process-wide session singleton.
        return client;
    }

    Component State(NetworkManager manager)
    {
        if (manager == null || manager.SpawnManager == null) return null;
        return manager.SpawnManager.SpawnedObjectsList
            .Where(value => value != null && value.IsSpawned)
            .Select(value => value.GetComponent(_stateType)).FirstOrDefault(value => value != null);
    }

    static VRNetAvatar Avatar(NetworkManager manager, ulong owner)
        => manager.SpawnManager.SpawnedObjectsList.Where(value => value != null && value.OwnerClientId == owner)
            .Select(value => value.GetComponent<VRNetAvatar>()).FirstOrDefault(value => value != null);

    bool IsNight(Component state) => state != null && (bool)_stateType.GetProperty("IsNight").GetValue(state);
    bool Toggle(Component state) => (bool)_stateType.GetMethod("ToggleDayNight").Invoke(state, null);

    static Transform Child(GameObject parent, string name, Vector3 position)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent.transform);
        child.position = position;
        return child;
    }

    static bool PoseMatches(VRNetAvatar avatar, Transform root, Transform head, Transform left, Transform right)
        => avatar != null && Vector3.Distance(avatar.transform.position, root.position) < 0.04f
            && Vector3.Distance(avatar.Head.position, head.position) < 0.04f
            && Vector3.Distance(avatar.LeftHand.position, left.position) < 0.04f
            && Vector3.Distance(avatar.RightHand.position, right.position) < 0.04f;

    IEnumerator Until(Func<bool> condition, string phase)
    {
        var deadline = Time.realtimeSinceStartup + 12f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "Timed out: " + phase + ". " + string.Join("; ", _managers
            .Where(manager => manager != null).Select(manager => $"{manager.name}: listening={manager.IsListening}, connected={manager.IsConnectedClient}, clients={manager.ConnectedClientsIds.Count}, reason={manager.DisconnectReason}")));
    }

    static int FreeUdpPort()
    {
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.Client.LocalEndPoint).Port;
    }

    static void PrepareEditor()
    {
        if (Application.isPlaying) return;
        Assert.That(EditorSceneManager.GetSceneManagerSetup().All(scene => !UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene.path).isDirty),
            Is.True, "Save open scenes before running the loopback integration test.");
        var snapshot = new EditorSnapshot
        {
            scenes = EditorSceneManager.GetSceneManagerSetup().Select(scene => new SceneSnapshot
                { path = scene.path, loaded = scene.isLoaded, active = scene.isActive }).ToArray()
        };
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(XrAsset))
        {
            var serialized = new SerializedObject(asset);
            var property = serialized.FindProperty("m_InitManagerOnStart");
            if (property == null) continue;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long localId);
            snapshot.xr.Add(new XrSnapshot { localId = localId, enabled = property.boolValue });
        }
        SessionState.SetString(SnapshotKey, JsonUtility.ToJson(snapshot));
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(XrAsset))
        {
            var serialized = new SerializedObject(asset);
            var property = serialized.FindProperty("m_InitManagerOnStart");
            if (property == null) continue;
            property.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        _managers ??= new List<NetworkManager>();
        if (Application.isPlaying)
        {
            foreach (var manager in _managers)
                if (manager != null) manager.Shutdown(true);
            var deadline = Time.realtimeSinceStartup + 5f;
            while (_managers.Any(manager => manager != null && manager.IsListening) && Time.realtimeSinceStartup < deadline)
                yield return null;
            foreach (var manager in _managers)
                if (manager != null) Object.Destroy(manager.gameObject);
            VRNetSession.SetLocalRig(null, null, null, null);
            yield return null;
            yield return new ExitPlayMode();
        }
        _managers.Clear();
        RestoreEditorState();
    }

    public static void RestoreEditorState()
    {
        var json = SessionState.GetString(SnapshotKey, "");
        if (string.IsNullOrEmpty(json)) return;
        var snapshot = JsonUtility.FromJson<EditorSnapshot>(json);
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(XrAsset))
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long localId);
            var saved = snapshot.xr.FirstOrDefault(value => value.localId == localId);
            if (saved == null) continue;
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("m_InitManagerOnStart").boolValue = saved.enabled;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssets();
        if (snapshot.scenes != null && snapshot.scenes.Length > 0)
            EditorSceneManager.RestoreSceneManagerSetup(snapshot.scenes.Select(scene => new SceneSetup
                { path = scene.path, isLoaded = scene.loaded, isActive = scene.active }).ToArray());
        SessionState.EraseString(SnapshotKey);
    }
}
