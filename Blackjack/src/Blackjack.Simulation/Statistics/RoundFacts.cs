namespace Blackjack.Simulation.Statistics;

/// <summary>Facts about one round that the statistics need beyond the hand outcomes.</summary>
/// <param name="PlayerBlackjack">The player was dealt a Blackjack.</param>
/// <param name="DealerPlayed">The dealer played out the hand (player not bust, no Blackjack on either side).</param>
/// <param name="DealerBust">The dealer played out the hand and busted.</param>
internal readonly record struct RoundFacts(bool PlayerBlackjack, bool DealerPlayed, bool DealerBust)
{
    /// <summary>Derives the facts from a round result.</summary>
    public static RoundFacts From(RoundResult result)
    {
        // A + 10 after a split is not a Blackjack (SPEC 7.1).
        var first = result.PlayerHands[0];
        var playerBlackjack = result.PlayerHands.Count == 1 && !first.IsFromSplit && first.Cards.Count == 2 && first.Total == 21;
        var dealerBlackjack = result.DealerCards.Count == 2 && result.DealerTotal == 21;
        var anyHandStanding = false;
        foreach (var hand in result.PlayerHands)
        {
            anyHandStanding |= hand.Outcome != HandOutcome.PlayerBust;
        }

        var dealerPlayed = !playerBlackjack && !dealerBlackjack && anyHandStanding;
        return new RoundFacts(playerBlackjack, dealerPlayed, dealerPlayed && result.DealerTotal > 21);
    }
}
