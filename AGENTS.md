# AGENTS.md

## Project scope

This repository contains **Synth Riders Playlist Manager**, a Windows-only local library and playlist manager for the Steam edition of Synth Riders V3 or later.

## Required technology

- C#, .NET, WPF, and MVVM
- Windows 10/11 x64
- Portable, self-contained `win-x64` publishing is the eventual distribution target
- Keep the application usable offline; do not make an online service the source of truth

## Source of truth and safety

- Synth Riders game data is the source of truth. App settings may contain UI/application preferences only.
- Support Steam only. Do not add Quest, Viveport, Unity, Electron, or a Python application UI.
- Never infer a V3+ path, schema, identifier, or audio format from old V2 information.
- Do not implement a reader or writer for unverified game data. Record the missing evidence and request representative real files.
- Treat game data as read-only while Synth Riders is running.
- Any future write must check that the game is stopped, create a backup, write to a temporary file, re-read and validate it, and replace the destination safely (atomically where possible).
- Preserve unknown fields and records during round-trip editing.
- Never bulk-modify game data without an explicit user confirmation.

## Environment-dependent values

- Never hard-code machine-specific paths, drive letters, user names, Steam Library locations, Steam userdata IDs, display/DPI assumptions, audio devices, game versions, song counts, or playlist counts.
- Do not hard-code unverified Playlist, Favorites, Custom Songs, metadata, artwork, or audio locations.
- Apply `Detect -> Validate -> Use -> Fallback` to every environment-dependent value.
- Discover the Steam installation from supported OS/Steam sources, enumerate every library from Steam metadata, inspect each library for the Synth Riders app manifest, resolve its actual install directory, and validate the result before use.
- A cached or manually selected game path is only a candidate. Validate it on every startup; if invalid, attempt automatic rediscovery.
- If discovery fails, keep the app usable and offer retry, validated manual folder selection, or cancel.
- Reject a manually selected directory unless verified identifying files/directories are present. Do not persist an invalid candidate.
- Distinguish environment-dependent values from specification constants. Verified constants such as the official Steam App ID, confirmed format magic values, and protocol-defined constants may remain constants with their provenance documented.
- Paths observed on a development machine may appear in research notes, but never in production defaults, fixtures, application code, or user settings checked into Git.

## Architecture

- Keep UI/ViewModels independent from game-file formats through Core interfaces.
- Put domain models and service contracts in `SynthRidersPlaylistManager.Core`.
- Put Steam discovery, file-system access, game-data adapters, settings, backup, logging, and audio implementations in `SynthRidersPlaylistManager.Infrastructure`.
- Put WPF views, ViewModels, resources, and composition in `SynthRidersPlaylistManager.App`.
- Keep tests beside these boundaries under `tests/`.
- Prefer small, explicit dependencies and avoid speculative abstractions.

## Development workflow

- Work phase-by-phase according to `docs/phase-0-report.md`; do not start a later phase without user direction.
- Do not read or write actual game payloads merely to make a test pass. Use sanitized fixtures after their formats are verified.
- Add tests for Steam/VDF parsing, process-state write guards, unknown-field preservation, and safe file replacement before enabling writes.
- UI strings belong in localization resources; game-provided song/artist/mapper text is not translated.
- Enable WPF collection virtualization for large song lists.
- Do not add telemetry or upload logs. Avoid logging personal information or unrelated filesystem paths.

## Verification

- Build with warnings treated seriously and nullable reference types enabled.
- Run the relevant unit tests after each behavioral change.
- For any game-data writer, require round-trip and failure-recovery tests before it can be wired to UI commands.
