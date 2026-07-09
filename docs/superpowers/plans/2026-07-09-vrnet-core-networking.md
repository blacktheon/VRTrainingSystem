# VRNet Core Networking Implementation Plan (Plan 1 of 3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the `com.dsta.vrnet` embedded UPM package core: LAN discovery, dedicated-server connection with team/version approval, owner-authoritative VR avatar sync, and auto-reconnect — demonstrated by the two demo scenes.

**Architecture:** Tablet runs NGO 2.x as dedicated server and broadcasts team-scoped UDP discovery messages; headsets discover, connect with an identity payload (persistent PlayerGuid + teamId + appVersion) validated by server-side connection approval, and mirror their local XR rig onto a networked avatar. Disconnects trigger client-side backoff reconnect; the server retains player identity and freezes avatars for a grace period.

**Tech Stack:** Unity 6000.3.19f1, Netcode for GameObjects 2.x, UnityTransport (UDP), VIVE OpenXR, NUnit (EditMode tests), Multiplayer Play Mode for integration testing.

**Execution notes:**
- Work directly in `L:\Unity\DSTA\VRTrainingSystem` on branch `develop`. Do NOT use a git worktree — Unity would rebuild the Library folder from scratch.
- The Unity editor is open on this project with unity-mcp connected. After creating/editing C# files, focus the editor (or use unity-mcp `Unity_RunCommand` to call `UnityEditor.AssetDatabase.Refresh()`), then check for compile errors with `Unity_GetConsoleLogs`.
- Run EditMode tests via the Test Runner window (Window → General → Test Runner → EditMode) or via unity-mcp. Expected counts are cumulative per task.
- The spec is at `docs/superpowers/specs/2026-07-09-vrnet-library-design.md`.

---

## File structure (what this plan creates)

```
Packages/com.dsta.vrnet/
├── package.json
├── Runtime/
│   ├── DSTA.VRNet.asmdef
│   ├── Core/
│   │   ├── VRNetConfig.cs            config ScriptableObject (ports, timeouts, team)
│   │   ├── PlayerIdentity.cs         persistent per-device PlayerGuid
│   │   ├── ConnectionPayload.cs      client→server identity payload
│   │   ├── ConnectionValidator.cs    pure approval logic (unit-tested)
│   │   ├── VRNetPlayerRegistry.cs    server-side guid↔client mapping (unit-tested)
│   │   ├── VRNetSession.cs           facade: StartServer/StartClient/SetLocalRig/events
│   │   └── ReconnectController.cs    client backoff reconnect
│   ├── Discovery/
│   │   ├── DiscoveryMessage.cs       broadcast payload + parsing (unit-tested)
│   │   ├── DiscoveryBroadcaster.cs   server UDP broadcast
│   │   └── DiscoveryListener.cs      client UDP listen + main-thread dispatch
│   └── Avatar/
│       ├── LocalRig.cs               static registry for the game's XR rig
│       ├── OwnerNetworkTransform.cs  owner-authoritative NetworkTransform
│       └── VRNetAvatar.cs            networked avatar mirroring LocalRig
├── Editor/DSTA.VRNet.Editor.asmdef   (empty assembly for now; BuildMenu is demo-side)
└── Tests/Editor/
    ├── DSTA.VRNet.Tests.asmdef
    ├── DiscoveryMessageTests.cs
    ├── ConnectionValidatorTests.cs
    ├── PlayerIdentityTests.cs
    └── VRNetPlayerRegistryTests.cs

Assets/Scripts/Demo/
├── DemoRigBinder.cs                  binds XR rig into LocalRig
├── DemoFakeRig.cs                    fake moving rig for desktop/MPPM testing
├── DemoClientUI.cs                   headset connect UI
├── DemoServerUI.cs                   tablet server UI
└── Editor/BuildMenu.cs               "VRNet/Build ..." APK menu items
```

---

### Task 1: Install packages and create the UPM package skeleton

**Files:**
- Modify: `Packages/manifest.json`
- Create: `Packages/com.dsta.vrnet/package.json`
- Create: `Packages/com.dsta.vrnet/Runtime/DSTA.VRNet.asmdef`
- Create: `Packages/com.dsta.vrnet/Editor/DSTA.VRNet.Editor.asmdef`
- Create: `Packages/com.dsta.vrnet/Tests/Editor/DSTA.VRNet.Tests.asmdef`

- [ ] **Step 1: Add NGO and Multiplayer Play Mode to `Packages/manifest.json`**

