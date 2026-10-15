# VRTrainingSystem — VRNet Package Playground

This repository is a **Unity playground project** for developing **VRNet Training Network**
(`com.dsta.vrnet`) — a reusable multiplayer networking package for VR training applications.
The package itself lives in [`Packages/com.dsta.vrnet/`](Packages/com.dsta.vrnet/) as an
embedded UPM package; everything under `Assets/` is the **reference integration and demo**,
and is never shipped to consumers.

VRNet provides, per training team:

- A **dedicated server on an Android tablet** (no player avatar) that instructors run.
- **Team-scoped LAN discovery** — headsets find and join *only* their team's server via UDP
  broadcast, with manual-IP fallback. Up to 6 teams can train in the same building without
  interfering with each other.
- **Owner-authoritative avatar sync** — root/head/hands replicated for every trainee.
- **"Stubborn Wi-Fi" resilience** — generous timeouts, automatic reconnect with backoff, and
  identity-preserving rejoin (a dropped trainee's avatar freezes, then the same identity
  returns instead of a duplicate player).

Built on **Unity Netcode for GameObjects 2.x** over UnityTransport (UDP). LAN only.

## Quest client + tablet sample

The sample now uses the Quest XR environment installed from QuestBaseTemplate. The
existing Meta Camera Rig is registered through `DemoRigBinder`; VRNet stays independent
of Meta SDK types. Local hand visuals come from that rig; remote avatars synchronize
the head and two hand transforms (not individual finger joints).

1. Build **VRNet → Build Tablet Server APK**, install it on an ARM64 Android tablet,
   and press **Start Server**. Keep tablet and clients on the same LAN and team ID.
2. Open `Assets/Scenes/ClientDemoScene.unity` in the Editor, connect the Quest through
   Quest Link with Meta set as the active OpenXR runtime, and press Play. The client
   auto-discovers the tablet and retries if the tablet starts later. For a fixed IP,
   set **ConnectUI → DemoClientUI → Server Address** before Play/build.
3. Connect a second client. The tablet roster shows connected/disconnected users.
   **Overview** frames the group; **Follow Next Trainee** cycles users. Drag the view
   to orbit and pinch (or mouse wheel) to zoom. The roster scrolls independently.
4. Press **Switch to Night / Switch to Day**. Lighting updates on all clients and the
   tablet. Late joiners and returning clients receive the current state. Stopping and
   starting a new session resets it to day.
5. Build **VRNet → Build Headset Client APK** for standalone Quest testing.

Use the VRNet build menus with Android selected. They produce separate APKs and app IDs
(`com.immrsiv.vrnet.server` / `com.immrsiv.vrnet.client`). Tablet builds disable XR and
remove headset-only manifest requirements; the original Quest settings are restored
after success or failure. Do not use a normal Quest-configured Build button for the tablet.

The demo disables NGO scene management because each role has a different entry scene.
`VRNetCore`, the avatar, and `DemoSessionState` use the same registered network prefabs
in both builds. Day/night is a small demo of persistent server-owned state, not the
generic scenario framework described in the roadmap.

The **EditMode** Test Runner includes package tests, manifest/build-setting regressions,
and `DemoNetworkIntegrationTests`, which temporarily enters Play Mode for real UDP
loopback clients. Save scenes first; the test restores the open scenes and XR startup
settings. Results are saved to `Logs/vrnet-all-results.xml`. Verify Quest tracking,
touch input, LAN discovery, and Wi-Fi recovery on physical devices before release.

Validated on 2026-10-16: **38 tests passed**, including two-client UDP loopback,
late join/reconnect, pose and lighting sync, build isolation, and camera framing.
Both APK build menus completed with zero build errors, and the packaged manifests
were inspected. Existing imported SDK/sample warnings remain. Physical-device
tracking, touch, and Wi-Fi tests are still required.

## Repository layout

