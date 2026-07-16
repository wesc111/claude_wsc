namespace ChessViewer.Models;

public enum PieceType  { Pawn, Knight, Bishop, Rook, Queen, King }
public enum PieceColor { White, Black }

public record ChessPiece(PieceType Type, PieceColor Color);

public record LastMove(int FromCol, int FromRow, int ToCol, int ToRow);

public class ChessBoard
{
    // _grid[col, row]: col 0='a'..7='h', row 0=rank1..7=rank8
    private readonly ChessPiece?[,] _grid = new ChessPiece?[8, 8];

    public (int Col, int Row)? EnPassantTarget { get; private set; }
    public LastMove? LastMove { get; private set; }

    public ChessPiece? this[int col, int row] =>
        col >= 0 && col < 8 && row >= 0 && row < 8 ? _grid[col, row] : null;

    private ChessBoard() { }

    private static readonly PieceType[] BackRow =
        [PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen,
         PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook];

    public static ChessBoard Initial()
    {
        var b = new ChessBoard();
        b.PlaceBackRow(PieceColor.White, backRank: 0, pawnRank: 1);
        b.PlaceBackRow(PieceColor.Black, backRank: 7, pawnRank: 6);
        return b;
    }

    private void PlaceBackRow(PieceColor color, int backRank, int pawnRank)
    {
        for (int col = 0; col < 8; col++)
        {
            _grid[col, backRank] = new ChessPiece(BackRow[col], color);
            _grid[col, pawnRank] = new ChessPiece(PieceType.Pawn, color);
        }
    }

    private ChessBoard Clone()
    {
        var b = new ChessBoard { EnPassantTarget = EnPassantTarget };  // LastMove intentionally not cloned
        Array.Copy(_grid, b._grid, 64);
        return b;
    }

    public ChessBoard ApplyMove(string san, PieceColor color)
    {
        var b = Clone();
        san = san.TrimEnd('+', '#', '!', '?').Trim();

        // Castling
        if (san is "O-O" or "0-0")     { b.Castle(color, kingside: true);  return b; }
        if (san is "O-O-O" or "0-0-0") { b.Castle(color, kingside: false); return b; }

        bool isCapture = san.Contains('x');
        san = san.Replace("x", "");

        // Promotion
        PieceType promoteTo = PieceType.Queen;
        if (san.Contains('='))
        {
            promoteTo = LetterToPiece(san[san.IndexOf('=') + 1]);
            san = san[..san.IndexOf('=')];
        }

        // Destination is always the last two chars: file + rank
        int toCol = san[^2] - 'a';
        int toRow  = san[^1] - '1';
        san = san[..^2];

        // Piece type (uppercase prefix); absent = pawn
        PieceType piece = PieceType.Pawn;
        if (san.Length > 0 && char.IsUpper(san[0]))
        {
            piece = LetterToPiece(san[0]);
            san = san[1..];
        }

        // Disambiguation: whatever remains is a file letter, rank digit, or both
        int? fromColHint = null, fromRowHint = null;
        foreach (char c in san)
        {
            if (c >= 'a' && c <= 'h') fromColHint = c - 'a';
            else if (c >= '1' && c <= '8') fromRowHint = c - '1';
        }

        // En passant: pawn captures to a square that was empty before this move
        bool isEnPassant = piece == PieceType.Pawn && isCapture && _grid[toCol, toRow] is null;

        var (fromCol, fromRow) = b.FindSource(piece, color, toCol, toRow, fromColHint, fromRowHint, isCapture);

        // Execute the move
        b._grid[toCol, toRow]     = b._grid[fromCol, fromRow];
        b._grid[fromCol, fromRow] = null;

        if (isEnPassant)
        {
            int epRow = color == PieceColor.White ? toRow - 1 : toRow + 1;
            b._grid[toCol, epRow] = null;
        }

        // Promotion
        if (piece == PieceType.Pawn && (toRow == 0 || toRow == 7))
            b._grid[toCol, toRow] = new ChessPiece(promoteTo, color);

        // Set en passant target for the opponent
        b.EnPassantTarget = piece == PieceType.Pawn && Math.Abs(toRow - fromRow) == 2
            ? (toCol, (toRow + fromRow) / 2)
            : null;

        b.LastMove = new LastMove(fromCol, fromRow, toCol, toRow);
        return b;
    }

