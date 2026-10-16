namespace Blackjack.Simulation.Tests;

/// <summary>Builds round results for statistics tests.</summary>
public static class Rounds
{
    public static RoundResult Create(string playerCards, string dealerCards, HandOutcome outcome, decimal net, decimal bet = 10m)
    {
        var player = new Hand(Cards.Parse(playerCards));
        var dealer = new Hand(Cards.Parse(dealerCards));
        var hand = new HandResult(player.Cards.ToArray(), player.Total, player.IsSoft, bet, outcome, net);
        return new RoundResult(bet, [hand], dealer.Cards.ToArray(), dealer.Total, net, null);
    }
}