```
VRTrainingSystem/
├── Packages/com.dsta.vrnet/          ← THE PACKAGE (this is the product)
│   ├── package.json                    name, version, NGO dependency
│   ├── Runtime/
│   │   ├── Core/                       VRNetSession, VRNetConfig, registry, identity,
│   │   │                               connection approval, reconnect controller
│   │   ├── Discovery/                  UDP broadcast/listen + message format
│   │   └── Avatar/                     VRNetAvatar, OwnerNetworkTransform, LocalRig
│   ├── Editor/                         editor-only helpers
│   └── Tests/Editor/                   EditMode unit tests
├── Assets/                           ← THE DEMO (reference integration, not shipped)
│   ├── Scenes/ClientDemoScene.unity    VR headset scene (Meta Quest)
│   ├── Scenes/ServerDemoScene.unity    tablet server scene (flat UI)
│   ├── Scenes/MppmTestScene.unity      in-editor integration test scene
│   ├── Prefabs/VRNetCore.prefab        NetworkManager + UnityTransport + VRNetSession
│   ├── Prefabs/VRNetAvatar.prefab      reference avatar prefab
│   ├── Scripts/Demo/                   demo UIs, rig binder, MPPM bootstrap, build menu
│   └── Settings/VRNetConfig.asset      the demo's config instance
└── docs/superpowers/                 design spec and implementation plans
```

## Requirements

| What | Version |
|---|---|
| Unity | **6000.3.19f1** (URP), with **Android Build Support** (SDK/NDK/OpenJDK) |
| Netcode for GameObjects | 2.4.0 (package dependency) |
| Headsets | Meta Quest (Android), or Quest Link for Editor testing |
| Server device | ARM64 Android tablet, Android 8.0/API 26 or later, OpenGL ES 3 |
| Testing without hardware | Multiplayer Play Mode package (already installed) |

---

# Part 1 — Using the package in your own project

## Install

Package Manager → **Add package from git URL**:

```
https://github.com/LDRSG/VRTrainingSystem.git?path=/Packages/com.dsta.vrnet#v0.1.0
```

Pin the version with the tag suffix (`#v0.1.0`). Upgrading later = change the tag.

## Core concepts

- **`VRNetSession`** — the facade for the whole library. One per scene, next to NGO's
  `NetworkManager`. Everything goes through it: start server/client, events, roster.
- **`VRNetConfig`** — a ScriptableObject holding all deployment tunables (team ID, ports,
  timeouts, grace periods). One asset per project/deployment.
- **`LocalRig`** — the decoupling seam. The package never references your game's types;
  instead your game hands its XR rig transforms to the package once at startup, and the
  avatar system mirrors whatever was registered.
- **`VRNetAvatar`** — sits on the player prefab. On the owning headset it copies the
  `LocalRig` pose every frame; `OwnerNetworkTransform` components replicate root/head/hands
  to everyone else. The owner's own renderers are hidden automatically.
- **`VRNetPlayerRegistry`** — server-side roster keyed by a persistent per-device
  `PlayerGuid`, so identities survive disconnects (`Connected` flips false, record remains).

## Integrate — 5 steps

