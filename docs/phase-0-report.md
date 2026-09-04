# Phase 0 report

Date: 2026-08-31 (Asia/Tokyo)

## Workspace and Git

- The workspace was empty and suitable for an independent new project.
- It was not a Git repository at the start of Phase 0; a local repository is initialized as part of this phase.
- No remote is configured and no commit is created.
- Root hygiene files, architecture notes, and research notes are added. No WPF project or game-data implementation is created yet.

## Development environment confirmed

- Windows x64 (`win-x64`), reported OS version `10.0.26200`.
- .NET SDK `10.0.303`, MSBuild `18.6.14`.
- `Microsoft.WindowsDesktop.App 10.0.11` is installed.
- The .NET SDK includes the C# WPF template, and a `net10.0` WPF dry run succeeds.
- Visual Studio Community 2022 `17.14.37` and Visual Studio Community 2026 `18.8.3` are installed and launchable.
- No optional `dotnet workload` is installed; WPF uses the Windows Desktop SDK and does not require a separate `dotnet workload`.

## Steam/Synth Riders information confirmed (read-only)

- The official Steam store URL confirms App ID `885000`.
- Steam is installed under `C:\Program Files (x86)\Steam`.
- `libraryfolders.vdf` contains multiple libraries, including non-C drives.
- `appmanifest_885000.acf` is present and reports install directory `SynthRiders`, build ID `24909110`, and installed state.
- The resolved local installation is on an H: Steam library.
- The install root visibly includes `Playlist` (containing a `.playlist` file), `favorites.bin`, `played.bin`, `songstats.bin`, and `settings.bin`.
- `SynthRidersUC` visibly contains `CustomSongs`, `CustomPlaylists`, and other custom-content directories.
- No file payload was parsed and no game file was changed.

Build IDs and local paths are observations, not constants for application code. Only App ID `885000`, verified against the official Steam store, is a candidate constant.

## Environment-dependent value policy

- Production code will use `Detect -> Validate -> Use -> Fallback` rather than development-machine defaults.
- Steam discovery will obtain the Steam installation through supported system information, enumerate all configured libraries, inspect the verified App ID manifest, resolve its manifest-provided install directory, and validate the candidate.
- Cached and manually selected paths will be revalidated. Invalid cached paths trigger rediscovery; invalid manual selections are rejected and not persisted.
- Discovery failure will be a recoverable UI state offering retry, manual folder selection, or cancel.
- Drive letters, Steam roots, install paths, user IDs, game versions, counts, DPI/display assumptions, and audio devices will not be constants.
- Playlist, Favorites, Custom Songs, metadata, artwork, and audio paths remain unimplemented until verified.

## Still unknown

- The precise V3+ game version corresponding to the observed build.
- Playlist directory contents/schema and playlist identity/order rules.
- `favorites.bin` serialization, IDs, encoding, versioning, and integrity checks.
- Official, DLC, and Custom Song metadata stores and stable cross-file song IDs.
- Audio/artwork formats and whether they can be safely previewed outside the game.
- Reliable game process names and Steam Cloud conflict behavior.
- Round-trip requirements for preserving unknown fields/records.

See `docs/game-data-research.md` for the evidence checklist.

## Information/files needed from the user before data-format implementation

- Permission to inspect read-only copies of the local V3+ files listed in the research checklist, or sanitized copies supplied separately.
- Controlled before/after samples for one favorite change and representative playlist operations.
- Confirmation of the game-displayed version associated with those samples.
- One known Official, DLC, and Custom Song example for correlating IDs and metadata.

These are not required to start the UI-only portion of Phase 1.

## Phase 1 scope (only after user approval)

1. Create the solution and the App/Core/Infrastructure/test projects described in `docs/architecture.md`.
2. Add the dark, high-density WPF shell with top/left/center/right/bottom regions and design-time placeholder data only.
3. Establish MVVM commands/state, dependency injection/composition, and capability states such as disconnected/read-only—not speculative game readers.
4. Add Japanese and English resource dictionaries with runtime language switching.
5. Add app-local settings and local file logging abstractions without telemetry.
6. Add initial tests and verify Debug/Release builds.

The Phase 1 shell will include the recoverable “Synth Riders could not be detected” state and retry/manual-select/cancel commands as UI contracts only. Actual Steam discovery belongs to Phase 2.

Phase 1 will not read or write unknown Synth Riders payload formats.
