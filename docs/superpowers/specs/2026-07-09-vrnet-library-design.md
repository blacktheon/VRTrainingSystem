# VRNet Library — Design Spec

**Date:** 2026-07-09
**Project:** VRTrainingSystem (Unity 6000.3.19f1, URP, Android)
**Status:** Approved by user

## Purpose

A reusable multiplayer networking library for VR training projects, developed inside this
playground project and distributable to other Unity projects. It provides: player presence
(trainees see and cooperate with each other in VR), instructor scenario control from an
Android tablet, and session recording with replay on the tablet.

The playground project doubles as the reference integration and demo.

## Context and constraints

- **Hardware:** VIVE Focus Vision headsets (Android, via VIVE OpenXR plugin) + one Android
  tablet per team.
- **Scale:** 4-6 headsets + 1 tablet per team; up to 6 teams training concurrently in the
  same building. Teams are fully independent — no cross-team communication.
- **Network:** each team may use its own Wi-Fi router. Signal interference between teams is
  expected; the system must tolerate Wi-Fi drops gracefully ("stubborn").
- **Networking stack:** Unity Netcode for GameObjects (NGO) 2.x over UnityTransport (UDP),
  carrying over the owner-authoritative patterns from the previous VR Vehicle project.
- **Predecessor:** `L:\Unity\DSTA\VR Vehicle\Assets\Scripts\Multiplayer` — proven rig-sync
  and RPC patterns, but coupled to game-specific singletons. This library inverts that
  coupling (see Integration contract).

## Decisions (settled during brainstorming)

| Topic | Decision |
|---|---|
| Topology | Tablet = dedicated NGO server (no player avatar) + instructor UI |
| Multi-team | Each tablet is an isolated session; team-scoped discovery |
| Scenario control | Generic named-variable system; games register handlers |
| Replay | Server records; replay viewer on tablet only |
| Discovery | LAN UDP broadcast with manual IP fallback |
| Distribution | Embedded UPM package in this repo, installed by consumers via git URL |
| Unity/stack | Unity 6 + NGO 2.x + VIVE OpenXR (not legacy Wave SDK) |

## Repo layout

```
VRTrainingSystem/                          (this repo, Unity 6 playground project)
├── Packages/
│   └── com.dsta.vrnet/                    ← THE LIBRARY (embedded UPM package)
│       ├── package.json                   (name, version, NGO 2.x dependency)
│       ├── Runtime/
│       │   ├── Core/                      session lifecycle, roles, config, PlayerGuid
│       │   ├── Discovery/                 UDP broadcast/response, team filtering
│       │   ├── Avatar/                    rig sync (root/head/hands), avatar registry
│       │   ├── Scenario/                  scenario variables, command channel
│       │   └── Recording/                 recorder, .vrrec format, playback engine
│       ├── Editor/                        inspectors, setup helpers
│       └── Samples~/                      minimal integration example
└── Assets/                                ← THE DEMO (not shipped to consumers)
    ├── Scenes/ClientDemoScene.unity       VR player scene (XRI + VIVE OpenXR)
    ├── Scenes/ServerDemoScene.unity       server + instructor UI + replay viewer
    └── Scripts/Demo/                      time-of-day/weather handlers, tablet UI
```

Consumers install with:

```
https://github.com/LDRSG/VRTrainingSystem.git?path=/Packages/com.dsta.vrnet#v0.1.0
```

Versioning via git tags. If the library matures, it can split into its own repo later;
consumers only change the URL.

One Unity project builds both APKs: headset client and tablet server are different scenes
selected at build time via a build menu ("Build Headset APK" / "Build Tablet APK").

## Integration contract (the decoupling rule)

The library never references game types. Games register themselves into the library:

- Rig source: game calls `VRNetSession.SetLocalRig(root, head, leftHand, rightHand)`;
  the avatar sync reads whatever was registered (replaces the old `VRRigReferences.instance`
  singleton dependency).
- Scenario reactions: game calls `ScenarioSync.Subscribe(name, handler)`.
- The demo project in `Assets/` is the reference implementation of this contract.

## Network topology & resilience