**1. Session object.** Create a GameObject with `NetworkManager` + `Unity Transport` +
`VRNetSession` (or copy this repo's `VRNetCore` prefab pattern). Don't configure the
transport's address/port — `VRNetSession` applies everything from the config at start.

**2. Config.** `Assets → Create → VRNet → Config`. Set `teamId` and `appVersion`
(server rejects clients whose team ID *or* app version mismatch). Assign the asset to
`VRNetSession`.

**3. Avatar prefab.** Root object: `NetworkObject` (**Dont Destroy With Owner: ON** — the
grace-period freeze depends on it), `VRNetAvatar`, `OwnerNetworkTransform`. Children
`Head`, `LeftHand`, `RightHand`: each with `OwnerNetworkTransform`. Assign head/hand
transforms and the owner-hidden renderers on `VRNetAvatar`. Set the prefab as the
`NetworkManager`'s **Player Prefab**.

**4. Register your rig** once at startup (see [`DemoRigBinder.cs`](Assets/Scripts/Demo/DemoRigBinder.cs)):

```csharp
// root = XR Origin, head = Main Camera, hands = controller transforms
VRNetSession.SetLocalRig(xrOrigin, mainCamera, leftController, rightController);
```

**5. Start the session.**

```csharp
VRNetSession.Instance.StartServer();          // tablet
VRNetSession.Instance.StartClientAuto();      // headset: discover + join own team
VRNetSession.Instance.StartClient("10.0.0.5"); // headset: manual IP fallback
```

## Public API reference

### `VRNetSession` (MonoBehaviour, singleton via `Instance`)

| Member | Role |
|---|---|
| `StartServer()` | Start dedicated server + discovery broadcast + connection approval |
| `StartClientAuto()` | Listen for own team's broadcast (config timeout), then connect |
| `StartClient(address, port = -1)` | Connect directly; `-1` = config's `serverPort` |
| `StopSession()` | Tear everything down (also disables auto-reconnect) |
| `SetLocalRig(root, head, left, right)` | Register the XR rig (static) |
| `Config` | A session-owned runtime copy of the assigned `VRNetConfig` |
| `Registry` | Server-side roster (`ConnectedPlayers` / `AllPlayers`) |
| `PlayerName` | Defaults to device name; set before connecting to customize |
| `PlayerGuidOverride` | Leave null on devices. Only for multiple instances sharing one machine's PlayerPrefs (e.g. Multiplayer Play Mode) |

Events: `OnStatusChanged(string)` (human-readable status line — wire it to a UI text),
`OnConnected`, `OnDisconnected`, `OnDiscoveryTimeout`, `OnStopped` (explicit stop after
cleanup), `OnPlayersChanged` (roster changed; server side). Automatic reconnect retains
the selected direct IP/port, or repeats discovery when auto-join was selected.

### `VRNetConfig` fields

| Group | Fields |
|---|---|
| Identity | `teamId`, `serverName`, `appVersion` |
| Network | `serverPort` (7777), `discoveryPort` (47777), `discoveryIntervalSeconds`, `discoveryTimeoutSeconds` |
| Resilience | `disconnectTimeoutMs` (10 s — Wi-Fi blips shorter than this never disconnect at all), `connectTimeoutMs`, `maxConnectAttempts`, `avatarDespawnGraceSeconds` (30 s frozen-avatar window awaiting rejoin), `reconnectMaxBackoffSeconds` |

### Behavior you get for free

- **Auto-reconnect:** after the *first* successful connection, any disconnect or discovery
  timeout retries the selected connection method with exponential backoff (1 s → doubling → capped). Disabled
  by `StopSession()`.
- **Rejoin identity:** each device stores a persistent GUID. The server recognizes a
  returning GUID, despawns the stale frozen avatar, and re-associates the same
  `PlayerRecord` — the roster shows a rejoin, not a new player.
- **Rejection reasons:** mismatched team/version connections are rejected with a reason
  string, surfaced through `OnStatusChanged` on both ends.

---

# Part 2 — Developing the package (this repo)

The demo scenes are the package's test bench. The loop is: edit package code → verify in
editor (unit tests + Multiplayer Play Mode) → verify on hardware → tag a release.

## Edit

Package source is editable in place under `Packages/com.dsta.vrnet/` (embedded packages
compile like normal code; it appears in the Project window under "Packages"). Assemblies:
`DSTA.VRNet` (runtime), `DSTA.VRNet.Editor`, `DSTA.VRNet.Tests` (EditMode).

**The one architecture rule:** the package never references game/demo types. Anything the
package needs from the game comes in through a registration call (`SetLocalRig`, events).
Demo code depending on the package: fine. The reverse: never.

## Test — three levels

**1. Unit tests (seconds).** `Window → General → Test Runner → EditMode → Run All`.
Covers discovery message round-trip, connection validation, identity, and registry logic.

**2. Multiplayer Play Mode integration (minutes, no hardware).** Open
`Assets/Scenes/MppmTestScene.unity`, then `Window → Multiplayer → Multiplayer Play Mode`
and enable virtual players. [`MppmRoleBootstrap`](Assets/Scripts/Demo/MppmRoleBootstrap.cs)
assigns roles at runtime: the **main editor becomes the server**, every **virtual player
becomes a client**. Two MPPM player *tags* trigger scripted misbehavior:

| Tag on virtual player | Exercises |
|---|---|
| *(none)* | Normal join, avatar spawn, roster |
| `WRONGTEAM` | Joins with mismatched team ID → must be rejected with a reason |
| `DROPPER` | Self-disconnects after 12 s → must auto-reconnect and rejoin as the same identity |

Watch the Console for `[MPPMTEST]` lines (roster snapshots, discovery hits, events).
The `WRONGTEAM` client uses direct loopback to exercise approval rejection; discovery
would otherwise filter the mismatched server out. Runtime team changes affect only
the session's config copy, not `Assets/Settings/VRNetConfig.asset`.

**3. On-hardware (before any release).** See "Build & smoke test" below.

## Build & smoke test

One project builds both APKs — same Android target, different entry scene — via the
[`BuildMenu`](Assets/Scripts/Demo/Editor/BuildMenu.cs):

- **VRNet → Build Headset Client APK** → `Builds/VRNet-HeadsetClient.apk`
- **VRNet → Build Tablet Server APK** → `Builds/VRNet-TabletServer.apk`

Install with `adb install -r <apk>` (Quest needs Developer Mode and USB debugging
authorized). Then, with the tablet and headsets on the same Wi-Fi:

1. Tablet: start server (`TEAM-A`). Status line confirms; the app runs without XR.
2. Headset: automatic discovery connects; roster shows the player.
3. Second headset: both trainees see each other's head/hands moving smoothly.
4. **The resilience test:** kill one headset's Wi-Fi mid-session. Expect: roster flips to
   `DROPPED` in ~10 s, avatar freezes for others. Restore Wi-Fi: auto-reconnect, same
   identity back, avatar unfreezes. This is the core promise — always test it.
5. Team isolation: a second server with a different team ID on the same LAN must be
   invisible to `TEAM-A` headsets.
6. Set the client's **Server Address** in the Inspector to test direct-IP connection
   when broadcast is filtered but traffic between devices is allowed.

## Release a new version

1. Bump `version` in `Packages/com.dsta.vrnet/package.json`.
2. Decide whether the protocol changed: if old clients must not join new servers, also bump
   `appVersion` in consumers' `VRNetConfig` assets (approval rejects version mismatches) —
   and keep the demo's config + Player Settings `bundleVersion` in step.
3. Run all three test levels.
4. Commit, tag (`git tag v0.x.0`), push with tags. Consumers upgrade by changing the URL tag.

---

# Part 3 — Learning scenarios

Hands-on exercises, in order. Each teaches one concept; 1–4 need no hardware.

### Scenario 1 — "First contact" (10 min, editor only)

*Goal: see a full server + client session without writing any code.*

1. Open `MppmTestScene`, enable **one** virtual player in Multiplayer Play Mode, press Play.
2. In the main editor Console, find the server startup line, the discovery broadcast, the
   join, and the roster snapshot. In the virtual player's Console, find the discovery hit
   and `OnConnected`.
3. You now know the whole lifecycle: broadcast → discovery → approval → spawn → roster.

### Scenario 2 — "The bouncer" (15 min, editor only)

*Goal: understand team-scoped approval — why six teams can share a building.*

1. Tag a second virtual player `WRONGTEAM` and press Play.
2. Watch it get rejected, and find the rejection *reason* in both consoles.
3. Read [`ConnectionValidator.cs`](Packages/com.dsta.vrnet/Runtime/Core/ConnectionValidator.cs)
   (it's tiny) to see exactly what's checked: team ID and app version, carried in the
   connection payload.
4. Confirm the source config still says `TEAM-A`; the test changes only its runtime copy.

### Scenario 3 — "Pull the plug" (15 min, editor only)

*Goal: internalize the resilience model — the package's reason to exist.*

1. Tag a virtual player `DROPPER` and press Play. It self-disconnects after 12 s.
2. In the server console, watch the sequence: *lost connection (grace 30 s)* → backoff
   retries on the client → *rejoined* — same name, no duplicate roster entry.
3. Now open `Assets/Settings/VRNetConfig.asset` and set `avatarDespawnGraceSeconds` to `1`.
   Re-run: the avatar despawns before the rejoin. Understand which deployments want long
   grace (flaky venue Wi-Fi) vs short (fast trainee turnover). Restore `30`.

### Scenario 4 — "Integrate from zero" (45 min, editor only)

*Goal: perform the real consumer workflow — wire VRNet into a scene with no VRNet in it.*

1. Create a new scene in this project. No copying the demo scenes — build it up:
   NetworkManager + Unity Transport + `VRNetSession` on one GameObject.
2. Create your *own* `VRNetConfig` asset with `teamId: TEAM-TRAINEE`; assign it.
3. Build an avatar prefab from cubes/spheres following the 5-step guide above (root +
   head + two hands, `OwnerNetworkTransform` on all four, `NetworkObject` with *Dont
   Destroy With Owner* ON). Assign as Player Prefab.
4. Write a 10-line rig binder that calls `VRNetSession.SetLocalRig(...)` with any four
   transforms (see `DemoFakeRig` for a no-headset stand-in that moves them for you), and a
   trigger for `StartServer()` / `StartClientAuto()` (UI button, or an MPPM bootstrap).
5. Verify with one virtual player: two moving avatars, roster of one.
6. Checkpoint: your scene never referenced demo code, and package code never referenced
   yours. That boundary is the integration contract.

### Scenario 5 — "Ship it" (60 min, hardware: 1 tablet + 2 headsets)

*Goal: the full deployment path.*

1. Build both APKs from the build menu; install to devices.
2. Run the six-step smoke test from Part 2 — including the Wi-Fi pull. Time how long the
   rejoin takes; compare against `disconnectTimeoutMs` + backoff to check it matches your
   understanding.
3. Change the team ID **in the tablet UI at runtime** (the demo server UI writes it into
   the config before starting) and confirm headsets with the old team ID can no longer find
   it — that's the whole multi-team deployment story: config, not code.

### Scenario 6 — "Hostile venue" (30 min, hardware)

*Goal: practice the fallbacks you'll need on site.*

1. Use a test network that filters UDP broadcast while permitting traffic between
   devices. Auto-join now times out — read the status message trail.
2. Set **ConnectUI → DemoClientUI → Server Address** in the Inspector, then replay
   or rebuild the client. Full AP/client isolation must be disabled on the router;
   a direct IP cannot bypass a block on traffic between devices.
3. Read the Wi-Fi planning notes in the
   [package README](Packages/com.dsta.vrnet/README.md): one 5 GHz SSID per team on
   non-overlapping channels. The library survives interference; it cannot remove it.

---

## Roadmap (from the [design spec](docs/superpowers/specs/2026-07-09-vrnet-library-design.md))

- **Scenario control** — networked named variables (`ScenarioSync.Set("weather", "rain")`)
  with subscribe-on-join semantics, plus one-shot command RPCs. Instructor drives the
  training scenario from the tablet.
- **Recording & replay** — server records rig poses + scenario events to `.vrrec`;
  tablet-only replay viewer with ghost avatars and timeline scrubbing.
- Out of scope by design: cross-team communication, internet play/relays, voice chat,
  interest management.

## Documents

- [Package README](Packages/com.dsta.vrnet/README.md) — consumer-facing quick reference
- [Design spec](docs/superpowers/specs/2026-07-09-vrnet-library-design.md) — decisions and rationale
- [Implementation plan](docs/superpowers/plans/2026-07-09-vrnet-core-networking.md) — how v0.1.0 was built
