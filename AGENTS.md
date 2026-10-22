# VRNet maintenance workflow

- This repository is the source of truth for `Packages/com.dsta.vrnet`.
- Implement reusable networking fixes here, with regression coverage, rather than maintaining downstream copies or workarounds in consuming projects.
- The user authorizes necessary VRNet updates to be committed and pushed to `origin/master`. Check the current remote tip and validate changes first; never force-push.
- Consumers such as STBC use `https://github.com/blacktheon/VRTrainingSystem.git?path=/Packages/com.dsta.vrnet#master`. The repository's default branch may differ, so keep the explicit branch suffix.
- After publishing a package change, refresh it through the consuming project's Unity Package Manager and commit its updated manifest/lockfile with the integration. A Git branch dependency stays at its resolved commit until refreshed.