Per team: tablet runs NGO 2.x as dedicated server over UnityTransport (UDP); 4-6 headset
clients connect. Teams never interconnect.

**Team identity.** Each tablet is configured with a team ID string (e.g. `TEAM-A`) in its
UI. Discovery broadcasts carry `{teamId, serverName, ip, port, appVersion}`. Headsets are
configured with their team ID once and auto-join only their team's server. Server-side
connection approval re-checks team ID + app version and rejects mismatches with a clear
reason.

**Resilience layers:**

1. **Generous timeouts** — UTP heartbeat and disconnect timeout tuned for Wi-Fi blips
   (~10s rather than 2-3s defaults), so brief signal drops don't disconnect at all.
2. **Auto-reconnect** — each headset generates a persistent `PlayerGuid` (stored on
   device). On disconnect the client re-runs discovery and rejoins automatically. The
   server re-associates the returning GUID with the same player identity (name, log
   identity) instead of treating it as a new player.
3. **State recovery for free** — all scenario state lives in NetworkVariables, so a
   rejoining client receives complete current state automatically on spawn. No custom
   resync code.
4. **Server never depends on clients** — recording and scenario state continue while any
   client is down; a dropped player's avatar freezes for a grace period, then despawns.

**Rig sync:** owner-authoritative (each headset owns its avatar), NetworkTransform at
~20Hz with interpolation. Root/head/left hand/right hand per player.

**Deployment guidance (docs, not code):** per-team 5GHz SSIDs on non-overlapping channels.
Software tolerates interference; only radio planning removes it.

## Scenario variables & commands

A `ScenarioSync` component on a server-owned NetworkObject holds a networked dictionary of
named variables (float/int/bool/string), e.g. `"timeOfDay"` → `0.75`, `"weather"` → `"rain"`.

- Server (tablet): `ScenarioSync.Set("weather", "rain")`
- Client (any game): `ScenarioSync.Subscribe("weather", OnWeatherChanged)` — fires on every
  change and once on join/rejoin with the current value.
- One-shot, non-persistent events use a separate `SendCommand(name, payload, target)` RPC
  channel (all clients or one client).

Demo implements time-of-day (sun rotation) and weather (rain particles) handlers.

## Recording & replay

**Recording (library, runs on server):** samples every player's rig (root/head/hands,
position + rotation) at 20Hz, plus scenario variable changes, commands, and join/leave
events. Written incrementally to a binary `.vrrec` file in
`persistentDataPath/Recordings/` on the tablet — a crash loses seconds, not the session.
File header: format version, team ID, start timestamp.

**Replay (tablet only):** the playback engine (file reading, time-stepping, interpolation)
lives in the library; the viewer UI lives in the demo server scene — load a session, spawn
ghost avatars, free-look camera, timeline with play/pause/scrub/speed. Scenario events
re-fire through the same `ScenarioSync.Subscribe` handlers during replay so the replayed
world matches what trainees saw.

## Error handling

- Discovery timeout → manual IP entry fallback UI.
- Connection failure → retry with backoff, status surfaced in-headset.
- Recording I/O failure → log and keep the session alive; never interrupt training over a
  log file.
- Client timeout on server → avatar freezes for a grace period, then despawns; identity
  retained for rejoin.

## Testing

- **Multiplayer Play Mode** (Unity 6 package): server + multiple virtual clients in one
  editor for whole-team session tests without hardware.
- **EditMode unit tests:** `.vrrec` encode/decode round-trip; discovery message parsing.
- **On-hardware checklist:** pull a headset's Wi-Fi mid-session → verify auto-rejoin +
  state recovery; kill the tablet app → verify recording file survives; two servers with
  different team IDs on one LAN → verify clients join only their team.

## Packages to add at implementation start

- `com.unity.netcode.gameobjects` (2.x)
- `com.unity.multiplayer.playmode` (testing)

## Out of scope (YAGNI)

- Cross-team monitoring / master station (possible phase 2).
- Replay in headsets or editor (format doesn't preclude it; viewer is tablet-only for now).
- Voice chat, internet play, relay servers — LAN only.
- Interest management / bandwidth optimization beyond 20Hz sampling (6 players is small).
