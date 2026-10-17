using System.Globalization;

namespace Blackjack.Reports;

/// <summary>Text formatting shared by the report files (English, invariant culture).</summary>
public static class ReportText
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>An amount without unnecessary decimals, e.g. <c>22.5</c>.</summary>
    public static string Money(decimal amount) => amount.ToString("0.##", Invariant);

    /// <summary>A net amount with sign, e.g. <c>+15</c>, <c>-25</c>, <c>0</c>.</summary>
    public static string SignedMoney(decimal amount) => amount > 0 ? "+" + Money(amount) : Money(amount);

    /// <summary>A percentage with one decimal, e.g. <c>42.9 %</c>; <c>n/a</c> without a base.</summary>
    public static string Percent(int count, int total) =>
        total == 0 ? "n/a" : (100.0 * count / total).ToString("0.0", Invariant) + " %";

    /// <summary>Cards separated by spaces, e.g. <c>A♠ 10♥</c>.</summary>
    public static string Cards(IEnumerable<Card> cards) => string.Join(" ", cards);

    /// <summary>The total of a hand, e.g. <c>soft 17</c>, <c>22 bust</c>, <c>Blackjack</c>.</summary>
    public static string Total(IReadOnlyList<Card> cards, bool isFromSplit = false)
    {
        var hand = new Hand(cards) { IsFromSplit = isFromSplit };
        if (hand.IsBlackjack)
        {
            return "Blackjack";
        }

        if (hand.IsBust)
        {
            return $"{hand.Total} bust";
        }

        return hand.IsSoft ? $"soft {hand.Total}" : hand.Total.ToString(Invariant);
    }

    /// <summary>The text of an outcome, e.g. <c>Dealer busts – you win</c>.</summary>
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

    /// <summary>The rules, e.g. <c>6 decks, S17, 3:2, penetration 75 %, bets 10–500</c>.</summary>
    public static string Rules(Rules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return $"{rules.Decks} deck{(rules.Decks == 1 ? "" : "s")}, {(rules.DealerHitsSoft17 ? "H17" : "S17")}, " +
               $"{Payout(rules.BlackjackPayout)}, penetration {(rules.Penetration * 100).ToString("0.##", Invariant)} %, " +
               $"bets {Money(rules.MinBet)}–{Money(rules.MaxBet)}";
    }

    private static string Payout(decimal payout)
    {
        for (var denominator = 1; denominator <= 10; denominator++)
        {
            var numerator = payout * denominator;
            if (numerator == decimal.Truncate(numerator))
            {
                return $"{numerator.ToString("0", Invariant)}:{denominator}";
            }
        }

        return $"{payout.ToString("0.####", Invariant)}:1";
    }
}
