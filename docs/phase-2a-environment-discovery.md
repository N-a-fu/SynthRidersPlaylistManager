# Phase 2A — Environment Discovery / Validation / Read-only Foundation

## Scope and result

Phase 2A connects only the environment diagnostics to the installed Steam edition of Synth Riders. The main library and all editing operations remain Phase 1.1 mock behavior. No `.playlist`, `favorites.bin`, `SynthDB`, `.synth`, `ImagesCache`, `tempExt`, or other game file is written, moved, deleted, repaired, or normalized.

## Discovery strategy

The implementation follows `Detect -> Validate -> Use -> Fallback`:

1. Read Steam installation candidates from supported Windows registry sources (HKCU and HKLM).
2. Read each Steam root's `steamapps/libraryfolders.vdf` without recursively scanning drives.
3. Inspect each library for the specification constant `appmanifest_885000.acf`.
4. Read the manifest's `installdir`, construct the library-relative candidate, and require both `SynthRiders.exe` and `SynthRiders_Data`.
5. Derive the Phase 0.5-observed locations only as candidates and validate every location independently.
6. If detection fails, retain the mock UI and offer rediscovery or a validated manual selection.

Steam App ID `885000` is a documented specification constant. No drive letter, Steam root, user name, library path, install directory, userdata ID, game version, or content count is a production constant.

## Data Location model

Core exposes one `DataLocation` per independently resolvable kind:

- Game Root
- Playlists
- Favorites
- Custom Songs
- SynthDB
- ImagesCache
- tempExt / temporary audio

Each value carries `Kind`, `ResolvedPath`, `Source`, `Status`, `LastValidated`, `ValidationMessage`, and `IsUserOverride`. Sources are `AutoDetected`, `UserOverride`, `Derived`, and `NotResolved`. Statuses are `Available`, `Unavailable`, `Missing`, `Invalid`, `NotConfigured`, and `Unknown`.

The apparent common root in the observed installation is not encoded as an architectural requirement. Any location can receive its own manual override, including Custom Songs on another drive or through a junction/symbolic link.

## Validation strategy

- Game Root: require the observed executable and Unity data directory.
- Playlists: require an accessible directory; a top-level `.playlist` confirms `Available`. An accessible empty directory is `Unknown`, not incorrectly rejected.
- Favorites: require a readable JSON object with a `Favorite` array.
- Custom Songs: require an accessible directory; a top-level `.synth` confirms `Available`. Empty is `Unknown`.
- SynthDB: require the SQLite 3 file header.
- ImagesCache: require an accessible directory; a top-level `.png` confirms `Available`. Empty is `Unknown`.
- tempExt: existence/access only. Its lifecycle is still unknown and it is not a source of truth.

Validation performs no recursive disk scan. Access checks and actual I/O are both guarded because a removable drive can disappear between them. Permission, I/O, missing-drive, invalid-path, long-path, and format errors become location state and a user-facing explanation instead of terminating the app.

## Auto detection and manual precedence

A valid saved manual override has precedence over an automatically detected or root-derived candidate. A manual candidate is validated before it is persisted; `Invalid`, `Missing`, or `Unavailable` new selections are not adopted. `Unknown` is allowed only where the location is accessible but the format cannot be conclusively rejected.

Saved overrides are revalidated at startup and on rediscovery. When a previously saved path becomes unavailable, its path remains saved and is shown as `Unavailable`; automatic detection does not silently overwrite it. This permits reconnection and later revalidation.

## Unavailable versus Missing

`Unavailable` is a storage/location-level state: for example, a detached external drive or an I/O/access failure. It never means that songs were deleted and must never trigger individual missing-song warnings, playlist cleanup, favorite cleanup, or repair.

`Missing` means the parent storage is accessible but the expected selected file/directory does not exist. Individual song-level missing evaluation is deferred to Phase 2B and may occur only after its parent location is confirmed available.

## Warning policy

Messages state:

1. what could not be accessed or validated;
2. what the user can check, such as removable-drive connection or a changed path;
3. that the app did not interpret the state as deletion and did not modify game data.

Unknown stays unknown: it is explained and is not converted into a speculative valid/invalid decision.

## Settings UI

The existing main layout is unchanged. The right-side Settings panel shows all seven locations with path, status, detection source, validation detail, a per-location `参照...` action, and `再検出`. Discovery runs asynchronously with duplicate-scan guarding. The header and status bar explicitly state that Phase 2A is read-only and the song library is still mock data. The checkbox-only clear action is now labeled `チェック解除`.

## Real environment read-only verification (2026-09-01)

The built application was launched against a development machine. Steam metadata resolved App ID `885000` in a non-default Steam library. Machine-specific path prefixes have been redacted; these symbolic paths are research evidence only and are not defaults or fixtures:

- Game Root: `<DetectedGameRoot>`
- Playlists: `<DetectedGameRoot>\Playlist`
- Favorites: `<DetectedGameRoot>\favorites.bin`
- Custom Songs: `<DetectedGameRoot>\SynthRidersUC\CustomSongs`
- SynthDB: `<DetectedGameRoot>\SynthRidersUC\SynthDB`
- ImagesCache: `<DetectedGameRoot>\SynthRidersUC\ImagesCache`
- tempExt: `<DetectedGameRoot>\SynthRidersUC\tempExt`

All seven were shown as `Available`: game identity structure, `.playlist`, Favorites JSON shape, `.synth`, SQLite header, `.png`, and tempExt existence were confirmed read-only. This matches the Phase 0.5 observed structure. The application did not enumerate real songs into the main list and did not write to the installation.

## Tests and verification

Tests cover valid multi-library detection, no installation, invalid root, accessible missing directory, manual override and precedence, invalid manual rejection, saved unavailable-path retention, startup revalidation, broken/vanishing candidate recovery, format failures, and the existing Phase 1.1 mock/UI behavior. Test fixtures use unique temporary directories only.

The initial UI run revealed a read-only TextBox binding defaulting to TwoWay; it was corrected to explicit OneWay. The corrected build launches, displays both persistent browsers and mini player unchanged, detects 7/7 locations, and opens the Environment panel without a WPF binding/startup exception.

## Unresolved items

The Phase 0.5 unresolved list remains unchanged: Official/DLC classification and audio, custom hash generation, SynthDB/ImagesCache/tempExt lifecycle, playlist filename prefix, official-song mapping, in-game arbitrary reordering, and safe Custom Song deletion. Encrypted `.synth` content is not decrypted or bypassed. tempExt and ImagesCache remain optional/cache-like candidates, never guaranteed sources of truth.

## Phase 2B handoff

Phase 2B may build read-only library adapters behind Core contracts using only `Available` locations. It must keep the mock mode fallback, suppress item-level missing evaluation when a parent location is unavailable, tolerate disappearing storage and locked files, preserve unknown data, and avoid treating cache fields or absolute paths inside SynthDB as trusted locations. Phase 2B has not been started.

## Phase 3 writer safety and Delete from Library TODO

Any future writer still requires: game-stopped verification, explicit user intent, backup, temporary write, re-read/validation, safe replacement, failure recovery, and unknown-field preservation. No writer is present in Phase 2A.

`ライブラリから削除...` / `Delete from Library...` remains documentation-only. It must not be implemented until safe Custom Song deletion semantics and all affected game records/caches are verified with real evidence.
