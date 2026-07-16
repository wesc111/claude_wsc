# Chess Viewer (Mac)

An Avalonia (cross-platform, .NET) desktop app for macOS that loads a PGN file and lets
you step through a chess game move by move on a rendered board.

This is a **macOS port** of a sibling WPF app, `../ChessViewer`. The two front ends share
the core chess/PGN logic file-for-file (via a linked source file, not a copy) and
otherwise are independent, parallel implementations of the same UI.

## Features

- Load a single-game PGN file and replay it move by move.
- Board rendering with piece images (falls back to Unicode glyphs if an image is missing).
- Highlights the squares of the last move played.
- Per-move PGN comments: shown in full for the current move, and as an 8-character
  truncated preview next to each move in the move list.
- Keyboard navigation: **←/→** for previous/next move, **Home/End** to jump to the
  start/end of the game.
- Flip-board toggle.
- Packaged as a double-clickable `ChessViewer.app` with a custom icon (see
  [Building a double-clickable app](#building-a-double-clickable-app)).

## Requirements

- .NET SDK (the `.csproj` targets `net8.0` with `<RollForward>LatestMajor</RollForward>`,
  so a newer installed runtime, e.g. .NET 10, is fine — don't "fix" this by pinning to
  8.0 or removing the roll-forward setting).
- macOS on Apple Silicon (`osx-arm64`) for the packaged `.app`; `dotnet run` works on any
  platform Avalonia supports.

## Running during development

```bash
dotnet build
dotnet run
```

`dotnet run` launches the app but, because it isn't started via Finder/LaunchServices,
macOS won't automatically bring its window to the front — `App.axaml.cs` calls into
`MacActivation` (a small native `NSApplication.activateIgnoringOtherApps` call) once the
main window opens, so this happens automatically instead of requiring a manual click on
the Dock icon.

## Building a double-clickable app

```bash
./build-app.sh
```

This produces `ChessViewer.app` in the repo root:

1. `dotnet publish`es the project **self-contained** for `osx-arm64`. This is
   deliberate: a framework-dependent build only knows how to find the .NET runtime via
   `PATH`/`DOTNET_ROOT`, which a terminal shell has (via Homebrew, etc.) but which
   Finder-launched processes do **not** inherit. A framework-dependent bundle would
   crash within milliseconds of every real double-click while still working fine from
   `dotnet run`/`open` in a terminal — self-contained removes that entire failure mode
   at the cost of a larger bundle (~100 MB).
2. Assembles the standard bundle layout (`Contents/MacOS`, `Contents/Resources`,
   `Contents/Info.plist`) and copies in the app icon.
3. Re-signs the whole assembled bundle with `codesign --force --deep --sign -`. This
   step matters: `dotnet publish` already ad-hoc signs the raw executable by itself,
   but that seal only covers the lone binary. Once `Info.plist`/`Resources` are added
   around it, the signature no longer matches the bundle's contents, and Gatekeeper
   silently refuses to launch it on a real double-click (`spctl` reports *"code has no
   resources but signature indicates they must be present"*). Re-signing after assembly
   fixes the seal.

Re-run `build-app.sh` after any code change — `dotnet build`/`dotnet run` do not update
`ChessViewer.app`.

The app icon source lives at `Assets/AppIcon/icon-1024.png` (a checkerboard using the
board's own square colors with the white king piece centered) with the compiled
`Assets/AppIcon/AppIcon.icns` used by the bundle.

## Project layout

```
ChessViewer.Mac.csproj      Project file; links Chess.cs from the sibling WPF project
Program.cs                  Entry point / AppBuilder setup
App.axaml(.cs)               Application startup, macOS activation hook
MainWindow.axaml(.cs)         Main window; keyboard navigation, About dialog wiring
MacActivation.cs             P/Invoke helper to bring the app to the foreground on macOS
RelayCommand.cs               ICommand implementation used by the ViewModel
ViewModels/MainViewModel.cs   PGN parsing, move navigation, all app state/commands
Controls/ChessboardControl.*  Board rendering (pieces, highlights, rank/file labels)
Converters/                   XAML value converters (bool <-> string)
Views/                        About dialog, generic message dialog
Assets/Pieces/*.png           Piece bitmaps ({color}{piece}.png, e.g. wK.png, bP.png)
Assets/AppIcon/                App icon source + compiled .icns
build-app.sh                   Packages ChessViewer.app (see above)
analysis/, pgn/                 Sample game analyses and PGN files for manual testing —
                                 not consumed by any automated test suite
```

## Architecture

### Board model (`../chessviewer/Models/Chess.cs`, shared with the WPF project)

Edit this file in the **WPF project's** `Models/` folder — this project links it
directly (`<Compile Include="..\chessviewer\Models\Chess.cs">` in the `.csproj`) rather
than keeping its own copy, so the two front ends never diverge on chess rules or PGN
move application.

`ChessBoard` is immutable by convention: `ApplyMove(san, color)` clones the board and
returns the new state; it never mutates `this`. The grid is `_grid[col, row]` with
column 0–7 = files a–h and row 0–7 = ranks 1–8 (rank 1 is index 0).

`ApplyMove` parses a SAN token directly (castling, captures, promotion, disambiguation,
en passant) and finds the source square by scanning all pieces of the right type/color
that can legally reach the destination square, using hints from the SAN when a move is
ambiguous. There is **no check/checkmate/stalemate detection and no castling-rights
tracking** — the engine trusts that the PGN's moves are legal and disambiguates purely
from SAN text plus reachability.

### Move navigation (`ViewModels/MainViewModel.cs`)

Navigation always **replays from the initial position**: `GoToMove(n)` starts a fresh
`ChessBoard.Initial()` and applies moves `0..n` in a loop rather than applying/undoing
individual moves. This keeps board state pure but makes navigation O(n) in the move
index. `_currentMoveIndex` is set to `int.MinValue` before the initial `GoToMove(-1)`
call in `LoadPgnText` specifically so that first call isn't treated as a no-op (compared
against the already-current index).

`MoveList` is a flat `ObservableCollection<string>` of formatted tokens — White at even
indices, Black at odd — where the `ListBox.SelectedIndex` two-way binds directly to
`CurrentMoveIndex`. Each entry also carries the move's comment, truncated to 8
characters + `"..."` if longer (see `TruncateComment`), so it's visible directly in the
move list; the untruncated comment for the currently selected move is shown separately
via `FormattedComment` (wrapped to two lines, ellipsized if still too long).

### PGN parsing (`MainViewModel.ParsePgn`)

Strips `{comments}`, `(variations)`, and `$NAG` annotations before tokenizing SAN moves,
and tracks per-move comments via `Regex.Split` on the comment pattern (alternating
segments: move text, comment, move text, comment, …). Only single-level parenthesized
variations are stripped (nested parens will break the regex), and only single-game PGN
files are supported.

### Board rendering (`Controls/ChessboardControl.axaml.cs`)

`Render()` runs on every `Board`/`FlipBoard` property change (via `AvaloniaProperty`
`Changed` handlers) and rebuilds all 64 `Border` elements from scratch — there is no
incremental diffing. Piece bitmaps are loaded once per key from
`avares://ChessViewer/Assets/Pieces/{key}.png` and cached in a static
`Dictionary<string, Bitmap?>`; a cached `null` means the PNG is missing and the Unicode
glyph fallback (`♔♕♖♗♘♙` / `♚♛♜♝♞♟`) is used instead. Asset naming is
`{color}{piece}.png`, e.g. `wK.png`, `bP.png` (color `w`/`b`, piece `K Q R B N P`).

### Keyboard navigation (`MainWindow.axaml.cs`)

Arrow keys and Home/End are wired via a **tunnel-phase** handler
(`RoutingStrategies.Tunnel`), so they're intercepted before a focused child control
(like the move `ListBox`) can consume the key itself — the Avalonia analogue of WPF's
`PreviewKeyDown`.

### macOS activation (`MacActivation.cs`, `App.axaml.cs`)

A small P/Invoke wrapper around `NSApplication.sharedApplication` /
`activateIgnoringOtherApps:`, called from the main window's `Opened` event (calling it
any earlier is a no-op, since the window isn't a real `NSWindow` yet). This is
self-activation, not remote control of another process, so — unlike AppleScript/System
Events automation — it needs no Accessibility/Automation permission grant.

## Differences from the WPF project (`../ChessViewer`)

Everything outside `Models/` (ViewModels, Views, Controls, Converters, RelayCommand) is
a separate implementation rewritten for Avalonia's APIs, not shared. Notable
substitutions, called out in code comments where they occur:

| WPF                                          | Avalonia (this project)                                         |
|-----------------------------------------------|------------------------------------------------------------------|
| `CommandManager.RequerySuggested`             | `RelayCommand.RaiseCanExecuteChanged()` called explicitly         |
| Static `MessageBox` / `OpenFileDialog`        | `Views/MessageDialog` and `Window.StorageProvider.OpenFilePickerAsync`, both requiring an owning `Window` (why `MainViewModel` takes one in its constructor) |
| `Visibility` enum                              | `StringToBoolConverter` feeding a plain `bool` to `IsVisible`     |
| `pack://application:,,,/...` asset URIs        | `avares://ChessViewer/Assets/...` (`AssetLoader.Open`)            |

If you change behavior in one front end that isn't purely a `Chess.cs` change, consider
whether `../ChessViewer` needs the equivalent fix.

## Known limitations

- Castling rights and move legality are not tracked — the board allows any move the SAN
  claims is legal.
- No check/checkmate/stalemate detection.
- No FEN generation or position hashing.
- No automated tests and no linter are configured; `analysis/` and `pgn/` hold sample
  data for manual testing/reference only.
