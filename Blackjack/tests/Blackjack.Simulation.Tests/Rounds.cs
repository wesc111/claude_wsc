namespace Blackjack.Simulation.Tests;

/// <summary>Builds round results for statistics tests.</summary>
public static class Rounds
{
    public static RoundResult Create(string playerCards, string dealerCards, HandOutcome outcome, decimal net, decimal bet = 10m)
    {
        var player = new Hand(Cards.Parse(playerCards));
        var dealer = new Hand(Cards.Parse(dealerCards));
        var hand = new HandResult(player.Cards.ToArray(), player.Total, player.IsSoft, bet, outcome, net);
        return new RoundResult(bet, [hand], dealer.Cards.ToArray(), dealer.Total, net, null, bet);
    }

    /// <summary>A round with several hands (after a split) or a doubled hand; each hand as (cards, outcome, bet, net, doubled).</summary>
    public static RoundResult CreateHands(string dealerCards, decimal initialBet, params (string Cards, HandOutcome Outcome, decimal Bet, decimal Net, bool Doubled)[] hands)
    {
        var dealer = new Hand(Cards.Parse(dealerCards));
        var split = hands.Length > 1;
        var results = hands.Select(h =>
        {
            var hand = new Hand(Cards.Parse(h.Cards)) { IsFromSplit = split };
            return new HandResult(hand.Cards.ToArray(), hand.Total, hand.IsSoft, h.Bet, h.Outcome, h.Net, h.Doubled, split);
        }).ToArray();
        return new RoundResult(hands.Sum(h => h.Bet), results, dealer.Cards.ToArray(), dealer.Total, hands.Sum(h => h.Net), null, initialBet);
    }
}
