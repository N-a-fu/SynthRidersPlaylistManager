# Official / DLC Cover Art Investigation

## Status

Investigation complete.

Official / DLC cover art is intentionally not integrated at this time.
The application will continue to use its Placeholder for Official / DLC songs.

This is a deliberate architecture decision, not an indication that the cover
images do not exist.

## Confirmed Findings

### Custom Songs

Custom Song cover art is already supported through ImagesCache.

- SynthDB `leaderboard_hash` matches PNG filenames.
- 310 / 310 Custom Songs matched during Phase 2C investigation.
- Existing implementation uses lazy decoding and shared in-memory caching.

### Official Song Sample

Song:

- TEST OFFICIAL SONG
- Test Artist

Confirmed Unity data:

- Song MonoBehaviour Path ID: 683
- Cover PPtr: `fileID=0, pathID=54`
- Cover object: Texture2D Path ID 54
- Texture name: `0134-Watchyourtongue_PiSk`
- Size: 512 x 512
- Format: DXT1
- Storage: inline in `sharedassets1.assets`

UnityPy successfully decoded the Texture2D and the resulting image was
confirmed to match the in-game cover.

### Paid DLC Sample

Song:

- New Gold
- Gorillaz feat. Tame Impala & Bootie Brown
- Pack ID: GORILLAZ

Confirmed Unity data:

- Song MonoBehaviour Path ID: 655
- Cover PPtr: `fileID=0, pathID=252`
- Cover object: Texture2D Path ID 252
- Texture name: `0106-Gorillaz_New_Gold`
- Storage: `sharedassets1.assets`

The paid DLC sample uses the same structural relationship as the Official
sample:

`Song MonoBehaviour -> Texture2D PPtr -> Cover`

## Important Result

Cover selection does not require:

- title matching
- artist matching
- Texture2D filename guessing
- hardcoded Texture2D Path IDs

The Song object directly references its Cover Texture2D through a PPtr.

## C# / .NET Investigation

AssetsTools.NET 3.0.5 was tested as a lightweight implementation candidate.

Result:

- It could read the MonoBehaviour.
- The available TypeTree exposed only the basic MonoBehaviour fields.
- `m_Script` resolves to:
  - `globalgamemanagers.assets`
  - MonoScript Path ID 497
  - Class: `Util.Data.OSTSongScriptableObject`
  - Assembly: `Assembly-CSharp`
- The MonoScript identifies the class but does not contain the custom field schema.
- `GetBaseField` could not reconstruct the Song fields because the required
  IL2CPP type template was unavailable.

Therefore AssetsTools.NET alone cannot safely identify the Cover PPtr as a
typed Song field.

## Rejected Implementation Methods

The following methods are intentionally not used:

- Cpp2IL or equivalent large IL2CPP reconstruction in the product
- AssetRipper or equivalent large asset-analysis infrastructure
- AssetStudio as a runtime dependency
- Python runtime in the product
- UnityPy in the product
- fixed byte offsets
- raw-byte heuristics
- hardcoded Path IDs
- Texture2D name guessing
- title/artist based cover guessing
- per-song mappings

Without full IL2CPP type reconstruction, a lightweight and structurally safe
C# method was not found.

## Product Decision

Current behavior:

- Custom Songs: real cover art
- Official songs: Placeholder
- DLC songs: Placeholder

Cover art remains optional metadata.

Failure to resolve a cover must never make a Song Missing, Invalid, or cause
the song to disappear from the library.

No large IL2CPP analysis dependency will be added solely for cover-art
display.

## Future Reconsideration

Official / DLC cover integration may be reconsidered if a lightweight,
maintainable .NET solution becomes available that can safely resolve the
custom MonoBehaviour field schema without:

- large IL2CPP reconstruction
- fixed offsets
- heuristics
- Python/runtime external tools

The confirmed PPtr relationship documented above can then be reused without
repeating the original asset investigation.

## Safety

All investigation of Synth Riders game data was read-only.

No Synth Riders game data was modified.

The Playlist Manager source and Git staging were not modified during the
asset probes.
