namespace Blackjack.Simulation.Players;

/// <summary>Plays a basic strategy table (SPEC 6.2 / 7.6) with a flat bet.</summary>
internal sealed class BasicStrategyPlayer(decimal bet, BasicStrategyTable? table = null) : FlatBetPlayer(bet)
{
    private readonly BasicStrategyTable _table = table ?? BasicStrategyTable.Full;

    /// <inheritdoc />
    public override PlayerAction Decide(DecisionContext context) => _table.Decide(context);
}
