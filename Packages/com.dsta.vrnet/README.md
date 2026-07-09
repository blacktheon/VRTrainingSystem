# VRNet Training Network

Multiplayer networking for VR training: a dedicated Android-tablet server per team,
team-scoped LAN discovery, owner-authoritative avatar sync, and automatic reconnect.
Built on Unity Netcode for GameObjects 2.x.

## Install (other projects)

Package Manager → Add package from git URL:

    https://github.com/LDRSG/VRTrainingSystem.git?path=/Packages/com.dsta.vrnet#v0.1.0

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
- Discovery and approval are team-scoped: a headset only finds and joins the server
  whose `teamId` matches its config. App version must match too.
- The demo tablet UI writes the team ID typed at runtime into the shared `VRNetConfig`
  asset instance; in the editor with domain reload disabled this persists between play
  sessions. Deployment builds each carry their own config copy, so this only affects
  editor testing.

## Deployment notes (Wi-Fi)

Give each team its own 5 GHz SSID on a non-overlapping channel. The library tolerates
drops (10s timeout, auto-reconnect, identity-preserving rejoin) but cannot remove
radio interference — plan channels per room.
