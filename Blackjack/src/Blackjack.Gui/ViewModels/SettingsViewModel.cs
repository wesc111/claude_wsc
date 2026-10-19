using Blackjack.Gui.Settings;

namespace Blackjack.Gui.ViewModels;

/// <summary>The settings dialog (spec E9): an editable copy of the settings, applied by the table only on OK.</summary>
public sealed class SettingsViewModel
{
    private readonly GameSettings _settings;

    /// <summary>Creates the dialog for the current settings.</summary>
    /// <param name="settings">The current settings of the table.</param>
    /// <param name="bankroll">The bankroll shown in the dialog.</param>
    /// <param name="rulesApplyNow">Whether changed rules apply at once (no round played yet) or to the next game.</param>
    public SettingsViewModel(GameSettings settings, decimal bankroll, bool rulesApplyNow)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        Decks = settings.Decks;
        DealerRule = Find(DealerRuleOptions, settings.DealerHitsSoft17);
        Payout = Find(PayoutOptions, settings.BlackjackPayout);
        Penetration = Find(PenetrationOptions, settings.Penetration);
        MinBet = Find(MinBetOptions, settings.MinBet);
        MaxBet = Find(MaxBetOptions, settings.MaxBet);
        DoubleAfterSplit = settings.DoubleAfterSplit;
        ResplitAces = settings.ResplitAces;
        StartingBankroll = Find(StartingBankrollOptions, settings.StartingBankroll);
        IsSoundOn = settings.IsSoundOn;
        BankrollText = $"Current bankroll: {DisplayText.Money(bankroll)}";
        RulesNote = rulesApplyNow
            ? "Changed table rules apply at once."
            : "Changed table rules apply to the next game.";
    }

    /// <summary>The selectable numbers of decks.</summary>
    public IReadOnlyList<int> DeckOptions { get; } = [.. Enumerable.Range(Rules.MinDecks, Rules.MaxDecks - Rules.MinDecks + 1)];

    /// <summary>S17 or H17.</summary>
    public IReadOnlyList<SettingOption<bool>> DealerRuleOptions { get; } =
        [new(false, "Stands on soft 17 (S17)"), new(true, "Hits soft 17 (H17)")];

    /// <summary>3:2 or 6:5.</summary>
    public IReadOnlyList<SettingOption<decimal>> PayoutOptions { get; } =
        [.. GameSettings.PayoutOptions.Select(p => new SettingOption<decimal>(p, PayoutText(p)))];

    /// <summary>The reshuffle marks in percent.</summary>
    public IReadOnlyList<SettingOption<decimal>> PenetrationOptions { get; } =
        [.. GameSettings.PenetrationOptions.Select(p => new SettingOption<decimal>(p, $"{DisplayText.Money(p * 100)} % of the shoe"))];

    /// <summary>The minimum bets.</summary>
    public IReadOnlyList<SettingOption<decimal>> MinBetOptions { get; } = MoneyOptions(GameSettings.MinBetOptions);

    /// <summary>The maximum bets.</summary>
    public IReadOnlyList<SettingOption<decimal>> MaxBetOptions { get; } = MoneyOptions(GameSettings.MaxBetOptions);

    /// <summary>The starting bankrolls.</summary>
    public IReadOnlyList<SettingOption<decimal>> StartingBankrollOptions { get; } = MoneyOptions(GameSettings.StartingBankrollOptions);

    /// <summary>Number of decks.</summary>
    public int Decks { get; set; }

    /// <summary>The dealer's soft 17 rule.</summary>
    public SettingOption<bool> DealerRule { get; set; }

    /// <summary>The Blackjack payout.</summary>
    public SettingOption<decimal> Payout { get; set; }

    /// <summary>The penetration.</summary>
    public SettingOption<decimal> Penetration { get; set; }

    /// <summary>The minimum bet.</summary>
    public SettingOption<decimal> MinBet { get; set; }

    /// <summary>The maximum bet.</summary>
    public SettingOption<decimal> MaxBet { get; set; }

    /// <summary>Double after split.</summary>
    public bool DoubleAfterSplit { get; set; }

    /// <summary>Re-split Aces.</summary>
    public bool ResplitAces { get; set; }

    /// <summary>The starting bankroll.</summary>
    public SettingOption<decimal> StartingBankroll { get; set; }

    /// <summary>The Sound switch.</summary>
    public bool IsSoundOn { get; set; }

    /// <summary>The current bankroll, e.g. <c>Current bankroll: 1010</c>.</summary>
    public string BankrollText { get; }

    /// <summary>When changed table rules take effect.</summary>
    public string RulesNote { get; }

    /// <summary>The settings as chosen in the dialog; the saved bankroll is kept.</summary>
    public GameSettings ToSettings() => _settings with
    {
        Decks = Decks,
        DealerHitsSoft17 = DealerRule.Value,
        BlackjackPayout = Payout.Value,
        Penetration = Penetration.Value,
        MinBet = MinBet.Value,
        MaxBet = MaxBet.Value,
        DoubleAfterSplit = DoubleAfterSplit,
        ResplitAces = ResplitAces,
        StartingBankroll = StartingBankroll.Value,
        IsSoundOn = IsSoundOn,
    };

    /// <summary>The payout as a ratio, e.g. <c>3:2</c>.</summary>
    private static string PayoutText(decimal payout) =>
        DisplayText.PayoutPrint(payout).Replace("BLACKJACK PAYS ", "", StringComparison.Ordinal).Replace(" TO ", ":", StringComparison.Ordinal);

    private static SettingOption<decimal>[] MoneyOptions(IEnumerable<decimal> values) =>
        [.. values.Select(v => new SettingOption<decimal>(v, DisplayText.Money(v)))];

    // Values outside the lists cannot occur after GameSettings.Normalized; the first option is the fallback.
    private static SettingOption<T> Find<T>(IReadOnlyList<SettingOption<T>> options, T value) =>
        options.FirstOrDefault(o => EqualityComparer<T>.Default.Equals(o.Value, value)) ?? options[0];
}
