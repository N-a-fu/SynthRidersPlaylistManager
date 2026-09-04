# Phase 2C — Cover Art read-only integration

## Scope and safety

Phase 2C adds optional artwork presentation from the currently observed `ImagesCache`. It does not write, generate, normalize, rename, or delete any game file. `SynthDB`, `ImagesCache`, `.playlist`, `favorites.bin`, `.synth`, `tempExt`, and Custom Songs remain read-only. Cover art is auxiliary metadata and is never a song identity source or a Library-load prerequisite.

Both `Cover available` and `Cover not configured` are normal states. A song without a resolved cover remains in Library, Favorites, and Playlists, keeps the same identity and availability, and displays the standard music-note placeholder. Missing covers do not produce per-song warnings and are not a STOP condition.

## Current read-only observation

The installed environment was re-examined on 2026-09-01 without changing timestamps intentionally or opening files for write:

- ImagesCache files: **310**
- Extensions: **310 `.png`**
- 64-character hexadecimal filenames: **310**
- zero-byte files: **0**
- duplicate filenames (case-insensitive): **0**
- PNG files successfully decoded by WPF: **310**
- invalid/corrupt images: **0**
- SynthDB `TracksCache.leaderboard_hash`: **310 rows / 310 distinct**
- exact case-sensitive filename-stem ↔ `leaderboard_hash` matches: **310**
- direct matches used for Custom Songs: **310**
- orphan ImagesCache files: **0**
- conflicting mappings: **0**
- resolved Custom Song covers: **310**
- unresolved/missing Custom Song covers: **0** in the current environment
- Official-side / Unknown cover: **0 resolved / 1 unresolved**; placeholder is used

All observed hashes and filenames were lowercase. Production comparison remains ordinal case-insensitive because the identifier is hexadecimal and Windows filesystems may vary in case presentation; no title, artist, mapper, ordering, or count-based fallback is used.

Image dimensions ranged from **139×139 to 512×512**. **290/310** were square and **20/310** non-square. Common sizes included 500×500, 512×512, 300×300, 256×256, 511×511, and 225×225. UI artwork therefore uses `Stretch=Uniform` and does not assume a square bitmap.

## Architecture

```text
validated ImagesCache location
  -> top-level PNG enumeration
  -> filename hash validation
  -> PNG signature/IHDR validation
  -> exact hash catalog
  -> SynthDB Custom Song hash join
  -> Song.CoverState / CoverImagePath / CoverKey
  -> virtualized CoverArtView
  -> shared application-memory thumbnail cache
```

Core defines `CoverArtState` (`Available`, `Missing`, `Unknown`, `ParentLocationUnavailable`, `InvalidImage`, `ConflictingMapping`) and aggregate `CoverArtDiagnostics`. Infrastructure owns filesystem enumeration, PNG validation, direct hash mapping, and diagnostics. App owns WPF decode and presentation.

The existing Song metadata load remains primary. Cover resolution is optional and cannot remove a song or cause Real Library to fall back to Mock. An unavailable ImagesCache emits one location-level warning; missing individual files do not. A corrupt image receives `InvalidImage` and uses the placeholder.

Official-side / Unknown entries do not receive the Custom Song mapping rule. `TEST OFFICIAL SONG` therefore remains unresolved and uses the placeholder; no Custom hash is invented.

## Loading and cache strategy

`CoverArtView` starts loading only when its virtualized WPF element is loaded. It decodes on a worker thread, requests 48-pixel data for list thumbnails and 96-pixel data for the shared Mini Player, freezes the resulting `BitmapImage`, and caches it in application memory by full validated path plus decode size. Both panes therefore reuse the same decoded asset instead of decoding once per pane.

`BitmapCacheOption.OnLoad` and a stream opened with read/share-delete semantics release the game file immediately after decode. No converted thumbnail is written to ImagesCache or elsewhere. Row recycling is protected by comparing the requested path after asynchronous completion, preventing a prior row's image from being assigned to a recycled row. Decode or I/O failure after discovery leaves the placeholder visible.

Existing DataGrid row/column virtualization and recycling remain enabled. Full-resolution images are not expanded at startup.

In the 311-song environment, covers appeared without a visible startup stall and rapid scrolling remained responsive with no observed cross-row image mix. After releasing the external UI-capture session, an idle five-second sample used **0.031 CPU seconds**, remained responsive, and held approximately **166.5 MB Working Set**. CPU measured while screen capture was attached was intentionally excluded because it represented observation overhead rather than the application alone.

## UI behavior

- Browser A/B use a 34px artwork slot and preserve the dense list layout.
- The single shared Mini Player uses a 56px artwork slot within the existing one-row Header/Mini Player composition.
- Selecting a different row updates that shared cover.
- Search, filters, active pane, row selection, checkboxes, Favorite hearts, and playlist membership remain independent of cover state.
- Real Mode hearts and all game-data editing commands remain disabled; Audio Preview remains unconnected.
- Mock songs use the same placeholder and do not depend on the real ImagesCache.

## Failure and STOP policy

Cover missing is normal. Even many unresolved covers do not stop the phase and do not classify songs as missing. STOP applies to ambiguous/cross-song mapping, conflicting images for one hash, identity corruption, disappearance of songs caused by artwork handling, need for `.synth` decryption, or any requirement to write game data.

ImagesCache remains a **currently observed cache source**, not a permanent source of truth. Its generation/invalidation lifecycle and official/DLC artwork source remain unresolved.

## Handoff

Phase 2C is limited to artwork. Audio Preview, Favorites write, Playlist write, library deletion, and all Writer work remain unimplemented. Any later phase must preserve cover optionality, hash identity, read-only failure isolation, and the existing safe writer prerequisites.