Add these two lines inside `"dependencies"` (alphabetical position doesn't matter), and add the `"testables"` key at the top level of the JSON (sibling of `"dependencies"`):

```json
"com.unity.netcode.gameobjects": "2.4.0",
"com.unity.multiplayer.playmode": "1.4.0",
```

```json
"testables": ["com.dsta.vrnet"]
```

If Unity fails to resolve either version, open Package Manager (Window → Package Manager → Unity Registry) and install "Netcode for GameObjects" and "Multiplayer Play Mode" by name — then keep whatever version it resolves.

- [ ] **Step 2: Create `Packages/com.dsta.vrnet/package.json`**

```json
{
  "name": "com.dsta.vrnet",
  "version": "0.1.0",
  "displayName": "VRNet Training Network",
  "description": "Multiplayer networking library for VR training: dedicated tablet server, team-scoped LAN discovery, avatar sync, scenario control, session recording.",
  "unity": "6000.0",
  "dependencies": {
    "com.unity.netcode.gameobjects": "2.4.0"
  }
}
```

- [ ] **Step 3: Create `Packages/com.dsta.vrnet/Runtime/DSTA.VRNet.asmdef`**

```json
{
  "name": "DSTA.VRNet",
  "rootNamespace": "DSTA.VRNet",
  "references": [
    "Unity.Netcode.Runtime",
    "Unity.Netcode.Components"
  ],
  "autoReferenced": true
}
```

- [ ] **Step 4: Create `Packages/com.dsta.vrnet/Editor/DSTA.VRNet.Editor.asmdef`**

```json
{
  "name": "DSTA.VRNet.Editor",
  "rootNamespace": "DSTA.VRNet.Editor",
  "references": ["DSTA.VRNet"],
  "includePlatforms": ["Editor"],
  "autoReferenced": true
}
```

- [ ] **Step 5: Create `Packages/com.dsta.vrnet/Tests/Editor/DSTA.VRNet.Tests.asmdef`**

```json
{
  "name": "DSTA.VRNet.Tests",
  "rootNamespace": "DSTA.VRNet.Tests",
  "references": ["DSTA.VRNet", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
  "includePlatforms": ["Editor"],
  "precompiledReferences": ["nunit.framework.dll"],
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "autoReferenced": false
}
```

- [ ] **Step 6: Verify compile**

Focus the Unity editor (or trigger `AssetDatabase.Refresh()` via unity-mcp), wait for import, then check `Unity_GetConsoleLogs`.
Expected: no compile errors; Package Manager shows "VRNet Training Network 0.1.0" under "In Project".

- [ ] **Step 7: Commit**

```bash
git add Packages/manifest.json Packages/packages-lock.json "Packages/com.dsta.vrnet"
git commit -m "feat: add com.dsta.vrnet package skeleton with NGO 2.x"
```

---

### Task 2: DiscoveryMessage (TDD)

**Files:**
- Create: `Packages/com.dsta.vrnet/Tests/Editor/DiscoveryMessageTests.cs`
- Create: `Packages/com.dsta.vrnet/Runtime/Discovery/DiscoveryMessage.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Text;
using NUnit.Framework;
using DSTA.VRNet.Discovery;

namespace DSTA.VRNet.Tests
{
    public class DiscoveryMessageTests
    {
        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var original = DiscoveryMessage.Create("TEAM-A", "Tablet 1", 7777, "0.1.0");
            bool ok = DiscoveryMessage.TryParse(original.ToBytes(), out var parsed);

            Assert.IsTrue(ok);
            Assert.AreEqual("TEAM-A", parsed.teamId);
            Assert.AreEqual("Tablet 1", parsed.serverName);
            Assert.AreEqual(7777, parsed.port);
            Assert.AreEqual("0.1.0", parsed.appVersion);
        }

        [Test]
        public void TryParse_Garbage_ReturnsFalse()
        {
            bool ok = DiscoveryMessage.TryParse(Encoding.UTF8.GetBytes("not json {{{"), out _);
            Assert.IsFalse(ok);
        }

        [Test]
        public void TryParse_WrongMagic_ReturnsFalse()
        {
            var msg = DiscoveryMessage.Create("TEAM-A", "x", 7777, "0.1.0");
            msg.magic = "NOPE";
            bool ok = DiscoveryMessage.TryParse(msg.ToBytes(), out _);
            Assert.IsFalse(ok);
        }

        [Test]
        public void TryParse_NullOrEmpty_ReturnsFalse()
        {
            Assert.IsFalse(DiscoveryMessage.TryParse(null, out _));
            Assert.IsFalse(DiscoveryMessage.TryParse(new byte[0], out _));
        }
    }
}
```

- [ ] **Step 2: Verify red**

Focus editor / refresh. Expected: **compile error** (`DiscoveryMessage` does not exist) — that is this step's "failing test".

- [ ] **Step 3: Implement `DiscoveryMessage.cs`**

```csharp
using System;
using System.Text;
using UnityEngine;

namespace DSTA.VRNet.Discovery
{
    /// <summary>
    /// Payload broadcast by the server tablet over UDP so headsets can find it.
    /// JSON-encoded for forward compatibility; 'magic' guards against foreign packets.
    /// </summary>
    [Serializable]
    public struct DiscoveryMessage
    {
        public const string MagicValue = "VRNET1";

        public string magic;
        public string teamId;
        public string serverName;
        public int port;
        public string appVersion;

        public static DiscoveryMessage Create(string teamId, string serverName, int port, string appVersion)
        {
            return new DiscoveryMessage
            {
                magic = MagicValue,
                teamId = teamId,
                serverName = serverName,
                port = port,
                appVersion = appVersion
            };
        }

        public byte[] ToBytes() => Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));

        public static bool TryParse(byte[] data, out DiscoveryMessage message)
        {
            message = default;
            if (data == null || data.Length == 0) return false;
            try
            {
                message = JsonUtility.FromJson<DiscoveryMessage>(Encoding.UTF8.GetString(data));
            }
            catch (Exception)
            {
                return false;
            }
            return message.magic == MagicValue;
        }
    }
}
```

- [ ] **Step 4: Verify green**

Run EditMode tests in Test Runner. Expected: **4 pass**.

- [ ] **Step 5: Commit**

```bash
git add "Packages/com.dsta.vrnet"
git commit -m "feat: team-scoped discovery message with parse validation"
```

---

### Task 3: ConnectionPayload + ConnectionValidator (TDD)

**Files:**
- Create: `Packages/com.dsta.vrnet/Tests/Editor/ConnectionValidatorTests.cs`
- Create: `Packages/com.dsta.vrnet/Runtime/Core/ConnectionPayload.cs`
- Create: `Packages/com.dsta.vrnet/Runtime/Core/ConnectionValidator.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Text;
using NUnit.Framework;
using DSTA.VRNet.Core;

namespace DSTA.VRNet.Tests
{
    public class ConnectionValidatorTests
    {
        static byte[] Payload(string guid = "5f0c7c9e-9d3a-4a5e-8b21-2f6f0a34d111",
                              string team = "TEAM-A", string version = "0.1.0", string name = "Trainee 1")
        {
            return new ConnectionPayload
            {
                playerGuid = guid, teamId = team, appVersion = version, playerName = name
            }.ToBytes();
        }

        [Test]
        public void Validate_MatchingTeamAndVersion_Approves()
        {
            var result = ConnectionValidator.Validate(Payload(), "TEAM-A", "0.1.0");
            Assert.IsTrue(result.Approved);
            Assert.AreEqual("Trainee 1", result.Payload.playerName);
        }

        [Test]
        public void Validate_WrongTeam_Rejects()
        {
            var result = ConnectionValidator.Validate(Payload(team: "TEAM-B"), "TEAM-A", "0.1.0");
            Assert.IsFalse(result.Approved);
            StringAssert.Contains("team", result.Reason.ToLower());
        }

        [Test]
        public void Validate_WrongVersion_Rejects()
        {
            var result = ConnectionValidator.Validate(Payload(version: "9.9.9"), "TEAM-A", "0.1.0");
            Assert.IsFalse(result.Approved);
            StringAssert.Contains("version", result.Reason.ToLower());
        }

        [Test]
        public void Validate_MalformedPayload_Rejects()
        {
            var result = ConnectionValidator.Validate(Encoding.UTF8.GetBytes("garbage"), "TEAM-A", "0.1.0");
            Assert.IsFalse(result.Approved);
        }

        [Test]
        public void Validate_InvalidGuid_Rejects()
        {
            var result = ConnectionValidator.Validate(Payload(guid: "not-a-guid"), "TEAM-A", "0.1.0");
            Assert.IsFalse(result.Approved);
        }
    }
}
```

- [ ] **Step 2: Verify red** — refresh; expected compile error (`ConnectionPayload` does not exist).

- [ ] **Step 3: Implement `ConnectionPayload.cs`**

```csharp
using System;
using System.Text;
using UnityEngine;

namespace DSTA.VRNet.Core
{
    /// <summary>Identity a client sends with its connection request (NGO ConnectionData).</summary>
    [Serializable]
    public struct ConnectionPayload
    {
        public string playerGuid;
        public string playerName;
        public string teamId;
        public string appVersion;

        public byte[] ToBytes() => Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));

        public static bool TryParse(byte[] data, out ConnectionPayload payload)
        {
            payload = default;
            if (data == null || data.Length == 0) return false;
            try
            {
                payload = JsonUtility.FromJson<ConnectionPayload>(Encoding.UTF8.GetString(data));
            }
            catch (Exception)
            {
                return false;
            }
            return Guid.TryParse(payload.playerGuid, out _);
        }
    }
}
```

- [ ] **Step 4: Implement `ConnectionValidator.cs`**

```csharp
namespace DSTA.VRNet.Core
{
    public struct ValidationResult
    {
        public bool Approved;
        public string Reason;
        public ConnectionPayload Payload;
    }

    /// <summary>Pure server-side approval logic — no Unity/network dependencies so it is unit-testable.</summary>
    public static class ConnectionValidator
    {
        public static ValidationResult Validate(byte[] payloadBytes, string serverTeamId, string serverAppVersion)
        {
            if (!ConnectionPayload.TryParse(payloadBytes, out var payload))
                return new ValidationResult { Approved = false, Reason = "Malformed connection payload" };

            if (payload.teamId != serverTeamId)
                return new ValidationResult
                {
                    Approved = false,
                    Reason = $"Wrong team: client is '{payload.teamId}', server is '{serverTeamId}'"
                };

            if (payload.appVersion != serverAppVersion)
                return new ValidationResult
                {
                    Approved = false,
                    Reason = $"App version mismatch: client {payload.appVersion}, server {serverAppVersion}"
                };

            return new ValidationResult { Approved = true, Reason = "", Payload = payload };
        }
    }
}
```

- [ ] **Step 5: Verify green** — run EditMode tests. Expected: **9 pass** (4 + 5).

- [ ] **Step 6: Commit**

```bash
git add "Packages/com.dsta.vrnet"
git commit -m "feat: connection payload and server-side approval validation"
```

---

### Task 4: PlayerIdentity (TDD)

**Files:**
- Create: `Packages/com.dsta.vrnet/Tests/Editor/PlayerIdentityTests.cs`
- Create: `Packages/com.dsta.vrnet/Runtime/Core/PlayerIdentity.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using NUnit.Framework;
using DSTA.VRNet.Core;

namespace DSTA.VRNet.Tests
{
    public class PlayerIdentityTests
    {
        [SetUp]
        public void ClearStoredGuid() => PlayerIdentity.Reset();

        [Test]
        public void GetOrCreate_ReturnsValidGuid()
        {
            Assert.IsTrue(Guid.TryParse(PlayerIdentity.GetOrCreate(), out _));
        }

        [Test]
        public void GetOrCreate_IsStableAcrossCalls()
        {
            Assert.AreEqual(PlayerIdentity.GetOrCreate(), PlayerIdentity.GetOrCreate());
        }

        [Test]
        public void Reset_ProducesNewGuid()
        {
            var first = PlayerIdentity.GetOrCreate();
            PlayerIdentity.Reset();
            Assert.AreNotEqual(first, PlayerIdentity.GetOrCreate());
        }
    }
}
```

- [ ] **Step 2: Verify red** — refresh; expected compile error.

- [ ] **Step 3: Implement `PlayerIdentity.cs`**

```csharp
using System;
using UnityEngine;

namespace DSTA.VRNet.Core
{
    /// <summary>
    /// Persistent per-device player identity. Survives app restarts so the server
    /// can recognize a reconnecting headset as the same trainee.
    /// </summary>
    public static class PlayerIdentity
    {
        const string PrefsKey = "vrnet.playerGuid";

        public static string GetOrCreate()
        {
            var existing = PlayerPrefs.GetString(PrefsKey, "");
            if (Guid.TryParse(existing, out _)) return existing;

            var fresh = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(PrefsKey, fresh);
            PlayerPrefs.Save();
            return fresh;
        }

        public static void Reset() => PlayerPrefs.DeleteKey(PrefsKey);
    }
}
```

- [ ] **Step 4: Verify green** — run EditMode tests. Expected: **12 pass**.

- [ ] **Step 5: Commit**

```bash
git add "Packages/com.dsta.vrnet"
git commit -m "feat: persistent per-device player identity"
```

---

### Task 5: VRNetConfig

**Files:**
- Create: `Packages/com.dsta.vrnet/Runtime/Core/VRNetConfig.cs`

- [ ] **Step 1: Implement `VRNetConfig.cs`** (plain data holder — no test beyond compile)

```csharp
using UnityEngine;

namespace DSTA.VRNet.Core
{
    /// <summary>All tunables for one deployment. Create one asset per project via the asset menu.</summary>
    [CreateAssetMenu(menuName = "VRNet/Config", fileName = "VRNetConfig")]
    public class VRNetConfig : ScriptableObject
    {
        [Header("Identity")]
        public string teamId = "TEAM-A";
        public string serverName = "VRNet Server";
        public string appVersion = "0.1.0";

        [Header("Network")]
        public int serverPort = 7777;
        public int discoveryPort = 47777;
        public float discoveryIntervalSeconds = 1f;
        public float discoveryTimeoutSeconds = 6f;

        [Header("Resilience")]
        [Tooltip("How long UTP tolerates silence before declaring disconnect. Generous for shaky Wi-Fi.")]
        public int disconnectTimeoutMs = 10000;
        public int connectTimeoutMs = 2000;
        public int maxConnectAttempts = 5;
        [Tooltip("Server keeps a dropped player's frozen avatar this long awaiting rejoin.")]
        public float avatarDespawnGraceSeconds = 30f;
        public float reconnectMaxBackoffSeconds = 10f;
    }
}
```

- [ ] **Step 2: Verify compile** — refresh; `Unity_GetConsoleLogs` shows no errors. Confirm menu Assets → Create → VRNet → Config exists.

- [ ] **Step 3: Commit**

```bash
git add "Packages/com.dsta.vrnet"
git commit -m "feat: VRNetConfig scriptable object"
```

---

### Task 6: VRNetPlayerRegistry (TDD)

**Files:**
- Create: `Packages/com.dsta.vrnet/Tests/Editor/VRNetPlayerRegistryTests.cs`
- Create: `Packages/com.dsta.vrnet/Runtime/Core/VRNetPlayerRegistry.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;
using DSTA.VRNet.Core;

namespace DSTA.VRNet.Tests
{
    public class VRNetPlayerRegistryTests
    {
        const string GuidA = "5f0c7c9e-9d3a-4a5e-8b21-2f6f0a34d111";

        static ConnectionPayload Payload(string guid = GuidA, string name = "Trainee 1")
            => new ConnectionPayload { playerGuid = guid, playerName = name, teamId = "TEAM-A", appVersion = "0.1.0" };

        [Test]
        public void FirstJoin_IsNew_AndConnected()
        {
            var registry = new VRNetPlayerRegistry();
            var kind = registry.RecordApproved(1, Payload());

            Assert.AreEqual(JoinKind.New, kind);
            Assert.IsTrue(registry.TryGetByClient(1, out var record));
            Assert.IsTrue(record.Connected);
            Assert.AreEqual("Trainee 1", record.PlayerName);
        }

        [Test]
        public void Disconnect_MarksNotConnected_AndUnmapsClientId()
        {
            var registry = new VRNetPlayerRegistry();
            registry.RecordApproved(1, Payload());
            var record = registry.RecordDisconnected(1);

            Assert.IsNotNull(record);
            Assert.IsFalse(record.Connected);
            Assert.IsFalse(registry.TryGetByClient(1, out _));
            Assert.IsTrue(registry.TryGetByGuid(GuidA, out _));
        }

        [Test]
        public void Rejoin_SameGuidNewClientId_IsRejoin_AndRemaps()
        {
            var registry = new VRNetPlayerRegistry();
            registry.RecordApproved(1, Payload());
            registry.RecordDisconnected(1);
            var kind = registry.RecordApproved(2, Payload());

            Assert.AreEqual(JoinKind.Rejoin, kind);
            Assert.IsFalse(registry.TryGetByClient(1, out _));
            Assert.IsTrue(registry.TryGetByClient(2, out var record));
            Assert.IsTrue(record.Connected);
        }

        [Test]
        public void Disconnect_UnknownClient_ReturnsNull()
        {
            Assert.IsNull(new VRNetPlayerRegistry().RecordDisconnected(42));
        }

        [Test]
        public void ConnectedPlayers_ExcludesDropped()
        {
            var registry = new VRNetPlayerRegistry();
            registry.RecordApproved(1, Payload());
            registry.RecordApproved(2, Payload(guid: "6a1d8d0f-1e4b-4b6f-9c32-3a7b1b45e222", name: "Trainee 2"));
            registry.RecordDisconnected(1);

            var connected = new System.Collections.Generic.List<VRNetPlayerRegistry.PlayerRecord>(registry.ConnectedPlayers);
            Assert.AreEqual(1, connected.Count);
            Assert.AreEqual("Trainee 2", connected[0].PlayerName);
        }
    }
}
```

- [ ] **Step 2: Verify red** — refresh; expected compile error.

- [ ] **Step 3: Implement `VRNetPlayerRegistry.cs`**

```csharp
using System.Collections.Generic;
using System.Linq;

namespace DSTA.VRNet.Core
{
    public enum JoinKind { New, Rejoin }

    /// <summary>
    /// Server-side bookkeeping: which persistent PlayerGuid is behind which transient
    /// NGO clientId. Records survive disconnects so rejoining trainees keep their identity.
    /// Pure data class — no Unity dependencies.
    /// </summary>
    public class VRNetPlayerRegistry
    {
        public class PlayerRecord
        {
            public string PlayerGuid;
            public string PlayerName;
            public ulong ClientId;
            public bool Connected;
        }

        readonly Dictionary<string, PlayerRecord> _byGuid = new();
        readonly Dictionary<ulong, string> _guidByClient = new();

        public IEnumerable<PlayerRecord> ConnectedPlayers => _byGuid.Values.Where(r => r.Connected);
        public IEnumerable<PlayerRecord> AllPlayers => _byGuid.Values;

        public JoinKind RecordApproved(ulong clientId, ConnectionPayload payload)
        {
            if (_byGuid.TryGetValue(payload.playerGuid, out var existing))
            {
                _guidByClient.Remove(existing.ClientId);
                existing.ClientId = clientId;
                existing.Connected = true;
                existing.PlayerName = payload.playerName;
                _guidByClient[clientId] = payload.playerGuid;
                return JoinKind.Rejoin;
            }

            _byGuid[payload.playerGuid] = new PlayerRecord
            {
                PlayerGuid = payload.playerGuid,
                PlayerName = payload.playerName,
                ClientId = clientId,
                Connected = true
            };
            _guidByClient[clientId] = payload.playerGuid;
            return JoinKind.New;
        }

        public PlayerRecord RecordDisconnected(ulong clientId)
        {
            if (!_guidByClient.TryGetValue(clientId, out var guid)) return null;
            var record = _byGuid[guid];
            record.Connected = false;
            _guidByClient.Remove(clientId);
            return record;
        }

        public bool TryGetByClient(ulong clientId, out PlayerRecord record)
        {
            record = null;
            return _guidByClient.TryGetValue(clientId, out var guid) && _byGuid.TryGetValue(guid, out record);
        }

        public bool TryGetByGuid(string playerGuid, out PlayerRecord record)
            => _byGuid.TryGetValue(playerGuid, out record);
    }
}
```

- [ ] **Step 4: Verify green** — run EditMode tests. Expected: **17 pass**.

- [ ] **Step 5: Commit**

```bash
git add "Packages/com.dsta.vrnet"
git commit -m "feat: server-side player registry with rejoin identity mapping"
```

---

### Task 7: Discovery broadcaster and listener

**Files:**
- Create: `Packages/com.dsta.vrnet/Runtime/Discovery/DiscoveryBroadcaster.cs`
- Create: `Packages/com.dsta.vrnet/Runtime/Discovery/DiscoveryListener.cs`

These are thin socket wrappers — no unit tests (message logic was tested in Task 2); they get exercised in the Task 12 integration checklist.

- [ ] **Step 1: Implement `DiscoveryBroadcaster.cs`**

```csharp
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace DSTA.VRNet.Discovery
{
    /// <summary>Server-side: periodically broadcasts a DiscoveryMessage on the LAN.</summary>
    public class DiscoveryBroadcaster : MonoBehaviour
    {
        UdpClient _udp;
        byte[] _bytes;
        int _port;

        public void StartBroadcasting(DiscoveryMessage message, int discoveryPort, float intervalSeconds)
        {
            StopBroadcasting();
            _udp = new UdpClient { EnableBroadcast = true };
            _bytes = message.ToBytes();
            _port = discoveryPort;
            InvokeRepeating(nameof(Broadcast), 0f, intervalSeconds);
        }

        public void StopBroadcasting()
        {
            CancelInvoke(nameof(Broadcast));
            _udp?.Close();
            _udp = null;
        }

        void Broadcast()
        {
            try
            {
                _udp.Send(_bytes, _bytes.Length, new IPEndPoint(IPAddress.Broadcast, _port));
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"[VRNet] Discovery broadcast failed: {e.Message}");
            }
        }

        void OnDestroy() => StopBroadcasting();
    }
}
```

- [ ] **Step 2: Implement `DiscoveryListener.cs`**

```csharp
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace DSTA.VRNet.Discovery
{
    /// <summary>
    /// Client-side: listens for server broadcasts, filters by team, and raises
    /// OnServerFound on the main thread. ReuseAddress allows several listeners on
    /// one machine (Multiplayer Play Mode virtual players).
    /// </summary>
    public class DiscoveryListener : MonoBehaviour
    {
        public event Action<IPAddress, DiscoveryMessage> OnServerFound;

        UdpClient _udp;
        string _teamFilter;
        readonly ConcurrentQueue<(IPAddress address, DiscoveryMessage message)> _found = new();

        public void StartListening(int discoveryPort, string teamFilter)
        {
            StopListening();
            _teamFilter = teamFilter;
            _udp = new UdpClient();
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udp.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));
            _ = ReceiveLoop(_udp);
        }

        public void StopListening()
        {
            _udp?.Close();
            _udp = null;
        }

        async Task ReceiveLoop(UdpClient udp)
        {
            while (true)
            {
                UdpReceiveResult result;
                try { result = await udp.ReceiveAsync(); }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) { return; }

                if (DiscoveryMessage.TryParse(result.Buffer, out var message) && message.teamId == _teamFilter)
                    _found.Enqueue((result.RemoteEndPoint.Address, message));
            }
        }

        void Update()
        {
            while (_found.TryDequeue(out var hit))
                OnServerFound?.Invoke(hit.address, hit.message);
        }

        void OnDestroy() => StopListening();
    }
}
```

- [ ] **Step 3: Verify compile** — refresh; no console errors. All 17 tests still pass.

- [ ] **Step 4: Commit**

```bash
git add "Packages/com.dsta.vrnet"
git commit -m "feat: UDP discovery broadcaster and team-filtered listener"
```

---

### Task 8: Avatar layer — LocalRig, OwnerNetworkTransform, VRNetAvatar

**Files:**
- Create: `Packages/com.dsta.vrnet/Runtime/Avatar/LocalRig.cs`
- Create: `Packages/com.dsta.vrnet/Runtime/Avatar/OwnerNetworkTransform.cs`
- Create: `Packages/com.dsta.vrnet/Runtime/Avatar/VRNetAvatar.cs`

- [ ] **Step 1: Implement `LocalRig.cs`**

```csharp
using UnityEngine;

namespace DSTA.VRNet.Avatar
{
    /// <summary>
    /// The integration point that replaces game-specific rig singletons:
    /// the game registers its XR rig transforms here (via VRNetSession.SetLocalRig)
    /// and the library's avatar mirrors them. The library never references game types.
    /// </summary>
    public static class LocalRig
    {
        public static Transform Root { get; private set; }
        public static Transform Head { get; private set; }
        public static Transform LeftHand { get; private set; }
        public static Transform RightHand { get; private set; }

        public static bool IsSet => Root != null && Head != null && LeftHand != null && RightHand != null;

        public static void Set(Transform root, Transform head, Transform leftHand, Transform rightHand)
        {
            Root = root;
            Head = head;
            LeftHand = leftHand;
            RightHand = rightHand;
        }

        public static void Clear() => Set(null, null, null, null);
    }
}
```

- [ ] **Step 2: Implement `OwnerNetworkTransform.cs`**

```csharp
using Unity.Netcode.Components;

namespace DSTA.VRNet.Avatar
{
    /// <summary>Owner-authoritative NetworkTransform: each headset drives its own avatar.</summary>
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
```

- [ ] **Step 3: Implement `VRNetAvatar.cs`**

```csharp
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace DSTA.VRNet.Avatar
{
    /// <summary>
    /// Networked player avatar. The owner mirrors its LocalRig in world space each
    /// frame; OwnerNetworkTransforms replicate to everyone else. The owner's own
    /// renderers are hidden (you don't want to see your own head from inside).
    /// </summary>
    public class VRNetAvatar : NetworkBehaviour
    {
        public static readonly List<VRNetAvatar> All = new();

        [SerializeField] Transform _head;
        [SerializeField] Transform _leftHand;
        [SerializeField] Transform _rightHand;
        [SerializeField] Renderer[] _hiddenForOwner;

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            if (IsOwner)
                foreach (var renderer in _hiddenForOwner)
                    renderer.enabled = false;
        }

        public override void OnNetworkDespawn() => All.Remove(this);

        void Update()
        {
            if (!IsOwner || !LocalRig.IsSet) return;
            transform.SetPositionAndRotation(LocalRig.Root.position, LocalRig.Root.rotation);
            _head.SetPositionAndRotation(LocalRig.Head.position, LocalRig.Head.rotation);
            _leftHand.SetPositionAndRotation(LocalRig.LeftHand.position, LocalRig.LeftHand.rotation);
            _rightHand.SetPositionAndRotation(LocalRig.RightHand.position, LocalRig.RightHand.rotation);
        }

        public static VRNetAvatar FindByOwner(ulong clientId)
            => All.Find(a => a.OwnerClientId == clientId);
    }
}
```

- [ ] **Step 4: Verify compile** — refresh; no console errors; 17 tests still pass.

- [ ] **Step 5: Commit**

```bash
git add "Packages/com.dsta.vrnet"
git commit -m "feat: avatar layer with LocalRig registration and owner-authoritative sync"
```

---

### Task 9: VRNetSession + ReconnectController

**Files:**
- Create: `Packages/com.dsta.vrnet/Runtime/Core/VRNetSession.cs`
- Create: `Packages/com.dsta.vrnet/Runtime/Core/ReconnectController.cs`

- [ ] **Step 1: Implement `VRNetSession.cs`**

```csharp
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

        public event Action<string> OnStatusChanged;
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action OnDiscoveryTimeout;
        public event Action OnPlayersChanged;

        DiscoveryBroadcaster _broadcaster;
        DiscoveryListener _listener;
        ReconnectController _reconnect;
        bool _callbacksHooked;

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
            _reconnect = gameObject.AddComponent<ReconnectController>();
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
            nm.StartServer();
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

        public void StartClientAuto() => StartCoroutine(ClientAutoRoutine());

        IEnumerator ClientAutoRoutine()
        {
            var nm = NetworkManager.Singleton;
            if (nm.IsClient || nm.IsServer) nm.Shutdown();
            while (nm.ShutdownInProgress) yield return null;

            Status($"Searching for {_config.teamId} server...");
            IPAddress foundAddress = null;
            DiscoveryMessage foundMessage = default;
            Action<IPAddress, DiscoveryMessage> handler = (address, message) =>
            {
                foundAddress = address;
                foundMessage = message;
            };
            _listener.OnServerFound += handler;
            _listener.StartListening(_config.discoveryPort, _config.teamId);

            float deadline = Time.realtimeSinceStartup + _config.discoveryTimeoutSeconds;
            while (foundAddress == null && Time.realtimeSinceStartup < deadline)
                yield return null;

            _listener.OnServerFound -= handler;
            _listener.StopListening();

            if (foundAddress == null)
            {
                Status("No server found. Check Wi-Fi/team ID, retry, or enter IP manually.");
                OnDiscoveryTimeout?.Invoke();
                yield break;
            }
            StartClient(foundAddress.ToString(), foundMessage.port);
        }

        public void StartClient(string address, int port = -1)
        {
            var nm = NetworkManager.Singleton;
            HookCallbacks(nm);
            ApplyTransportSettings(nm, listenAll: false,
                address: address, port: port > 0 ? port : _config.serverPort);
            nm.NetworkConfig.ConnectionData = new ConnectionPayload
            {
                playerGuid = PlayerIdentity.GetOrCreate(),
                playerName = PlayerName,
                teamId = _config.teamId,
                appVersion = _config.appVersion
            }.ToBytes();
            nm.StartClient();
            Status($"Connecting to {address}...");
        }

        public void StopSession()
        {
            _reconnect.Enabled = false;
            _broadcaster.StopBroadcasting();
            _listener.StopListening();
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
```

- [ ] **Step 2: Implement `ReconnectController.cs`**

```csharp
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace DSTA.VRNet.Core
{
    /// <summary>
    /// Client-side resilience: after a successful connection, any disconnect or failed
    /// re-discovery triggers another StartClientAuto with exponential backoff. Enabled
    /// on first successful connect; disabled by VRNetSession.StopSession().
    /// </summary>
    public class ReconnectController : MonoBehaviour
    {
        public bool Enabled;

        VRNetSession _session;
        float _backoffSeconds = 1f;
        Coroutine _pending;

        void Start()
        {
            _session = GetComponent<VRNetSession>();
            _session.OnConnected += () =>
            {
                Enabled = true;
                _backoffSeconds = 1f;
            };
            _session.OnDisconnected += ScheduleRetry;
            _session.OnDiscoveryTimeout += ScheduleRetry;
        }

        void ScheduleRetry()
        {
            if (!Enabled || _pending != null) return;
            _pending = StartCoroutine(Retry());
        }

        IEnumerator Retry()
        {
            Debug.Log($"[VRNet] Reconnecting in {_backoffSeconds:0.#}s...");
            yield return new WaitForSeconds(_backoffSeconds);
            _backoffSeconds = Mathf.Min(_backoffSeconds * 2f, _session.Config.reconnectMaxBackoffSeconds);
            _pending = null;
            if (Enabled) _session.StartClientAuto();
        }
    }
}
```

- [ ] **Step 3: Verify compile** — refresh; no console errors; 17 tests still pass.

- [ ] **Step 4: Commit**

```bash
git add "Packages/com.dsta.vrnet"
git commit -m "feat: VRNetSession facade and client auto-reconnect"
```

---

### Task 10: Demo scripts

**Files:**
- Create: `Assets/Scripts/Demo/DemoRigBinder.cs`
- Create: `Assets/Scripts/Demo/DemoFakeRig.cs`
- Create: `Assets/Scripts/Demo/DemoClientUI.cs`
- Create: `Assets/Scripts/Demo/DemoServerUI.cs`

- [ ] **Step 1: Implement `DemoRigBinder.cs`** (attach to XR Origin in ClientDemoScene)

```csharp
using DSTA.VRNet.Core;
using UnityEngine;

/// <summary>Reference integration: hands the project's XR rig to the VRNet library.</summary>
public class DemoRigBinder : MonoBehaviour
{
    [SerializeField] Transform _root;      // XR Origin
    [SerializeField] Transform _head;      // Main Camera
    [SerializeField] Transform _leftHand;  // Left Controller
    [SerializeField] Transform _rightHand; // Right Controller

    void Start() => VRNetSession.SetLocalRig(_root, _head, _leftHand, _rightHand);
}
```

- [ ] **Step 2: Implement `DemoFakeRig.cs`** (for desktop/Multiplayer Play Mode testing — lives on a GO that is DISABLED by default)

```csharp
using DSTA.VRNet.Core;
using UnityEngine;

/// <summary>
/// Fake XR rig for testing without a headset: creates rig transforms and orbits the
/// head so avatar sync is visible. Enable this GameObject only for desktop tests;
/// keep it disabled when DemoRigBinder provides the real rig.
/// </summary>
public class DemoFakeRig : MonoBehaviour
{
    Transform _root, _head, _left, _right;

    void Start()
    {
        _root = new GameObject("FakeRoot").transform;
        _root.SetParent(transform, false);
        _head = new GameObject("FakeHead").transform;
        _head.SetParent(_root, false);
        _left = new GameObject("FakeLeftHand").transform;
        _left.SetParent(_root, false);
        _right = new GameObject("FakeRightHand").transform;
        _right.SetParent(_root, false);
        VRNetSession.SetLocalRig(_root, _head, _left, _right);
    }

    void Update()
    {
        float t = Time.time;
        _head.localPosition = new Vector3(Mathf.Sin(t) * 0.5f, 1.6f, Mathf.Cos(t) * 0.5f);
        _head.localRotation = Quaternion.Euler(0f, t * 40f, 0f);
        _left.localPosition = _head.localPosition + new Vector3(-0.3f, -0.4f, 0.2f);
        _right.localPosition = _head.localPosition + new Vector3(0.3f, -0.4f, 0.2f);
    }
}
```

- [ ] **Step 3: Implement `DemoClientUI.cs`**

```csharp
using DSTA.VRNet.Core;
using TMPro;
using UnityEngine;

/// <summary>Headset connect UI: auto-join, manual IP fallback, live status line.</summary>
public class DemoClientUI : MonoBehaviour
{
    [SerializeField] VRNetSession _session;
    [SerializeField] TMP_Text _statusText;
    [SerializeField] TMP_InputField _manualIpField;
    [SerializeField] GameObject _connectPanel;

    void Start()
    {
        _session.OnStatusChanged += status => _statusText.text = status;
        _session.OnConnected += () => _connectPanel.SetActive(false);
        _session.OnDisconnected += () => _connectPanel.SetActive(true);
    }

    public void OnJoinAutoClicked() => _session.StartClientAuto();
    public void OnJoinManualClicked() => _session.StartClient(_manualIpField.text);
}
```

- [ ] **Step 4: Implement `DemoServerUI.cs`**

```csharp
using System.Text;
using DSTA.VRNet.Core;
using TMPro;
using UnityEngine;

/// <summary>Tablet UI: set team ID, start the server, watch the roster.</summary>
public class DemoServerUI : MonoBehaviour
{
    [SerializeField] VRNetSession _session;
    [SerializeField] TMP_Text _statusText;
    [SerializeField] TMP_Text _playerListText;
    [SerializeField] TMP_InputField _teamIdField;
    [SerializeField] GameObject _startPanel;

    void Start()
    {
        _teamIdField.text = _session.Config.teamId;
        _session.OnStatusChanged += status =>
        {
            _statusText.text = status;
            RefreshPlayerList();
        };
        _session.OnPlayersChanged += RefreshPlayerList;
    }

    public void OnStartServerClicked()
    {
        if (!string.IsNullOrWhiteSpace(_teamIdField.text))
            _session.Config.teamId = _teamIdField.text.Trim();
        _session.StartServer();
        _startPanel.SetActive(false);
    }

    void RefreshPlayerList()
    {
        var builder = new StringBuilder("Players:\n");
        foreach (var player in _session.Registry.AllPlayers)
            builder.AppendLine($"  {player.PlayerName}  {(player.Connected ? "connected" : "DROPPED")}");
        _playerListText.text = builder.ToString();
    }
}
```

- [ ] **Step 5: Verify compile** — refresh; no console errors.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts
git commit -m "feat: demo client/server UI and rig binding scripts"
```

---

### Task 11: Scene and prefab setup (Unity editor work)

**Files:**
- Create (in editor): `Assets/Settings/VRNetConfig.asset`
- Create (in editor): `Assets/Prefabs/VRNetAvatar.prefab`, `Assets/Prefabs/VRNetCore.prefab`
- Modify (in editor): `Assets/Scenes/ClientDemoScene.unity`, `Assets/Scenes/ServerDemoScene.unity`

Do these steps in the Unity editor (directly or driving it via unity-mcp). Save the scene after each block.

- [ ] **Step 1: Create the config asset**

Project window → `Assets/Settings` → right-click → Create → VRNet → Config. Name it `VRNetConfig`. Leave defaults (`TEAM-A`, ports 7777/47777).

- [ ] **Step 2: Create the avatar prefab**

1. In any scene, create an empty GameObject `VRNetAvatar`.
2. Add components: `NetworkObject` (check **Dont Destroy With Owner** — this is what lets the avatar freeze in place during the reconnect grace period instead of vanishing), `VRNetAvatar`, `OwnerNetworkTransform`.
3. Create children with primitive visuals:
   - `Head` — cube, scale (0.25, 0.25, 0.25), add `OwnerNetworkTransform`, remove its BoxCollider.
   - `LeftHand` — sphere, scale (0.1, 0.1, 0.1), add `OwnerNetworkTransform`, remove its SphereCollider.
   - `RightHand` — same as LeftHand.
4. On the root `VRNetAvatar` component, assign `_head`, `_leftHand`, `_rightHand` to those children and `_hiddenForOwner` to the three child Renderers.
5. Drag to `Assets/Prefabs/` to create the prefab; delete the scene instance.

- [ ] **Step 3: Create the core prefab (shared by both scenes)**

1. Create empty GameObject `VRNetCore`.
2. Add `NetworkManager`; on it set Player Prefab = `VRNetAvatar` prefab. `UnityTransport` is added automatically as "Select transport…" → Unity Transport if not present.
3. Add `VRNetSession`; assign the `VRNetConfig` asset. (`DiscoveryBroadcaster`/`Listener`/`ReconnectController` self-attach at runtime.)
4. Drag to `Assets/Prefabs/` as `VRNetCore.prefab`; delete the scene instance.

- [ ] **Step 4: Wire ClientDemoScene**

1. Open `Assets/Scenes/ClientDemoScene.unity`. Add the `VRNetCore` prefab.
2. On the XR Origin: add `DemoRigBinder`; assign root = XR Origin, head = Main Camera, leftHand/rightHand = the controller transforms.
3. Add a disabled GameObject `FakeRig` with `DemoFakeRig` (for desktop testing).
4. Add a world-space Canvas `ConnectUI` (~1m in front of spawn): TMP status text, TMP input field for manual IP, buttons "Join" → `DemoClientUI.OnJoinAutoClicked` and "Join IP" → `DemoClientUI.OnJoinManualClicked`. Add `DemoClientUI` to the canvas, assign all refs (`_session` = VRNetCore's VRNetSession, `_connectPanel` = the panel holding the buttons).
5. Save the scene.

- [ ] **Step 5: Wire ServerDemoScene**

1. Open `Assets/Scenes/ServerDemoScene.unity`. Add the `VRNetCore` prefab.
2. Add a screen-space Canvas: TMP input field (team ID), button "Start Server" → `DemoServerUI.OnStartServerClicked`, TMP status text, TMP player-list text. Add `DemoServerUI`, assign all refs; `_startPanel` = panel holding input+button.
3. Add a simple flythrough or fixed camera looking at the origin (this scene runs on the tablet, no XR).
4. Save the scene.

- [ ] **Step 6: Add both scenes to Build Settings** (File → Build Profiles → Scene List): ClientDemoScene index 0, ServerDemoScene index 1.

- [ ] **Step 7: Commit**

```bash
git add Assets
git commit -m "feat: demo scenes wired with VRNet core, avatar prefab, connect UIs"
```

---

### Task 12: Integration verification with Multiplayer Play Mode

No new files — this task verifies everything works together. Uses Multiplayer Play Mode (MPPM) so one editor hosts the server and virtual players are clients.

- [ ] **Step 1: Configure MPPM** — Window → Multiplayer → Multiplayer Play Mode. Enable Virtual Player 1 and 2.

- [ ] **Step 2: Loopback discovery + join test**

1. Main editor: open `ServerDemoScene`, enter Play, click **Start Server**. Status shows "Server ... up for TEAM-A".
2. Virtual players: set them to `ClientDemoScene` (or switch scene before play), **enable the FakeRig GameObject** in their instances, press their Join button.
3. Expected: both virtual players connect without typing an IP; server roster lists both device names as "connected"; two avatars with orbiting heads are visible in the server scene view.

- [ ] **Step 3: Wrong-team rejection test**

1. On one virtual player, temporarily set a different team ID (edit `VRNetConfig.asset` teamId to `TEAM-B` in that player's inspector at runtime — or change the server's team field before starting).
2. Expected: client discovery times out (filtered) — status "No server found..."; if connecting via manual IP instead, connection is rejected with reason containing "Wrong team".
3. Restore team IDs.

- [ ] **Step 4: Reconnect test**

1. With both clients connected, stop (exit play mode on) one virtual player mid-session, wait 3s, start it and Join again.
2. Expected: server status shows "... lost connection (grace 30s)" then "... rejoined"; roster never duplicates the player; the avatar reappears and animates.

- [ ] **Step 5: Fix anything that failed, then commit any fixes**

```bash
git add -A
git commit -m "fix: integration issues found in multiplayer playmode testing"
```

(Skip the commit if nothing changed.)

---

### Task 13: Build menu, package README, push

**Files:**
- Create: `Assets/Scripts/Demo/Editor/BuildMenu.cs`
- Create: `Packages/com.dsta.vrnet/README.md`

- [ ] **Step 1: Implement `BuildMenu.cs`**

```csharp
using UnityEditor;
using UnityEngine;

/// <summary>One-click Android builds: same project, different entry scene per device.</summary>
public static class BuildMenu
{
    const string ClientScene = "Assets/Scenes/ClientDemoScene.unity";
    const string ServerScene = "Assets/Scenes/ServerDemoScene.unity";

    [MenuItem("VRNet/Build Headset Client APK")]
    public static void BuildClient() => Build(ClientScene, "Builds/VRNet-HeadsetClient.apk");

    [MenuItem("VRNet/Build Tablet Server APK")]
    public static void BuildServer() => Build(ServerScene, "Builds/VRNet-TabletServer.apk");

    static void Build(string scene, string outputPath)
    {
        var report = BuildPipeline.BuildPlayer(new[] { scene }, outputPath, BuildTarget.Android, BuildOptions.None);
        Debug.Log($"[VRNet] Build {report.summary.result}: {outputPath} ({report.summary.totalSize / (1024 * 1024)} MB)");
    }
}
```

- [ ] **Step 2: Write `Packages/com.dsta.vrnet/README.md`**

```markdown
# VRNet Training Network

Multiplayer networking for VR training: a dedicated Android-tablet server per team,
team-scoped LAN discovery, owner-authoritative avatar sync, and automatic reconnect.
Built on Unity Netcode for GameObjects 2.x.

## Install (other projects)

Package Manager → Add package from git URL:

    https://github.com/LDRSG/VRTrainingSystem.git?path=/Packages/com.dsta.vrnet#v0.1.0

## Integrate (5 steps)

1. Add a GameObject with `NetworkManager` (+ Unity Transport) and `VRNetSession`.
2. Create a config: Assets → Create → VRNet → Config; set your team ID; assign it to `VRNetSession`.
3. Create an avatar prefab: root with `NetworkObject` (Dont Destroy With Owner ON),
   `VRNetAvatar`, `OwnerNetworkTransform`; children Head/LeftHand/RightHand each with
   `OwnerNetworkTransform`. Set it as the NetworkManager's Player Prefab.
4. Register your XR rig once at startup:
   `VRNetSession.SetLocalRig(xrOrigin, mainCamera, leftController, rightController);`
5. Tablet calls `VRNetSession.Instance.StartServer()`; headsets call `StartClientAuto()`.

## Deployment notes (Wi-Fi)

Give each team its own 5 GHz SSID on a non-overlapping channel. The library tolerates
drops (10s timeout, auto-reconnect, identity-preserving rejoin) but cannot remove
radio interference — plan channels per room.
```

- [ ] **Step 3: Verify compile** — refresh; menu items VRNet → Build … appear.

- [ ] **Step 4: Run full EditMode suite one last time** — expected: **17 pass, 0 fail**.

- [ ] **Step 5: Commit and push**

```bash
git add -A
git commit -m "feat: build menu and package README"
git push origin develop
```

---

## Out of scope for this plan

- Scenario variables/commands → Plan 2 (`ScenarioSync`).
- Recording and tablet replay → Plan 3 (`.vrrec` recorder + viewer).
- On-hardware test pass (Focus Vision + real tablet) — do after Plans 1-2 land; the MPPM checklist in Task 12 is the software-level gate.
