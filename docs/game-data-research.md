# Synth Riders V3+ game-data research checklist

## Confirmed in Phase 0

- Official Steam store App ID: `885000`.
- Steam root is discoverable from the Windows registry.
- `steamapps/libraryfolders.vdf` contains libraries outside drive C.
- `steamapps/appmanifest_885000.acf` identifies `installdir`.
- On this machine, the resulting install contains a `Playlist` directory with a `.playlist` file and files named `favorites.bin`, `played.bin`, `songstats.bin`, and `settings.bin`.
- Directory names `SynthRidersUC/CustomSongs` and `SynthRidersUC/CustomPlaylists` are present, along with other `SynthRidersUC/Custom*` directories.

These names and locations are observations only. No payload format or semantics has been inferred.

The observed local drive, Steam root, install path, build ID, filenames, and directory layout are research evidence—not production defaults. Production discovery must enumerate and validate the current user's Steam-managed locations at runtime.

## Evidence required before implementing readers

Use copies or sanitized samples; never edit the originals during research.

1. Game version/build and whether the sample was produced by V3+.
2. Directory listings before and after one controlled change in-game.
3. Representative Playlist files: empty, one-song, multi-song, reordered, renamed, and deleted cases.
4. `favorites.bin` snapshots before/after adding and removing exactly one known song.
5. A known official song, DLC song, and custom song, with the UI metadata visible for correlation.
6. Custom-song directory and representative beatmap/container files.
7. Official/DLC metadata location and evidence linking its IDs to Playlist/Favorites entries.
8. Local audio and artwork locations/formats for each supported song type.
9. Duplicate titles/artists and non-ASCII metadata to determine stable identity and encoding.
10. Steam Cloud behavior and any files updated at game shutdown.
11. Minimal identifying files/directories that safely validate a manually selected Synth Riders V3+ install without relying on mutable user-data locations.
12. Supported Steam installation discovery sources and precedence, including missing, stale, malformed, and multi-library metadata cases.

## Questions each format investigation must answer

- Is it text, JSON, a binary serialization, archive, encrypted payload, or Unity asset?
- What is the stable song/playlist identifier and its case/normalization rule?
- Which fields are optional, ordered, versioned, or unknown?
- Can unknown fields/records be preserved byte-for-byte or semantically on round trip?
- What validation proves a write is acceptable to the current game build?
- Which process names reliably indicate that Synth Riders is running?
- Does Steam Cloud introduce conflict or overwrite risks?
- Which observed values are format constants, and which vary by machine, user, install, or game build?

## Safe research procedure

- Confirm Synth Riders is stopped before collecting change-pair samples.
- Copy samples outside the game directory and hash originals/copies.
- Make one controlled in-game change per pair and record exactly what changed.
- Analyze copies only. Do not develop against or commit personal game data.
- Store sanitized fixtures under a future `tests/fixtures/verified/` directory; private copies remain ignored.

No Reader/Writer should be marked production-ready until its format, version assumptions, failure behavior, and unknown-data preservation are covered by fixtures and tests.
