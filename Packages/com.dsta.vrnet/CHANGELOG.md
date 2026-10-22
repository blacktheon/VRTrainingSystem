# Changelog

## 0.1.1 - 2026-10-22

- Raise `VRNetSession.OnClientStartFailed` when a direct connection cannot start,
  including transport startup failures that never produce an NGO disconnect event.
- Keep reconnect backoff running through failed direct-start attempts, and let
  initial-connect adapters handle the same failure without transport-specific code.
- Document the upstream `master` Git URL and the explicit refresh required for
  Unity's commit-locked Git dependencies.
