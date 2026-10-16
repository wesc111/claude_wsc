namespace Blackjack.Simulation.Players;

/// <summary>Never risks a bust: stands on hard 12+, hits soft hands up to soft 17 and stands on soft 18+.</summary>
internal sealed class NeverBustPlayer(decimal bet) : FlatBetPlayer(bet)
{
    /// <inheritdoc />
    public override PlayerAction Decide(DecisionContext context)
    {
        var hit = context.IsSoft ? context.PlayerTotal <= 17 : context.PlayerTotal < 12;
        return hit ? PlayerAction.Hit : PlayerAction.Stand;
    }
}
