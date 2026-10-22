# VRNet maintenance workflow

- This repository is the source of truth for `Packages/com.dsta.vrnet`.
- Keep VRNet general-purpose for any VR Unity project. Tank controls, crew roles, scene bindings, and application-specific behavior belong in the consuming project.
- Change this package only when a required shared networking capability or low-level fix cannot reasonably live downstream. Add regression coverage for those changes.
- The user authorizes necessary VRNet updates to be committed and pushed to `origin/develop`. Check the current remote tip and validate changes first; never force-push. This replaces the previous master-branch workflow.
- Consumers such as STBC use `https://github.com/blacktheon/VRTrainingSystem.git?path=/Packages/com.dsta.vrnet#develop`; keep the explicit branch suffix.
- After publishing a package change, refresh it through the consuming project's Unity Package Manager and commit its updated manifest/lockfile with the integration. A Git branch dependency stays at its resolved commit until refreshed.
