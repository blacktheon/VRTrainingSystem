# Quest client and tablet instructor demo

The user has completed the Quest XR environment and asked to continue the remaining jobs. Preserve the installed Meta packages, rig, hand/controller interaction, and locomotion setup. Work in the existing feature/MetaQuestDemo checkout used by the open Unity Editor.

## Outcome

ClientDemoScene registers the existing Quest rig with the reusable com.dsta.vrnet package. Editor clients and Quest builds discover or directly connect to a tablet server. Connected clients and the tablet see each trainee's head and two hands. ServerDemoScene remains a flat Android instructor application with a roster, overview/follow camera, and day/night button.

## Boundaries

- VRNet remains independent of Meta SDK and demo types. Existing transform registration is the integration contract.
- Retain the user's Quest environment and sample geometry. Remove only broken obsolete XR scene components where necessary.
- Keep the two entry scenes separate, disable NGO scene management on the demo NetworkManager, and share identical dynamic network prefabs.
- Use a demo-owned, dynamically spawned server-authoritative NetworkVariable<bool> for night state. Apply initial state on spawn and changes thereafter, including late join/rejoin. A new server session begins in day mode. No generic scenario framework or finger-joint networking is required.
- Tablet roster uses existing persistent player identity and marks disconnects. Camera defaults to a third-person overview with touch/mouse orbit and zoom; follow selection falls back safely when the selected avatar disappears.
- Build menu snapshots and restores project settings. Tablet builds disable Android XR loaders and startup and remove Quest-only requirements from the generated Android manifest. Client builds retain the user's Quest settings. Distinct application IDs permit both APKs to coexist.
- Sessions use a runtime copy of configuration so editor testing does not rewrite the config asset. Stop/restart cleans up callbacks and rig references appropriately.

## Acceptance

1. Existing package tests pass; relevant session isolation and network state regressions are covered.
2. Client scene has valid root, center-eye and two hand registrations; Quest rig remains intact.
3. Separate-scene clients connect, avatars replicate to server and peers, and disconnect/rejoin updates roster without duplicates.
4. Tablet controls start/stop server, show connections, select overview/follow view, and toggle lighting for all clients. Client code cannot author night state.
5. Late join/reconnect receives current night state; restart resets to day.
6. Both builds compile, tablet manifest has no required VR hardware or VR launch mode, and settings are restored after build success/failure.
7. Report device-only checks separately from automated/editor evidence.