    private void Castle(PieceColor color, bool kingside)
    {
        int rank = color == PieceColor.White ? 0 : 7;
        if (kingside)
        {
            _grid[6, rank] = new ChessPiece(PieceType.King, color);
            _grid[5, rank] = new ChessPiece(PieceType.Rook, color);
            _grid[4, rank] = null;
            _grid[7, rank] = null;
            LastMove = new LastMove(4, rank, 6, rank);
        }
        else
        {
            _grid[2, rank] = new ChessPiece(PieceType.King, color);
            _grid[3, rank] = new ChessPiece(PieceType.Rook, color);
            _grid[4, rank] = null;
            _grid[0, rank] = null;
            LastMove = new LastMove(4, rank, 2, rank);
        }
        EnPassantTarget = null;
    }

    private (int col, int row) FindSource(PieceType type, PieceColor color,
        int toCol, int toRow, int? fromColHint, int? fromRowHint, bool isCapture)
    {
        for (int col = 0; col < 8; col++)
        for (int row = 0; row < 8; row++)
        {
            var p = _grid[col, row];
            if (p is null || p.Type != type || p.Color != color) continue;
            if (fromColHint.HasValue && col != fromColHint) continue;
            if (fromRowHint.HasValue && row != fromRowHint) continue;
            if (CanReach(col, row, toCol, toRow, type, color, isCapture)) return (col, row);
        }
        throw new InvalidOperationException(
            $"No {color} {type} can reach {(char)('a' + toCol)}{toRow + 1}");
    }

    private bool CanReach(int fc, int fr, int tc, int tr, PieceType type, PieceColor color, bool isCapture) =>
        type switch
        {
            PieceType.Pawn   => CanPawnReach(fc, fr, tc, tr, color, isCapture),
            PieceType.Knight => IsKnightMove(fc, fr, tc, tr),
            PieceType.Bishop => CanSlide(fc, fr, tc, tr, diag: true,  straight: false),
            PieceType.Rook   => CanSlide(fc, fr, tc, tr, diag: false, straight: true),
            PieceType.Queen  => CanSlide(fc, fr, tc, tr, diag: true,  straight: true),
            PieceType.King   => Math.Abs(tc - fc) <= 1 && Math.Abs(tr - fr) <= 1,
            _                => false
        };

    private bool CanPawnReach(int fc, int fr, int tc, int tr, PieceColor color, bool isCapture)
    {
        int dir      = color == PieceColor.White ? 1 : -1;
        int startRow = color == PieceColor.White ? 1 : 6;

        if (isCapture)
            // Diagonal one step — used for normal captures and en passant
            return Math.Abs(fc - tc) == 1 && tr == fr + dir;

        // Non-capture: pawn must stay on the same file
        if (fc != tc) return false;
        if (tr == fr + dir) return true;
        if (fr == startRow && tr == fr + 2 * dir && _grid[tc, fr + dir] is null) return true;
        return false;
    }

    private static bool IsKnightMove(int fc, int fr, int tc, int tr)
    {
        int dc = Math.Abs(tc - fc), dr = Math.Abs(tr - fr);
        return (dc == 1 && dr == 2) || (dc == 2 && dr == 1);
    }

    private bool CanSlide(int fc, int fr, int tc, int tr, bool diag, bool straight)
    {
        int dc = tc - fc, dr = tr - fr;
        bool isDiag     = dc != 0 && dr != 0 && Math.Abs(dc) == Math.Abs(dr);
        bool isStraight = dc == 0 || dr == 0;

        if (isDiag && !diag) return false;
        if (isStraight && !straight) return false;
        if (!isDiag && !isStraight) return false;

        int sc = dc == 0 ? 0 : dc / Math.Abs(dc);
        int sr = dr == 0 ? 0 : dr / Math.Abs(dr);

        int c = fc + sc, r = fr + sr;
        while (c != tc || r != tr)
        {
            if (_grid[c, r] is not null) return false;
            c += sc; r += sr;
        }
        return true;
    }

    private static PieceType LetterToPiece(char c) => c switch
    {
        'N' => PieceType.Knight,
        'B' => PieceType.Bishop,
        'R' => PieceType.Rook,
        'Q' => PieceType.Queen,
        'K' => PieceType.King,
        _   => PieceType.Pawn
    };
}
