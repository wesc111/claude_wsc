using System.Globalization;

namespace Blackjack.Gui.ViewModels;

/// <summary>Display texts for money, totals and outcomes (English, like the rest of the project).</summary>
public static class DisplayText
{
    /// <summary>Formats an amount without unnecessary decimals, e.g. <c>22.5</c>, <c>975</c>.</summary>
    public static string Money(decimal amount) => amount.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>The first line of the table print, e.g. <c>BLACKJACK PAYS 3 TO 2</c> (payout as the smallest fraction).</summary>
    public static string PayoutPrint(decimal blackjackPayout)
    {
        var denominator = Enumerable.Range(1, 100).FirstOrDefault(d => blackjackPayout * d == decimal.Truncate(blackjackPayout * d), 1);
        return $"BLACKJACK PAYS {Money(blackjackPayout * denominator)} TO {denominator}";
    }

    /// <summary>The second line of the table print, e.g. <c>Dealer Must Stand on Soft 17</c>.</summary>
    public static string DealerRulePrint(bool dealerHitsSoft17) =>
        dealerHitsSoft17 ? "Dealer Must Hit on Soft 17" : "Dealer Must Stand on Soft 17";

    /// <summary>The banner of the table print; Insurance is a planned feature (spec E7).</summary>
    public const string InsurancePrint = "Insurance Pays 2 to 1";

    /// <summary>Formats a net amount with sign, e.g. <c>+15</c>, <c>-25</c>, <c>0</c>.</summary>
    public static string SignedMoney(decimal amount) => amount > 0 ? "+" + Money(amount) : Money(amount);

    /// <summary>The total of a hand, e.g. <c>soft 17</c>, <c>22 bust</c> or <c>Blackjack</c>.</summary>
    public static string Total(Hand hand)
    {
        ArgumentNullException.ThrowIfNull(hand);
        if (hand.IsBlackjack)
        {
            return "Blackjack";
        }

        if (hand.IsBust)
        {
            return $"{hand.Total} bust";
        }

        return hand.IsSoft ? $"soft {hand.Total}" : hand.Total.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The text shown for an outcome.</summary>
    public static string Outcome(HandOutcome outcome) => outcome switch
    {
        HandOutcome.BlackjackWin => "Blackjack! You win",
        HandOutcome.Win => "You win",
        HandOutcome.DealerBust => "Dealer busts – you win",
        HandOutcome.Push => "Push",
        HandOutcome.Loss => "You lose",
        HandOutcome.PlayerBust => "Bust – you lose",
        HandOutcome.DealerBlackjack => "Dealer has Blackjack – you lose",
        _ => outcome.ToString(),
    };
}
