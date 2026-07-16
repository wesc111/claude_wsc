using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ChessViewer.Models;
using ChessViewer.Views;

namespace ChessViewer.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    // Needed for StorageProvider (file picker) and as the owner of modal dialogs —
    // Avalonia has no static MessageBox/OpenFileDialog like WPF, both need a Window.
    private readonly Window _owner;

    private ChessBoard _board = ChessBoard.Initial();
    private List<string> _sanMoves = [];
    private List<string> _moveComments = [];
    private int _currentMoveIndex = -1;
    private bool _flipBoard;

    // Parsed PGN header tags — available for future display (e.g. White, Black, Event, Date)
    public Dictionary<string, string> GameProperties { get; private set; } = [];

    public ChessBoard Board
    {
        get => _board;
        private set { _board = value; OnPropertyChanged(); }
    }

    public bool FlipBoard
    {
        get => _flipBoard;
        set { _flipBoard = value; OnPropertyChanged(); }
    }

    public string CurrentComment =>
        _currentMoveIndex >= 0 && _currentMoveIndex < _moveComments.Count
            ? _moveComments[_currentMoveIndex]
            : string.Empty;

    public string FormattedComment
    {
        get
        {
            string text = CurrentComment;
            if (text.Length <= 80) return text;

            // Prefer breaking at a word boundary at or before column 80
            int wrap = text.LastIndexOf(' ', 79);
            if (wrap < 0) wrap = 80;

            string line1 = text[..wrap].TrimEnd();
            string line2 = text[(text[wrap] == ' ' ? wrap + 1 : wrap)..].TrimStart();
            if (line2.Length > 80) line2 = line2[..79] + "…";

            return line1 + "\n" + line2;
        }
    }

    public ObservableCollection<string> MoveList { get; } = [];

    public int CurrentMoveIndex
    {
        get => _currentMoveIndex;
        set => GoToMove(value);
    }

    public string MoveStatus => _sanMoves.Count == 0
        ? "No game loaded"
        : _currentMoveIndex < 0 ? "Start" : $"{_currentMoveIndex + 1} / {_sanMoves.Count}";

    private readonly RelayCommand _nextMoveCommand;
    private readonly RelayCommand _prevMoveCommand;
    private readonly RelayCommand _goToStartCommand;
    private readonly RelayCommand _goToEndCommand;

    public ICommand LoadPgnCommand   { get; }
    public ICommand NextMoveCommand  => _nextMoveCommand;
    public ICommand PrevMoveCommand  => _prevMoveCommand;
    public ICommand GoToStartCommand => _goToStartCommand;
    public ICommand GoToEndCommand   => _goToEndCommand;
    public ICommand FlipBoardCommand { get; }

    public MainViewModel(Window owner)
    {
        _owner = owner;

        LoadPgnCommand    = new RelayCommand(LoadPgn);
        _nextMoveCommand  = new RelayCommand(() => GoToMove(_currentMoveIndex + 1),
                                            () => _currentMoveIndex < _sanMoves.Count - 1);
        _prevMoveCommand  = new RelayCommand(() => GoToMove(_currentMoveIndex - 1),
                                            () => _currentMoveIndex >= 0);
        _goToStartCommand = new RelayCommand(() => GoToMove(-1),
                                            () => _currentMoveIndex >= 0);
        _goToEndCommand   = new RelayCommand(() => GoToMove(_sanMoves.Count - 1),
                                            () => _currentMoveIndex < _sanMoves.Count - 1);
        FlipBoardCommand  = new RelayCommand(() => FlipBoard = !FlipBoard);
    }

    private async void LoadPgn()
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load PGN File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("PGN Files") { Patterns = ["*.pgn"] },
                FilePickerFileTypes.All
            ]
        });
        if (files.Count == 0) return;

        try { LoadPgnText(await File.ReadAllTextAsync(files[0].Path.LocalPath)); }
        catch (Exception ex)
        {
            ShowError("Load Failed", $"Error: {ex.Message}");
        }
    }

    public void LoadPgnText(string pgn)
    {
        var (properties, moves, comments) = ParsePgn(pgn);
        if (moves.Count == 0)
        {
            ShowError("Parse Error", "No moves found in PGN.");
            return;
        }

        GameProperties   = properties;
        _sanMoves        = moves;
        _moveComments    = comments;

        MoveList.Clear();
        for (int i = 0; i < _sanMoves.Count; i++)
        {
            int    num     = i / 2 + 1;
            string prefix  = i % 2 == 0 ? $"{num,3}." : "    ";
            string line    = $"{prefix} {_sanMoves[i],-8}";
            string comment = TruncateComment(comments[i]);
            if (comment.Length > 0) line += $"  {comment}";
            MoveList.Add(line);
        }

        _currentMoveIndex = int.MinValue; // ensure GoToMove(-1) isn't treated as a no-op
        GoToMove(-1);
    }

    private void GoToMove(int index)
    {
        index = Math.Clamp(index, -1, _sanMoves.Count - 1);
        if (index == _currentMoveIndex) return;

        try
        {
            // Replay all moves from the start up to 'index'
            var board = ChessBoard.Initial();
            for (int i = 0; i <= index; i++)
            {
                var color = i % 2 == 0 ? PieceColor.White : PieceColor.Black;
                board = board.ApplyMove(_sanMoves[i], color);
            }

            _currentMoveIndex = index;
            Board = board;
        }
        catch (Exception ex)
        {
            ShowError("Error", $"Move error at {index + 1}: {ex.Message}");
            return;
        }

        OnPropertyChanged(nameof(CurrentMoveIndex));
        OnPropertyChanged(nameof(MoveStatus));
        OnPropertyChanged(nameof(CurrentComment));
        OnPropertyChanged(nameof(FormattedComment));
        RaiseNavCommandsChanged();
    }

    private void RaiseNavCommandsChanged()
    {
        _nextMoveCommand.RaiseCanExecuteChanged();
        _prevMoveCommand.RaiseCanExecuteChanged();
        _goToStartCommand.RaiseCanExecuteChanged();
        _goToEndCommand.RaiseCanExecuteChanged();
    }

    private void ShowError(string title, string message) => _ = MessageDialog.Show(_owner, title, message);

    // Comments shown beside each move in the move list are truncated to keep rows short;
    // the full comment for the currently selected move is still shown in FormattedComment.
    private static string TruncateComment(string comment) =>
        comment.Length > 8 ? comment[..8] + "..." : comment;

    // Parse a PGN string into header properties, SAN move tokens, and per-move comments.
    // A {comment} in PGN follows the move it annotates; odd-indexed segments from Regex.Split
    // are the comment text, even-indexed segments contain move tokens.
    private static (Dictionary<string, string> properties, List<string> moves, List<string> comments) ParsePgn(string pgn)
    {
        // Extract header tag pairs: [Key "Value"]
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Regex.Matches(pgn, @"^\[(\w+)\s+""([^""]*)""\]", RegexOptions.Multiline))
            properties[m.Groups[1].Value] = m.Groups[2].Value;

        // Strip headers — use \r? to handle both \n and \r\n line endings
        var text = Regex.Replace(pgn, @"^\[.*?\]\r?$", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"\([^)]*\)", "");  // single-level variations
        text = Regex.Replace(text, @"\$\d+",     "");  // NAG annotations
        text = Regex.Replace(text, @"\b(1-0|0-1|1/2-1/2|\*)\b", "");

        var moves    = new List<string>();
        var comments = new List<string>();

        // Regex.Split with a capturing group produces alternating segments:
        //   [0] text before first {}, [1] first comment, [2] text after, [3] next comment, …
        var segments = Regex.Split(text, @"\{([^}]*)\}");

        for (int i = 0; i < segments.Length; i++)
        {
            if (i % 2 == 0)
            {
                // Move-text segment: tokenise and append moves with empty comment placeholders
                var tokens = segments[i]
                    .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => Regex.Replace(t, @"^\d+\.+", ""))
                    .Where(t => t.Length > 0);

                foreach (var token in tokens)
                {
                    moves.Add(token);
                    comments.Add(string.Empty);
                }
            }
            else if (moves.Count > 0)
            {
                // Comment segment: attach to the last move that was just parsed
                comments[moves.Count - 1] = segments[i].Trim();
            }
        }

        return (properties, moves, comments);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
