# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```powershell
dotnet build
dotnet run
```

No tests exist yet. There is no linter configured.

## Architecture

### Board model (`Models/Chess.cs`)

`ChessBoard` is **immutable by convention**: `ApplyMove()` clones the board and returns the new state; it never mutates `this`. The internal grid is `_grid[col, row]` where `col` 0–7 = files a–h and `row` 0–7 = ranks 1–8 (rank 1 is index 0, rank 8 is index 7).

There is **no check/checkmate/stalemate detection**. The engine trusts that PGN moves are legal.

### Move navigation (`ViewModels/MainViewModel.cs`)

Navigation always **replays from the initial position** — `GoToMove(n)` loops from move 0 to n rather than applying or undoing individual moves. This keeps the board state pure but means navigation is O(n).

`MoveList` is a flat list of SAN tokens (not pairs). White moves are at even indices, Black at odd. The `ListBox.SelectedIndex` binds directly to `CurrentMoveIndex`, so the selected item is the last-applied move.

### PGN parsing (`MainViewModel.ParsePgn`)

The parser strips `{comments}`, `(variations)`, and `$NAG` annotations before tokenizing. These are discarded, not stored. Only single-level parenthesized variations are stripped (nested parens will break the regex).

### Board rendering (`Controls/ChessboardControl.xaml.cs`)

`Render()` is called on every board change and rebuilds all 64 `Border` elements from scratch. Piece images are loaded once from `pack://application:,,,/Assets/Pieces/{key}.png` and cached in a static `Dictionary<string, BitmapImage?>`. Null entries in the cache mean the PNG is missing and the Unicode glyph fallback is used.

Asset naming convention: `{color}{piece}.png` where color is `w`/`b` and piece is `K Q R B N P` — e.g. `wK.png`, `bP.png`.

### Key limitations to be aware of

- PGN parser only handles single-game files; multi-game PGN requires splitting on blank lines before the second `[Event` tag.
- Castling rights and move legality are not tracked — the board always allows castling moves if the SAN says so.
- No FEN generation or position hashing exists yet.
