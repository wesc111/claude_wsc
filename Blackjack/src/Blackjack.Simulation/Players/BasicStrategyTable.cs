namespace Blackjack.Simulation.Players;

/// <summary>
/// A basic strategy table stored as data: one row of action letters per player total, one column per
/// dealer upcard 2, 3, …, 10, A. Letters: <c>H</c> = Hit, <c>S</c> = Stand (M4 will add <c>D</c> and <c>P</c>).
/// </summary>
internal sealed class BasicStrategyTable
{
    private const int MinHardTotal = 4;
    private const int MinSoftTotal = 12;
    private const int MaxTotal = 21;

    private readonly PlayerAction[,] _hard;
    private readonly PlayerAction[,] _soft;

    /// <summary>Creates a table from row definitions; every total must be covered exactly once.</summary>
    public BasicStrategyTable(
        IEnumerable<(int From, int To, string Actions)> hardRows,
        IEnumerable<(int From, int To, string Actions)> softRows)
    {
        _hard = Build(hardRows, MinHardTotal, "hard");
        _soft = Build(softRows, MinSoftTotal, "soft");
    }

    /// <summary>The Hit/Stand basic strategy for multi-deck S17 (SPEC 6.2).</summary>
    public static BasicStrategyTable HitStand { get; } = new(
        hardRows:
        [
            //            2345678910A
            (4, 11, "HHHHHHHHHH"),
            (12, 12, "HHSSSHHHHH"),
            (13, 16, "SSSSSHHHHH"),
            (17, 21, "SSSSSSSSSS"),
        ],
        softRows:
        [
            (12, 17, "HHHHHHHHHH"),
            (18, 18, "SSSSSSSHHH"),
            (19, 21, "SSSSSSSSSS"),
        ]);

    /// <summary>Looks up the action for a player total against a dealer upcard.</summary>
    public PlayerAction Lookup(int total, bool isSoft, Card dealerUpcard)
    {
        var min = isSoft ? MinSoftTotal : MinHardTotal;
        ArgumentOutOfRangeException.ThrowIfLessThan(total, min);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(total, MaxTotal);
        var column = dealerUpcard.IsAce ? 9 : dealerUpcard.BaseValue - 2;
        return (isSoft ? _soft : _hard)[total - min, column];
    }

    private static PlayerAction[,] Build(IEnumerable<(int From, int To, string Actions)> rows, int minTotal, string kind)
    {
        var table = new PlayerAction[MaxTotal - minTotal + 1, 10];
        var covered = new bool[MaxTotal - minTotal + 1];
        foreach (var (from, to, actions) in rows)
        {
            if (actions.Length != 10)
            {
                throw new ArgumentException($"The {kind} row {from}–{to} must have 10 actions, one per upcard.");
            }

            for (var total = from; total <= to; total++)
            {
                if (total < minTotal || total > MaxTotal || covered[total - minTotal])
                {
                    throw new ArgumentException($"The {kind} total {total} is out of range or defined twice.");
                }

                covered[total - minTotal] = true;
                for (var column = 0; column < 10; column++)
                {
                    table[total - minTotal, column] = actions[column] switch
                    {
                        'H' => PlayerAction.Hit,
                        'S' => PlayerAction.Stand,
                        var c => throw new ArgumentException($"Unknown action '{c}' in the {kind} row {from}–{to}."),
                    };
                }
            }
        }

        var missing = Array.IndexOf(covered, false);
        if (missing >= 0)
        {
            throw new ArgumentException($"The {kind} total {missing + minTotal} is not defined.");
        }

        return table;
    }
}
