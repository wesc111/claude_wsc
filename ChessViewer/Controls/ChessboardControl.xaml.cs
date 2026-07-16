using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ChessViewer.Models;

namespace ChessViewer.Controls;

public partial class ChessboardControl : UserControl
{
    private static readonly SolidColorBrush LightSquare     = new(Color.FromRgb(0xF0, 0xD9, 0xB5));
    private static readonly SolidColorBrush DarkSquare      = new(Color.FromRgb(0xB5, 0x88, 0x63));
    private static readonly SolidColorBrush LightSquareMark = new(Color.FromRgb(0xF6, 0xF6, 0x69)); // yellow tint
    private static readonly SolidColorBrush DarkSquareMark  = new(Color.FromRgb(0xBB, 0xB2, 0x3A));

    // Fallback Unicode glyphs used when a PNG file is missing
    private static readonly SolidColorBrush WhitePiece  = new(Color.FromRgb(0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush BlackPiece  = new(Color.FromRgb(0x1A, 0x1A, 0x1A));

    // Cache loaded bitmaps so each image is decoded only once
    private static readonly Dictionary<string, BitmapImage?> _imageCache = [];

    public static readonly DependencyProperty BoardProperty =
        DependencyProperty.Register(nameof(Board), typeof(ChessBoard), typeof(ChessboardControl),
            new PropertyMetadata(null, (d, _) => ((ChessboardControl)d).Render()));

    public ChessBoard? Board
    {
        get => (ChessBoard?)GetValue(BoardProperty);
        set => SetValue(BoardProperty, value);
    }

    public static readonly DependencyProperty FlipBoardProperty =
        DependencyProperty.Register(nameof(FlipBoard), typeof(bool), typeof(ChessboardControl),
            new PropertyMetadata(false, (d, _) => ((ChessboardControl)d).Render()));

    public bool FlipBoard
    {
        get => (bool)GetValue(FlipBoardProperty);
        set => SetValue(FlipBoardProperty, value);
    }

    public ChessboardControl()
    {
        InitializeComponent();
        Render();
    }

    private void Render()
    {
        BoardGrid.Children.Clear();
        var board    = Board;
        var lastMove = board?.LastMove;

        bool flipped = FlipBoard;
        for (int displayRow = 0; displayRow < 8; displayRow++)
        {
            int boardRow = flipped ? displayRow : 7 - displayRow;
            for (int displayCol = 0; displayCol < 8; displayCol++)
            {
                int col = flipped ? 7 - displayCol : displayCol;

                bool isDark   = (col + boardRow) % 2 == 0;
                bool isMarked = lastMove is not null &&
                                ((col == lastMove.FromCol && boardRow == lastMove.FromRow) ||
                                 (col == lastMove.ToCol   && boardRow == lastMove.ToRow));

                var square = new Border
                {
                    Background = isMarked
                        ? (isDark ? DarkSquareMark : LightSquareMark)
                        : (isDark ? DarkSquare     : LightSquare)
                };

                var piece = board?[col, boardRow];
                if (piece is not null)
                    square.Child = MakePieceElement(piece);

                BoardGrid.Children.Add(square);
            }
        }
    }

    private static UIElement MakePieceElement(ChessPiece piece)
    {
        var bitmap = LoadPieceImage(piece);

        if (bitmap is not null)
        {
            return new Image
            {
                Source  = bitmap,
                Stretch = Stretch.Uniform,
                Margin  = new Thickness(3)
            };
        }

        // Fallback: Unicode glyph when the PNG file is not present
        return new TextBlock
        {
            Text                = UnicodeGlyph(piece),
            FontSize            = 40,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Foreground          = piece.Color == PieceColor.White ? WhitePiece : BlackPiece
        };
    }

    private static BitmapImage? LoadPieceImage(ChessPiece piece)
    {
        string key = PieceKey(piece);

        if (_imageCache.TryGetValue(key, out var cached))
            return cached;

        try
        {
            var uri = new Uri($"pack://application:,,,/Assets/Pieces/{key}.png", UriKind.Absolute);
            var img = new BitmapImage(uri);
            _imageCache[key] = img;
            return img;
        }
        catch
        {
            // Image file not found — cache the miss so we don't retry on every render
            _imageCache[key] = null;
            return null;
        }
    }

    private static string PieceKey(ChessPiece piece)
    {
        char color  = piece.Color == PieceColor.White ? 'w' : 'b';
        char letter = piece.Type switch
        {
            PieceType.King   => 'K',
            PieceType.Queen  => 'Q',
            PieceType.Rook   => 'R',
            PieceType.Bishop => 'B',
            PieceType.Knight => 'N',
            _                => 'P'
        };
        return $"{color}{letter}";   // e.g. "wK", "bP"
    }

    private static string UnicodeGlyph(ChessPiece piece) => (piece.Type, piece.Color) switch
    {
        (PieceType.King,   PieceColor.White) => "♔",
        (PieceType.Queen,  PieceColor.White) => "♕",
        (PieceType.Rook,   PieceColor.White) => "♖",
        (PieceType.Bishop, PieceColor.White) => "♗",
        (PieceType.Knight, PieceColor.White) => "♘",
        (PieceType.Pawn,   PieceColor.White) => "♙",
        (PieceType.King,   PieceColor.Black) => "♚",
        (PieceType.Queen,  PieceColor.Black) => "♛",
        (PieceType.Rook,   PieceColor.Black) => "♜",
        (PieceType.Bishop, PieceColor.Black) => "♝",
        (PieceType.Knight, PieceColor.Black) => "♞",
        (PieceType.Pawn,   PieceColor.Black) => "♟",
        _                                    => ""
    };
}
