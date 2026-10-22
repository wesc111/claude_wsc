namespace Blackjack.Core;

/// <summary>
/// A basic strategy table stored as data: one row of codes per player total (or pair), one column per
/// dealer upcard 2, 3, …, 10, A. Codes are separated by spaces: <c>H</c> Hit, <c>S</c> Stand,
/// <c>D</c> Double/Hit, <c>Ds</c> Double/Stand, <c>P</c> Split, <c>-</c> no split (SPEC 6.2 and 7.6).
/// </summary>
public sealed class BasicStrategyTable
{
    private const int MinHardTotal = 4;
    private const int MinSoftTotal = 12;
    private const int MaxTotal = 21;
    private const int MinPairValue = 2;
    private const int MaxPairValue = 11;

    private readonly StrategyCode[,] _hard;
    private readonly StrategyCode[,] _soft;
    private readonly StrategyCode[,]? _pairs;

    /// <summary>Creates a table from row definitions; every total (and pair, if given) must be covered exactly once.</summary>
    public BasicStrategyTable(
        IEnumerable<(int From, int To, string Codes)> hardRows,
        IEnumerable<(int From, int To, string Codes)> softRows,
        IEnumerable<(int From, int To, string Codes)>? pairRows = null)
    {
        _hard = Build(hardRows, MinHardTotal, MaxTotal, "hard", allowSplitCodes: false);
        _soft = Build(softRows, MinSoftTotal, MaxTotal, "soft", allowSplitCodes: false);
        _pairs = pairRows is null ? null : Build(pairRows, MinPairValue, MaxPairValue, "pair", allowSplitCodes: true);
    }

    /// <summary>The Hit/Stand-only basic strategy for multi-deck S17 (SPEC 6.2), strategy <c>basic-hs</c>.</summary>
    public static BasicStrategyTable HitStand { get; } = new(
        hardRows:
        [
            //         2 3 4 5 6 7 8 9 T A
            (4, 11, "H H H H H H H H H H"),
            (12, 12, "H H S S S H H H H H"),
            (13, 16, "S S S S S H H H H H"),
            (17, 21, "S S S S S S S S S S"),
        ],
        softRows:
        [
            (12, 17, "H H H H H H H H H H"),
            (18, 18, "S S S S S S S H H H"),
            (19, 21, "S S S S S S S S S S"),
        ]);

    /// <summary>The full basic strategy for 6 decks, S17, DAS, no surrender (SPEC 7.6), strategy <c>basic</c>.</summary>
    public static BasicStrategyTable Full { get; } = new(
        hardRows:
        [
            //         2 3 4 5 6 7 8 9 T A
            (4, 8, "H H H H H H H H H H"),
            (9, 9, "H D D D D H H H H H"),
            (10, 10, "D D D D D D D D H H"),
            (11, 11, "D D D D D D D D D H"),
            (12, 12, "H H S S S H H H H H"),
            (13, 16, "S S S S S H H H H H"),
            (17, 21, "S S S S S S S S S S"),
        ],
        softRows:
        [
            (12, 12, "H H H H H H H H H H"),
            (13, 14, "H H H D D H H H H H"),
            (15, 16, "H H D D D H H H H H"),
            (17, 17, "H D D D D H H H H H"),
            (18, 18, "S Ds Ds Ds Ds S S H H H"),
            (19, 21, "S S S S S S S S S S"),
        ],
        pairRows:
        [
            // Pair value: 2–10, 11 = Aces.
            (2, 3, "P P P P P P - - - -"),
            (4, 4, "- - - P P - - - - -"),
            (5, 5, "- - - - - - - - - -"),
            (6, 6, "P P P P P - - - - -"),
            (7, 7, "P P P P P P - - - -"),
            (8, 8, "P P P P P P P P P P"),
            (9, 9, "P P P P P S P P S S"),
            (10, 10, "- - - - - - - - - -"),
            (11, 11, "P P P P P P P P P P"),
        ]);

