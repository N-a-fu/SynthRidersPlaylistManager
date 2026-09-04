# Architecture decisions

## Solution layout planned for Phase 1

```text
SynthRidersPlaylistManager.sln
src/
  SynthRidersPlaylistManager.App/             WPF views, ViewModels, localization, composition
  SynthRidersPlaylistManager.Core/            Domain models, use cases, service contracts
  SynthRidersPlaylistManager.Infrastructure/  Steam, filesystem, settings, logging, game adapters, audio
tests/
  SynthRidersPlaylistManager.Core.Tests/
  SynthRidersPlaylistManager.Infrastructure.Tests/
docs/
```

Three production projects keep WPF and file formats separated without creating a project for every feature. Audio begins as an Infrastructure implementation behind a Core contract; it should become a separate project only if later complexity justifies it.

## Dependency direction

```text
App -> Core
App -> Infrastructure (composition root only)
Infrastructure -> Core
Core -> no UI or Infrastructure dependency
Tests -> project under test
```

The future Synth Riders adapters will implement Core read/write contracts. Until a V3+ format is verified, those contracts may expose capability/readiness states but must not contain speculative serialization details.

## Target baseline

- SDK pinned by `global.json`: 10.0.303
- Planned target framework: `net10.0-windows`
- Planned runtime identifier for release: `win-x64`
- Nullable reference types and implicit usings: enabled
- WPF collection virtualization: required for large library lists

The SDK and target can be revised before Phase 1 if repository contributors require a different supported baseline.

## Environment discovery policy

All machine-dependent values follow this pipeline:

```text
Detect -> Validate -> Use -> Fallback
```

Steam/Synth Riders discovery will therefore be modeled as:

```text
OS-supported Steam discovery sources
  -> parse the active Steam library metadata
  -> enumerate all libraries (any drive or path)
  -> locate appmanifest for verified App ID 885000
  -> resolve the manifest's actual install directory
  -> validate identifying game structure
  -> return a validated installation result
```

The implementation must return structured failure information instead of throwing an application-ending error. The UI can then offer rediscovery, manual selection, or cancellation.

A cached/manual path is validated before each use. An invalid cached path triggers rediscovery and is never silently retained as a valid installation. Validation rules must be based on verified identifying structure and must not assume Playlist, Favorites, Custom Songs, audio, or metadata locations until those locations are confirmed.

Only specification-level values may be constants. The official Steam App ID is one such value; drive letters, Steam roots, install directories, userdata IDs, game versions, counts, screen/DPI values, and audio devices are runtime data.

## Phase 2A environment boundary

`Core` owns the `DataLocation` state model and discovery contract. `Infrastructure` owns Windows/Steam metadata access, filesystem probing, validation, and app-settings persistence. `App` composes those implementations and presents diagnostics, without exposing game formats to the ViewModel.

Game Root, Playlists, Favorites, Custom Songs, SynthDB, ImagesCache, and tempExt are independent locations. A root-relative path observed in Phase 0.5 is a discovery candidate, not a permanent invariant. Each candidate is separately validated and may be independently overridden.

Location-level `Unavailable` is deliberately distinct from accessible-storage `Missing`. Downstream readers must not emit per-item missing results or cleanup candidates while a parent location is unavailable. See [`phase-2a-environment-discovery.md`](phase-2a-environment-discovery.md) for the implemented validation matrix and Phase 2B/3 handoff constraints.

## Phase 2B snapshot boundary

Real game data flows one way through `IRealLibraryReader` into a `LibrarySnapshot`; Views/ViewModels never query SQLite or parse game JSON. Custom identities use existing verified hashes without generating new values. Favorites and playlist entries that do not join remain explicit unresolved records.

Real mode is a capability boundary, not just a label: all write-like UI commands and drag/drop are disabled, while navigation, search, row selection, checkboxes, and metadata display remain usable. Mock mode remains available for CI, regression tests, development, and discovery failure. Details are in [`phase-2b-real-song-library.md`](phase-2b-real-song-library.md).

## Phase 2C optional cover boundary

Cover art is optional metadata layered onto a successfully loaded song snapshot. Core owns cover state and aggregate diagnostics, Infrastructure validates and maps the currently observed hash-named PNG cache, and App performs lazy thumbnail decode into a shared application-memory cache. Cover failure never changes song identity, availability, Favorite/Playlist membership, Real/Mock mode, or Library load success. See [`phase-2c-cover-art.md`](phase-2c-cover-art.md).
