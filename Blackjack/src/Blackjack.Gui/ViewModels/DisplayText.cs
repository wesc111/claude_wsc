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

    /// <summary>The banner of the table print; offered since E12 (spec E7).</summary>
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

    /// <summary>
    /// The decision situation of a strategy hint (spec E11), e.g. <c>hard 11 vs 6</c>, <c>soft 18 vs A</c> or
    /// <c>pair of 8s vs 10</c>; a pair only while it may be split.
    /// </summary>
    public static string Situation(DecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var cards = context.PlayerCards;
        var upcard = context.DealerUpcard.IsAce ? "A" : context.DealerUpcard.BaseValue.ToString(CultureInfo.InvariantCulture);
        var hand = context.AllowedActions.Contains(PlayerAction.Split) && cards.Count == 2 && cards[0].BaseValue == cards[1].BaseValue
            ? cards[0].IsAce ? "pair of Aces" : $"pair of {cards[0].BaseValue}s"
            : $"{(context.IsSoft ? "soft" : "hard")} {context.PlayerTotal}";
        return $"{hand} vs {upcard}";
    }

    /// <summary>Whether an insurance offer is even money: the player has Blackjack and it pays 3:2, so it wins 1:1 either way (spec E12).</summary>
    public static bool IsEvenMoney(InsuranceContext offer, decimal blackjackPayout)
    {
        ArgumentNullException.ThrowIfNull(offer);
        return offer.IsBlackjack && blackjackPayout == 1.5m;
    }

    /// <summary>The question of the insurance box, e.g. <c>Insurance for 5?</c> or <c>Take even money: +10 now?</c>.</summary>
    public static string InsuranceQuestion(InsuranceContext offer, decimal blackjackPayout)
    {
        ArgumentNullException.ThrowIfNull(offer);
        return IsEvenMoney(offer, blackjackPayout)
            ? $"You have Blackjack. Take even money: +{Money(offer.Amount * 2)} now?"
            : $"Insurance for {Money(offer.Amount)}?";
    }

    /// <summary>A count with sign, e.g. <c>+4</c>, <c>0</c>, <c>-3</c> (spec E13).</summary>
    public static string Count(int count) => count.ToString("+0;-0;0", CultureInfo.InvariantCulture);

    /// <summary>A true count or index with sign and one decimal, e.g. <c>+1.3</c>, <c>0.0</c>, <c>-0.4</c>.</summary>
    public static string TrueCount(decimal trueCount) => trueCount.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);

    /// <summary>The index condition of a count play, e.g. <c>true count +0.4 ≥ 0</c> or <c>true count -1.5 &lt; -1</c>.</summary>
    public static string IndexCondition(decimal trueCount, decimal index, bool atOrAbove) =>
        $"true count {TrueCount(trueCount)} {(atOrAbove ? "≥" : "<")} {Index(index)}";

    /// <summary>The index of a count play, e.g. <c>+4</c>, <c>0</c>, <c>-1</c>.</summary>
    public static string Index(decimal index) => index.ToString("+0.#;-0.#;0", CultureInfo.InvariantCulture);

    /// <summary>The hint for an index play, e.g. <c>Count play: Stand (hard 16 vs 10, true count +0.4 ≥ 0)</c>.</summary>
    public static string CountHint(IndexPlay play, DecisionContext context, decimal trueCount)
    {
        ArgumentNullException.ThrowIfNull(play);
        return $"Count play: {play.Deviation} ({Situation(context)}, {IndexCondition(trueCount, play.Index, play.AtOrAbove)})";
    }

    /// <summary>The feedback when an index play was missed, e.g. <c>Count play: Stand – you hit (hard 16 vs 10, true count +0.4 ≥ 0)</c>.</summary>
    public static string CountFeedback(IndexPlay play, PlayerAction chosen, DecisionContext context, decimal trueCount)
    {
        ArgumentNullException.ThrowIfNull(play);
        return $"Count play: {play.Deviation} – {You(chosen)} ({Situation(context)}, {IndexCondition(trueCount, play.Index, play.AtOrAbove)})";
    }

    /// <summary>The basic strategy hint on an insurance offer: always decline (spec E12).</summary>
    public static string InsuranceHint(bool evenMoney) => evenMoney ? "Basic strategy: No even money" : "Basic strategy: No insurance";

    /// <summary>The feedback after taking insurance or even money.</summary>
    public static string InsuranceFeedback(bool evenMoney) =>
        evenMoney ? "Basic strategy: No even money – you took even money" : "Basic strategy: No insurance – you took insurance";

    /// <summary>The hint shown before a decision, e.g. <c>Basic strategy: Double (hard 11 vs 6)</c>.</summary>
    public static string StrategyHint(PlayerAction recommended, DecisionContext context) =>
        $"Basic strategy: {recommended} ({Situation(context)})";

    /// <summary>The feedback after a decision that differs from basic strategy, e.g. <c>Basic strategy: Double – you hit (hard 11 vs 6)</c>.</summary>
    public static string StrategyFeedback(PlayerAction recommended, PlayerAction chosen, DecisionContext context)
    {
        return $"Basic strategy: {recommended} – {You(chosen)} ({Situation(context)})";
    }

    private static string You(PlayerAction chosen) => chosen switch
    {
        PlayerAction.Stand => "you stood",
        PlayerAction.Double => "you doubled",
        PlayerAction.Split => "you split",
        _ => "you hit",
    };
}
