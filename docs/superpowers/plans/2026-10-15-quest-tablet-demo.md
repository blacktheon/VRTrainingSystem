# Quest and Tablet Demo Implementation Plan

> **For agentic workers:** Use the executing-plans workflow, with parallel implementation of independent files and one final review.

**Goal:** Finish the existing Quest sample's client networking, tablet monitoring, and synchronized day/night control.

**Architecture:** Keep VRNet platform independent. Demo scripts bind the user's Quest rig, spawn shared state, and provide local presentation. Build tooling isolates flat-tablet settings from the existing Quest configuration.

**Tech Stack:** Unity 6000.3.19f1, NGO 2.4.0, UnityTransport, Meta XR, URP, Input System, UGUI/TMP.

**Spec:** ../specs/2026-10-15-quest-tablet-demo-design.md

## Global constraints

- Use C:/Work/Unity/VRTrainningSystem and the current feature/MetaQuestDemo branch.
- Preserve the user's Quest packages, rig, and locomotion.
- Only the coordinating agent operates the connected Unity Editor or edits scene/prefab assets.
- Changes remain uncommitted for user review; do not publish or push.

## Review focus

- Different scene entry points must not trigger NGO scene loading on clients.
- Failed server startup must leave retry controls usable.
- Late join/rejoin must receive night state without replaying a button click.
- Server stop/restart must not retain a previous roster/state or duplicate event handlers.
- Tablet build failure must restore Quest settings and original manifest.

## Tasks

- [x] 1. Session lifecycle and pose API: runtime config copy, callback cleanup, server/client state access, read-only avatar pose getters. Add behavior tests for config isolation and teardown. Files: package Core/VRNetSession.cs, Avatar/VRNetAvatar.cs, Tests/Editor.
- [x] 2. Tablet presentation: DemoServerUI.cs and new DemoSpectatorCamera.cs. Preserve serialized UI references; expose OnToggleDayNightClicked, OnStopServerClicked, OnOverviewClicked, OnFollowNextClicked. Consume DemoSessionState.Instance, IsNight, ToggleDayNight(); VRNetAvatar.Head/LeftHand/RightHand. Test via real scene controls and disconnect fallback.
- [x] 3. Android build isolation: BuildMenu.cs and a manifest processor. Snapshot/restore settings with try/finally; strip VR requirements only in generated tablet output. Test the manifest transformation using realistic XML and build both entry scenes.
- [x] 4. Shared state and scene integration: DemoSessionState, DemoSessionStateSpawner, DemoLightingController; bind existing Quest transforms via DemoRigBinder; auto-join with manual address fallback; wire server UI and shared network prefabs in Unity. Add actual integration coverage for initial/change/rejoin state and restart.
- [x] 5. Verification and docs: run package and demo tests, inspect both scenes for missing scripts/references, exercise dedicated server and clients over UDP loopback, compile Android APKs, inspect manifests, review diff, update README with exact menus and hardware smoke test.
- [ ] Device follow-up: install on the physical tablet and Quests; check Quest Link tracking/locomotion, touch controls, LAN discovery, and Wi-Fi interruption/recovery. No physical-device test was performed in this session.

## Execution record

- Baseline: user completed Quest setup; checkout clean. Client scene contains Meta Camera Rig and hand/controller interaction. Old XR Interaction Manager has a missing script; no DemoRigBinder is attached to the new rig.
- Ruling: continue implementation without another design gate because the user explicitly instructed continuation after the proposed design and supplied the XR environment.
- Ruling: work in the open editor's current feature checkout; a separate worktree would detach the edits from the user's configured Unity session.

- Verification: 38/38 Unity tests passed on 2026-10-15 at 16:06:25 UTC, including a dedicated server and two loopback clients exercising pose sync, late join, reconnect, frozen avatar replacement, and session restart. Build tests cover success/failure restoration, reloaded subassets, manifest cleanup, and generated Gradle dependency isolation.
- Verification: both saved demo scenes have zero missing scripts. Server Play Mode preview exercised start, stop, and day/night controls; day/night screenshots are in Logs. Imported environments contain two directional lights, both now dimmed in night mode.
- Review correction: the spectator viewport now excludes the opaque instructor sidebar and adapts to its scaled screen bounds. Both framing regression tests passed; final Play Mode screenshot is `Logs/tablet-final-preview.png`.
- Cleanup: removed unreferenced XRI settings left from the earlier integration; these referenced scripts from a package no longer installed. The user's Quest rig and XR packages remain in place.
- APK verification on 2026-10-16 (local): headset client succeeded with zero build errors (294,914,191 bytes); tablet server succeeded with zero build errors (222,269,898 bytes). Both build-menu calls completed settings restoration. Tablet manifest is ARM64/API 26/GLES3 without Quest/OpenXR or headset native-library requirements; client retains Quest head/hand tracking declarations. App IDs are distinct.
- Final editor state: ClientDemoScene, outside Play Mode, Android ASTC, Android XR startup enabled with one loader. No substantive diffs remain in ProjectSettings, Assets/Settings, Assets/XR, or Assets/Oculus after builds. Existing imported SDK/sample warnings remain; the demo scenes have no missing scripts.
