namespace Blackjack.Simulation.Players;

/// <summary>Plays the Hit/Stand basic strategy (SPEC 6.2) with a flat bet.</summary>
internal sealed class BasicStrategyPlayer(decimal bet, BasicStrategyTable? table = null) : FlatBetPlayer(bet)
{
    private readonly BasicStrategyTable _table = table ?? BasicStrategyTable.HitStand;

    /// <inheritdoc />
    public override PlayerAction Decide(DecisionContext context) =>
        _table.Lookup(context.PlayerTotal, context.IsSoft, context.DealerUpcard);
}