    /// <summary>The code for a player total against a dealer upcard.</summary>
    public StrategyCode Lookup(int total, bool isSoft, Card dealerUpcard)
    {
        var min = isSoft ? MinSoftTotal : MinHardTotal;
        ArgumentOutOfRangeException.ThrowIfLessThan(total, min);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(total, MaxTotal);
        return (isSoft ? _soft : _hard)[total - min, Column(dealerUpcard)];
    }

    /// <summary>The pair code (<see cref="StrategyCode.Split"/> or <see cref="StrategyCode.NoSplit"/>) for a pair of the given value.</summary>
    public StrategyCode LookupPair(int pairValue, Card dealerUpcard)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pairValue, MinPairValue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pairValue, MaxPairValue);
        return _pairs is null ? StrategyCode.NoSplit : _pairs[pairValue - MinPairValue, Column(dealerUpcard)];
    }

    /// <summary>Decides with this table; Double and Split fall back to the alternative action when they are not allowed.</summary>
    public PlayerAction Decide(DecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var allowed = context.AllowedActions;
        var cards = context.PlayerCards;
        if (allowed.Contains(PlayerAction.Split)
            && cards.Count == 2
            && cards[0].BaseValue == cards[1].BaseValue
            && LookupPair(cards[0].BaseValue, context.DealerUpcard) == StrategyCode.Split)
        {
            return PlayerAction.Split;
        }

        var action = Lookup(context.PlayerTotal, context.IsSoft, context.DealerUpcard) switch
        {
            StrategyCode.DoubleOrHit => allowed.Contains(PlayerAction.Double) ? PlayerAction.Double : PlayerAction.Hit,
            StrategyCode.DoubleOrStand => allowed.Contains(PlayerAction.Double) ? PlayerAction.Double : PlayerAction.Stand,
            StrategyCode.Stand => PlayerAction.Stand,
            _ => PlayerAction.Hit,
        };

        // Split Aces that may be re-split only allow Stand or Split.
        return allowed.Contains(action) ? action : PlayerAction.Stand;
    }

    private static int Column(Card dealerUpcard) => dealerUpcard.IsAce ? 9 : dealerUpcard.BaseValue - 2;

    private static StrategyCode[,] Build(
        IEnumerable<(int From, int To, string Codes)> rows, int min, int max, string kind, bool allowSplitCodes)
    {
        var table = new StrategyCode[max - min + 1, 10];
        var covered = new bool[max - min + 1];
        foreach (var (from, to, codes) in rows)
        {
            var cells = codes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (cells.Length != 10)
            {
                throw new ArgumentException($"The {kind} row {from}–{to} must have 10 codes, one per upcard.");
            }

            for (var row = from; row <= to; row++)
            {
                if (row < min || row > max || covered[row - min])
                {
                    throw new ArgumentException($"The {kind} row {row} is out of range or defined twice.");
                }

                covered[row - min] = true;
                for (var column = 0; column < 10; column++)
                {
                    table[row - min, column] = Parse(cells[column], kind, from, to, allowSplitCodes);
                }
            }
        }

        var missing = Array.IndexOf(covered, false);
        if (missing >= 0)
        {
            throw new ArgumentException($"The {kind} row {missing + min} is not defined.");
        }

        return table;
    }

    private static StrategyCode Parse(string code, string kind, int from, int to, bool allowSplitCodes) => (code, allowSplitCodes) switch
    {
        ("H", false) => StrategyCode.Hit,
        ("S", _) => StrategyCode.Stand,
        ("D", false) => StrategyCode.DoubleOrHit,
        ("Ds", false) => StrategyCode.DoubleOrStand,
        ("P", true) => StrategyCode.Split,
        ("-", true) => StrategyCode.NoSplit,
        _ => throw new ArgumentException($"Unknown code '{code}' in the {kind} row {from}–{to}."),
    };
}
