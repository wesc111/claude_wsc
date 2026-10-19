namespace Blackjack.Gui.Settings;

/// <summary>
/// The settings the GUI keeps between starts (spec E9): table rules, starting bankroll, the bankroll to continue with
/// and the Sound switch. The rules can only take the values offered in the settings dialog.
/// </summary>
public sealed record GameSettings
{
    /// <summary>The selectable Blackjack payouts: 3:2 and 6:5.</summary>
    public static IReadOnlyList<decimal> PayoutOptions { get; } = [1.5m, 1.2m];

    /// <summary>The selectable penetrations (reshuffle marks).</summary>
    public static IReadOnlyList<decimal> PenetrationOptions { get; } = [0.5m, 0.67m, 0.75m, 0.8m];

    /// <summary>The selectable minimum bets; each is below every maximum bet.</summary>
    public static IReadOnlyList<decimal> MinBetOptions { get; } = [5m, 10m, 25m];

    /// <summary>The selectable maximum bets.</summary>
    public static IReadOnlyList<decimal> MaxBetOptions { get; } = [100m, 250m, 500m, 1000m];

    /// <summary>The selectable starting bankrolls.</summary>
    public static IReadOnlyList<decimal> StartingBankrollOptions { get; } = [500m, 1000m, 2000m, 5000m];

    /// <summary>The settings of a first start: the default rules, bankroll 1000, sound on.</summary>
    public static GameSettings Default { get; } = new();

    /// <summary>Number of decks (1–8).</summary>
    public int Decks { get; init; } = 6;

    /// <summary><c>true</c> = H17, <c>false</c> = S17.</summary>
    public bool DealerHitsSoft17 { get; init; }

    /// <summary>The Blackjack payout, one of <see cref="PayoutOptions"/>.</summary>
    public decimal BlackjackPayout { get; init; } = 1.5m;

    /// <summary>The penetration, one of <see cref="PenetrationOptions"/>.</summary>
    public decimal Penetration { get; init; } = 0.75m;

    /// <summary>The minimum bet, one of <see cref="MinBetOptions"/>.</summary>
    public decimal MinBet { get; init; } = 10m;

    /// <summary>The maximum bet, one of <see cref="MaxBetOptions"/>.</summary>
    public decimal MaxBet { get; init; } = 500m;

    /// <summary>Whether a hand from a split may be doubled.</summary>
    public bool DoubleAfterSplit { get; init; } = true;

    /// <summary>Whether split Aces may be split again.</summary>
    public bool ResplitAces { get; init; }

    /// <summary>The bankroll of a new game after Reset bankroll or Game over, one of <see cref="StartingBankrollOptions"/>.</summary>
    public decimal StartingBankroll { get; init; } = 1000m;

    /// <summary>The bankroll to continue with at the next start; <c>null</c> = the starting bankroll.</summary>
    public decimal? Bankroll { get; init; }

    /// <summary>Whether sounds are played.</summary>
    public bool IsSoundOn { get; init; } = true;

    /// <summary>The settings with every value that is not offered replaced by its default.</summary>
    public GameSettings Normalized() => this with
    {
        Decks = Decks is >= Rules.MinDecks and <= Rules.MaxDecks ? Decks : Default.Decks,
        BlackjackPayout = Offered(PayoutOptions, BlackjackPayout, Default.BlackjackPayout),
        Penetration = Offered(PenetrationOptions, Penetration, Default.Penetration),
        MinBet = Offered(MinBetOptions, MinBet, Default.MinBet),
        MaxBet = Offered(MaxBetOptions, MaxBet, Default.MaxBet),
        StartingBankroll = Offered(StartingBankrollOptions, StartingBankroll, Default.StartingBankroll),
        Bankroll = Bankroll >= 0 ? Bankroll : null,
    };

    /// <summary>The rules of these settings; rules without a setting (e.g. peek, split hands) come from <paramref name="baseRules"/>.</summary>
    public Rules ToRules(Rules baseRules)
    {
        ArgumentNullException.ThrowIfNull(baseRules);
        return baseRules with
        {
            Decks = Decks,
            DealerHitsSoft17 = DealerHitsSoft17,
            BlackjackPayout = BlackjackPayout,
            Penetration = Penetration,
            MinBet = MinBet,
            MaxBet = MaxBet,
            DoubleAfterSplit = DoubleAfterSplit,
            ResplitAces = ResplitAces,
        };
    }

    /// <summary>The settings for the given rules, bankrolls and Sound switch.</summary>
    public static GameSettings From(Rules rules, decimal startingBankroll, decimal? bankroll, bool isSoundOn)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return new GameSettings
        {
            Decks = rules.Decks,
            DealerHitsSoft17 = rules.DealerHitsSoft17,
            BlackjackPayout = rules.BlackjackPayout,
            Penetration = rules.Penetration,
            MinBet = rules.MinBet,
            MaxBet = rules.MaxBet,
            DoubleAfterSplit = rules.DoubleAfterSplit,
            ResplitAces = rules.ResplitAces,
            StartingBankroll = startingBankroll,
            Bankroll = bankroll,
            IsSoundOn = isSoundOn,
        };
    }

    private static decimal Offered(IReadOnlyList<decimal> options, decimal value, decimal fallback) =>
        options.Contains(value) ? value : fallback;
}
