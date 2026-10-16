namespace Blackjack.Simulation.Players;

/// <summary>Plays exactly like the dealer under the given rules (hit ≤ 16, stand ≥ 17; hits soft 17 under H17).</summary>
internal sealed class MimicDealerPlayer(decimal bet, Rules rules) : FlatBetPlayer(bet)
{
    private readonly DealerStrategy _dealer = new(rules);

    /// <inheritdoc />
    public override PlayerAction Decide(DecisionContext context) =>
        _dealer.ShouldHit(new Hand(context.PlayerCards)) ? PlayerAction.Hit : PlayerAction.Stand;
}
