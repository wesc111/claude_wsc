# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet build
dotnet run
```

No tests exist yet. There is no linter configured.

The dev machine has only the .NET 10 runtime installed, not 8.0 — the `.csproj` sets
`<RollForward>LatestMajor</RollForward>` so the app still runs; don't "fix" this by
downgrading the runtime or removing the setting.

## Relationship to the sibling WPF project

This is an **Avalonia (cross-platform) port** of `../ChessViewer`, a WPF/Windows app.
The two front ends intentionally share game logic instead of duplicating it:
`ChessViewer.Mac.csproj` links `../chessviewer/Models/Chess.cs` directly (see the
`<Compile Include>` in the csproj) rather than copying the file. **Edit `Chess.cs` in
the WPF project's `Models/` folder, not here** — there is no local copy to edit.

Everything outside `Models/` (ViewModels, Views, Controls, Converters, RelayCommand) is
a separate, parallel implementation, rewritten for Avalonia's APIs rather than shared.
Where WPF and Avalonia diverge, the code has comments explaining the substitution, e.g.:
- No `CommandManager.RequerySuggested` → `RelayCommand.RaiseCanExecuteChanged()` must be
  called explicitly after any state change that affects a command's `CanExecute`.
- No static `MessageBox`/`OpenFileDialog` → `Views/MessageDialog` and
  `Window.StorageProvider.OpenFilePickerAsync` both require an owning `Window`, which is
  why `MainViewModel` takes one in its constructor.
- No `Visibility` enum → `StringToBoolConverter` feeds a plain `bool` to `IsVisible`.
- Assets load via `avares://ChessViewer/Assets/Pieces/{key}.png` (`AssetLoader.Open`),
  not WPF's `pack://application:,,,/...`.

If you change behavior in one front end that isn't purely a `Chess.cs` change, consider
whether the other front end (`../ChessViewer`) needs the equivalent fix.

## Architecture

### Board model (`Models/Chess.cs`, shared with the WPF project)

`ChessBoard` is **immutable by convention**: `ApplyMove()` clones the board and returns
the new state; it never mutates `this`. The internal grid is `_grid[col, row]` where col
0–7 = files a–h and row 0–7 = ranks 1–8 (rank 1 is index 0, rank 8 is index 7).

There is **no check/checkmate/stalemate detection**. The engine trusts that PGN moves
are legal and disambiguates purely from SAN + reachability.

### Move navigation (`ViewModels/MainViewModel.cs`)

Navigation always **replays from the initial position** — `GoToMove(n)` loops from move
0 to n rather than applying or undoing individual moves. This keeps board state pure but
means navigation is O(n). `_currentMoveIndex` is set to `int.MinValue` before the initial
`GoToMove(-1)` call in `LoadPgnText` specifically so that call isn't treated as a no-op.

`MoveList` is a flat list of formatted SAN tokens (not pairs) — White at even indices,
Black at odd. The `ListBox.SelectedIndex` two-way binds directly to `CurrentMoveIndex`.

Keyboard navigation (arrow keys, Home/End) is wired in `MainWindow.axaml.cs` via a
**tunnel-phase** handler (`RoutingStrategies.Tunnel`), so it intercepts before a focused
child control like the move `ListBox` consumes the key itself — the Avalonia analogue of
WPF's `PreviewKeyDown`.

### PGN parsing (`MainViewModel.ParsePgn`)

Strips `{comments}`, `(variations)`, and `$NAG` annotations before tokenizing, and tracks
per-move comments via `Regex.Split` on the comment pattern (alternating segments: move
text, comment, move text, comment, ...). Only single-level parenthesized variations are
stripped (nested parens will break the regex). Only single-game PGN files are supported.

### Board rendering (`Controls/ChessboardControl.axaml.cs`)

`Render()` runs on every `Board`/`FlipBoard` property change (via `AvaloniaProperty`
`Changed` handlers) and rebuilds all 64 `Border` elements from scratch. Piece bitmaps are
loaded once per key from `avares://ChessViewer/Assets/Pieces/{key}.png` and cached in a
static `Dictionary<string, Bitmap?>`; a cached `null` means the PNG is missing and the
Unicode glyph fallback is used instead. Asset naming: `{color}{piece}.png`, e.g. `wK.png`,
`bP.png` (color `w`/`b`, piece `K Q R B N P`).

### Key limitations to be aware of

- Castling rights and move legality are not tracked — the board allows any move the SAN
  claims is legal.
- No FEN generation or position hashing exists.
- The `analysis/` and `pgn/` directories hold sample game analyses and PGN files used for
  manual testing/reference, not app source or fixtures consumed by any test suite.
