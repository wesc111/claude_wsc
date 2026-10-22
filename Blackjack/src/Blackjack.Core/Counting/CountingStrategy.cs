namespace Blackjack.Core;

/// <summary>
/// Basic strategy (SPEC 7.6) with the Illustrious 18 of the Hi-Lo count (SPEC E13): the insurance index and the
/// 17 index plays, with the common multi-deck S17 indices. The true count is compared exactly, not rounded.
/// </summary>
public static class CountingStrategy
{
    /// <summary>Insurance is taken at true count ≥ +3 (more than a third of the remaining cards are tens).</summary>
    public const decimal InsuranceIndex = 3m;

    private const PlayerAction H = PlayerAction.Hit;
    private const PlayerAction S = PlayerAction.Stand;
    private const PlayerAction D = PlayerAction.Double;
    private const PlayerAction P = PlayerAction.Split;

    /// <summary>The index plays of the Illustrious 18 except insurance, in the usual order of their value.</summary>
    public static IReadOnlyList<IndexPlay> IndexPlays { get; } =
    [
        new(16, false, 10, H, S, 0m, AtOrAbove: true),
        new(15, false, 10, H, S, 4m, AtOrAbove: true),
        new(10, true, 5, S, P, 5m, AtOrAbove: true),
        new(10, true, 6, S, P, 4m, AtOrAbove: true),
        new(10, false, 10, H, D, 4m, AtOrAbove: true),
        new(12, false, 3, H, S, 2m, AtOrAbove: true),
        new(12, false, 2, H, S, 3m, AtOrAbove: true),
        new(11, false, 11, H, D, 1m, AtOrAbove: true),
        new(9, false, 2, H, D, 1m, AtOrAbove: true),
        new(10, false, 11, H, D, 4m, AtOrAbove: true),
        new(9, false, 7, H, D, 3m, AtOrAbove: true),
        new(16, false, 9, H, S, 5m, AtOrAbove: true),
        new(13, false, 2, S, H, -1m, AtOrAbove: false),
        new(12, false, 4, S, H, 0m, AtOrAbove: false),
        new(12, false, 5, S, H, -2m, AtOrAbove: false),
        new(12, false, 6, S, H, -1m, AtOrAbove: false),
        new(13, false, 3, S, H, -2m, AtOrAbove: false),
    ];

    /// <summary>Whether to take insurance at <paramref name="trueCount"/>.</summary>
    public static bool TakeInsurance(decimal trueCount) => trueCount >= InsuranceIndex;

    /// <summary>
    /// The action at <paramref name="trueCount"/> and the index play that caused it, or <c>null</c> if basic
    /// strategy applies. A play applies only where basic strategy chooses its basic action (e.g. not when a pair of
    /// 8s is split) and only if its deviation is allowed now; otherwise basic strategy decides.
    /// </summary>
    public static (PlayerAction Action, IndexPlay? Play) Decide(DecisionContext context, decimal trueCount)
    {
        ArgumentNullException.ThrowIfNull(context);
        var basic = BasicStrategyTable.Full.Decide(context);
        foreach (var play in IndexPlays)
        {
            if (play.Basic == basic && play.Matches(context) && play.AppliesAt(trueCount)
                && context.AllowedActions.Contains(play.Deviation))
            {
                return (play.Deviation, play);
            }
        }

        return (basic, null);
    }

    /// <summary>
    /// The bet units of the 1–8 spread (SPEC E13): true count below +2: 1, below +3: 2, below +4: 4, below +5: 6, else 8.
    /// </summary>
    public static int BetUnits(decimal trueCount) => trueCount switch
    {
        < 2m => 1,
        < 3m => 2,
        < 4m => 4,
        < 5m => 6,
        _ => 8,
    };

    /// <summary>The bet of the spread with one unit = the minimum bet, limited to the largest valid bet.</summary>
    public static decimal Bet(BetContext context, decimal trueCount)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Math.Min(BetUnits(trueCount) * context.Rules.MinBet, context.MaxAllowedBet);
    }
}
