# VRNet Training Network

Multiplayer networking for VR training: a dedicated Android-tablet server per team,
team-scoped LAN discovery, owner-authoritative avatar sync, and automatic reconnect.
Built on Unity Netcode for GameObjects 2.x.

## Install (other projects)

Package Manager → Add package from git URL:

    https://github.com/blacktheon/VRTrainingSystem.git?path=/Packages/com.dsta.vrnet#master

The branch suffix explicitly selects `master`, regardless of the repository's default
branch. Unity records the resolved commit in `Packages/packages-lock.json`; it does
not automatically pull new commits every time the Editor opens. After an upstream
push, use Package Manager's Git update action (or add the same Git URL again), verify
the resolved revision, and commit the consumer's updated lockfile.

## Integrate (5 steps)

1. Add a GameObject with `NetworkManager` (+ Unity Transport) and `VRNetSession`
   (or copy the demo's `VRNetCore` prefab pattern).
2. Create a config: Assets → Create → VRNet → Config; set your team ID; assign it to `VRNetSession`.
3. Create an avatar prefab: root with `NetworkObject` (Dont Destroy With Owner ON),
   `VRNetAvatar`, `OwnerNetworkTransform`; children Head/LeftHand/RightHand each with
   `OwnerNetworkTransform`. Set it as the NetworkManager's Player Prefab.
4. Register your XR rig once at startup:
   `VRNetSession.SetLocalRig(xrOrigin, mainCamera, leftController, rightController);`
5. Tablet calls `VRNetSession.Instance.StartServer()`; headsets call `StartClientAuto()`.

## Behavior notes

- Clients auto-reconnect with backoff after the first successful connection; a dropped
  player's avatar freezes for a grace period (config) and their identity survives rejoin.
  Direct-IP connections retain their address and port during retries; auto-join repeats
  discovery. `StopSession()` cancels retries and raises `OnStopped` after cleanup.
- A direct connection that fails before NGO starts raises `OnClientStartFailed`.
  ReconnectController handles this alongside disconnects after a successful join.
  UI/adapters that retry the initial connection should subscribe to this event as
  well as `OnDisconnected` and `OnDiscoveryTimeout`; stop scheduling when the user
  intentionally stops the session. The sample client demonstrates this pattern.
- Discovery and approval are team-scoped: a headset only finds and joins the server
  whose `teamId` matches its config. App version must match too.
- `VRNetSession.Config` is a session-owned copy of the assigned `VRNetConfig`. The demo
  tablet's team ID changes do not modify the source asset, including in the Editor.

## Editor build settings

Build tools that snapshot settings files can restore their bytes through
`DSTA.VRNet.Editor.BuildSettingsFiles.Restore(path, originalBytes)`. It replaces the
file atomically, avoiding Windows errors from truncating memory-mapped files, and
removes its temporary file if replacement fails. Restore live Unity objects and
their dirty state separately, as demonstrated by the tablet demo build scope.

## Deployment notes (Wi-Fi)

Give each team its own 5 GHz SSID on a non-overlapping channel. The library tolerates
drops (10s timeout, auto-reconnect, identity-preserving rejoin) but cannot remove
radio interference — plan channels per room.
