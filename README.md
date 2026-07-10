# Engine DJ Playlist Sync - Pass 76

## Changes

- Adds relocated-track detection to the Import Preview workflow.
- Import preview now matches tracks in this order:
  - exact stored Engine path
  - stable audio identity using filename, file size, duration and metadata where available
- Tracks that moved folders but match an existing library row are shown as `Relocated existing`, not `New track`.
- Relocated existing tracks are selected by default so the import can repair the stored path and playlist membership without inserting a duplicate Track row.
- Import now updates the existing Track.path when a unique relocated match is found.
- Preview/log counts now separate New, Relocated, and Existing tracks.
- Keeps Pass 75 Missing Files help text.
- Keeps concurrent analysis option.
- Keeps Missing Files layout crash fix, global font reduction, UHD/HD scaling, SQLite-safe packages, .NET 10 single-file standalone publish, and stems research removed.

## Publish

Run on Windows with the .NET 10 SDK installed:

```cmd
publish-win-x64-single-file.cmd
```
