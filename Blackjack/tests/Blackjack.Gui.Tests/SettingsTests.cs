using Blackjack.Gui.Settings;
using Blackjack.Gui.ViewModels;

namespace Blackjack.Gui.Tests;

/// <summary>Spec E9: the settings file, the settings dialog and how the table applies and saves the settings.</summary>
public sealed class SettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "blackjack-settings-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string FilePath => Path.Combine(_directory, "Blackjack", "settings.json");

    // --- Settings file ---

    [Fact]
    public void SavedSettingsAreLoadedAgain()
    {
        var store = new SettingsStore(FilePath);
        var settings = GameSettings.Default with
        {
            Decks = 2, DealerHitsSoft17 = true, BlackjackPayout = 1.2m, Penetration = 0.67m, MinBet = 25m, MaxBet = 1000m,
            DoubleAfterSplit = false, ResplitAces = true, StartingBankroll = 5000m, Bankroll = 1234.5m, IsSoundOn = false,
            Hints = HintMode.Feedback,
        };

        Assert.True(store.Save(settings));

        Assert.Equal(settings, new SettingsStore(FilePath).Load());
        Assert.False(File.Exists(FilePath + ".tmp"), "the temporary file is replaced");
    }

    [Fact]
    public void WithoutAFileTheDefaultsApply()
    {
        Assert.Equal(GameSettings.Default, new SettingsStore(FilePath).Load());
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{ "Decks": "six" }""")]
    [InlineData("null")]
    public void AnUnreadableFileGivesTheDefaults(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, content);

        Assert.Equal(GameSettings.Default, new SettingsStore(FilePath).Load());
    }

    [Fact]
    public void ValuesThatAreNotOfferedFallBackToTheirDefaultOneByOne()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, """
            { "Decks": 12, "MinBet": 7, "MaxBet": 250, "BlackjackPayout": 2, "Penetration": 0.9,
              "StartingBankroll": 3, "Bankroll": -5, "DealerHitsSoft17": true, "Unknown": 1 }
            """);

        var settings = new SettingsStore(FilePath).Load();

        Assert.Equal(GameSettings.Default with { MaxBet = 250m, DealerHitsSoft17 = true }, settings);
    }

    [Fact]
    public void ASaveThatFailsReturnsFalse()
    {
        // A file where the folder should be.
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "Blackjack"), "");

        Assert.False(new SettingsStore(FilePath).Save(GameSettings.Default));
    }

    [Fact]
    public void TheDefaultSettingsHaveTheDefaultRules()
    {
        Assert.Equal(Rules.Default, GameSettings.Default.ToRules(Rules.Default));
        Assert.Equal(GameSettings.Default, GameSettings.From(Rules.Default, 1000m, null, isSoundOn: true));
    }

    [Fact]
    public void EveryCombinationOfBetLimitsIsValid()
    {
        Assert.True(GameSettings.MinBetOptions.Max() <= GameSettings.MaxBetOptions.Min());
    }

    // --- Bankroll ---

    [Fact]
    public void TheSavedBankrollIsContinued()
    {
        var t = new Table(Rules.Default, 1000m, 750m, "10 9 9 8");
        var vm = t.ViewModel;
        t.Do(vm.Start);

        Assert.Equal(750m, vm.Bankroll);
        t.Execute(vm.DealCommand);
        t.Execute(vm.StandCommand);

        Assert.Equal(760m, vm.Bankroll);
        Assert.Equal(760m, t.SavedSettings[^1].Bankroll);
    }

    [Fact]
    public void ASavedBankrollBelowTheMinimumBetStartsWithTheStartingBankroll()
    {
        var t = new Table(Rules.Default, 1000m, 5m, "");
        t.Do(t.ViewModel.Start);

        Assert.Equal(1000m, t.ViewModel.Bankroll);
    }

    [Fact]
    public void TheBankrollIsSavedAfterEachRound()
    {
        var t = Table.Start("10 9 9 8 10 9 6 8");
        var vm = t.ViewModel;
        t.Execute(vm.DealCommand);
        t.Execute(vm.StandCommand);
        Assert.Equal(1010m, t.SavedSettings[^1].Bankroll);

        t.Execute(vm.DealCommand);
        t.Execute(vm.StandCommand);
        Assert.Equal(1000m, t.SavedSettings[^1].Bankroll);
    }

    [Fact]
    public void GameOverSavesTheStartingBankrollAndNewGameStartsWithIt()
    {
        var t = new Table(Rules.Default, 1000m, 10m, "10 9 6 8", "");
        var vm = t.ViewModel;
        t.Do(vm.Start);
        t.Execute(vm.DealCommand);
        t.Execute(vm.StandCommand);

        Assert.Equal(TablePhase.GameOver, vm.Phase);
        Assert.Equal(1000m, t.SavedSettings[^1].Bankroll);

        t.Execute(vm.NewGameCommand);

        Assert.Equal(1000m, vm.Bankroll);
    }

    [Fact]
    public void ResetBankrollAfterRoundsEndsTheSessionAndStartsWithTheStartingBankroll()
    {
        var t = new Table(1000m, "10 9 9 8", "A K Q J");
        var vm = t.ViewModel;
        t.Do(vm.Start);
        t.Execute(vm.DealCommand);
        t.Execute(vm.StandCommand);
        Assert.Equal(1010m, vm.Bankroll);

        t.Do(() => vm.ApplySettings(vm.CurrentSettings, resetBankroll: true));

        Assert.Equal(TablePhase.Betting, vm.Phase);
        Assert.Equal(1000m, vm.Bankroll);
        Assert.Equal(0, vm.Statistics.Rounds);
        Assert.Single(t.SavedLogs);
        Assert.Equal("Shoe: 4 / 4", vm.ShoeText);
        Assert.Empty(vm.PlayerHands);
        Assert.Equal(1000m, t.SavedSettings[^1].Bankroll);
        Assert.StartsWith(
            "Bankroll reset. Rounds: 1, wins: 1, losses: 0, pushes: 0, net: +10. Statistics saved to log/test.txt. Place your bet",
            vm.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetBankrollBeforeTheFirstRound()
    {
        var t = new Table(Rules.Default, 1000m, 750m, "10 9 9 8", "");
        var vm = t.ViewModel;
        t.Do(vm.Start);

        t.Do(() => vm.ApplySettings(vm.CurrentSettings with { StartingBankroll = 2000m }, resetBankroll: true));

        Assert.Equal(2000m, vm.Bankroll);
        Assert.Empty(t.SavedLogs);
        Assert.StartsWith("Bankroll reset. Place your bet", vm.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetBankrollAfterGameOverStartsANewGameAtOnce()
    {
        var t = new Table(Rules.Default, 1000m, 10m, "10 9 6 8", "");
        var vm = t.ViewModel;
        t.Do(vm.Start);
        t.Execute(vm.DealCommand);
        t.Execute(vm.StandCommand);
        Assert.Equal(TablePhase.GameOver, vm.Phase);

        t.Do(() => vm.ApplySettings(vm.CurrentSettings, resetBankroll: true));

        Assert.Equal(TablePhase.Betting, vm.Phase);
        Assert.Equal(1000m, vm.Bankroll);
    }

    [Fact]
    public void AChangedStartingBankrollDoesNotChangeTheCurrentBankroll()
    {
        var t = Table.Start("10 9 9 8");
        var vm = t.ViewModel;

        t.Do(() => vm.ApplySettings(vm.CurrentSettings with { StartingBankroll = 5000m }, resetBankroll: false));

        Assert.Equal(1000m, vm.Bankroll);
        Assert.Single(t.UsedRules);
        Assert.Equal(5000m, t.SavedSettings[^1].StartingBankroll);
    }

    [Fact]
    public void TheSettingsAreOnlyOpenBetweenRounds()
    {
        var t = Table.Start("10 9 9 8");
        var vm = t.ViewModel;
        Assert.True(vm.IsBetweenRounds);

        t.Execute(vm.DealCommand);
        Assert.False(vm.IsBetweenRounds);

        t.Execute(vm.StandCommand);
        Assert.True(vm.IsBetweenRounds);
    }

    // --- Rules ---

    [Fact]
    public void ChangedRulesApplyAtOnceBeforeTheFirstRound()
    {
        var t = new Table(1000m, "10 9 9 8", "10 9 9 8");
        var vm = t.ViewModel;
        t.Do(vm.Start);

        var settings = vm.CurrentSettings with
        {
            Decks = 2, DealerHitsSoft17 = true, BlackjackPayout = 1.2m, Penetration = 0.5m, MinBet = 25m, MaxBet = 100m,
            DoubleAfterSplit = false, ResplitAces = true,
        };
        t.Do(() => vm.ApplySettings(settings, resetBankroll: false));

        var rules = t.UsedRules[^1];
        Assert.Equal(2, t.UsedRules.Count);
        Assert.Equal(
            Rules.Default with
            {
                Decks = 2, DealerHitsSoft17 = true, BlackjackPayout = 1.2m, Penetration = 0.5m, MinBet = 25m, MaxBet = 100m,
                DoubleAfterSplit = false, ResplitAces = true,
            },
            rules);
        Assert.Equal(2, vm.Decks);
        Assert.Equal("BLACKJACK PAYS 6 TO 5", vm.PayoutPrint);
        Assert.Equal("Dealer Must Hit on Soft 17", vm.DealerRulePrint);
        Assert.Equal(TableViewModel.ShoeBarWidth * 0.5, vm.ReshuffleMarkerLeft);
        Assert.Equal(25m, vm.Bet);
        Assert.Equal(TablePhase.Betting, vm.Phase);
        Assert.Equal(settings, t.SavedSettings[^1]);
    }

    [Fact]
    public void ChangedRulesApplyToTheNextGameAfterARound()
    {
        var t = new Table(1000m, "10 9 9 8", "10 9 9 8");
        var vm = t.ViewModel;
        t.Do(vm.Start);
        t.Execute(vm.DealCommand);
        t.Execute(vm.StandCommand);

        t.Do(() => vm.ApplySettings(vm.CurrentSettings with { BlackjackPayout = 1.2m }, resetBankroll: false));

        Assert.Single(t.UsedRules);
        Assert.Equal("BLACKJACK PAYS 3 TO 2", vm.PayoutPrint);
        Assert.Equal(1.2m, t.SavedSettings[^1].BlackjackPayout);

        t.Execute(vm.LeaveTableCommand);
        t.Execute(vm.NewGameCommand);

        Assert.Equal(1.2m, t.UsedRules[^1].BlackjackPayout);
        Assert.Equal("BLACKJACK PAYS 6 TO 5", vm.PayoutPrint);
        Assert.Equal(1010m, vm.Bankroll);
    }

    [Fact]
    public void TheChosenDecksAreSaved()
    {
        var t = Table.Start("10 9 9 8");

        t.Do(() => t.ViewModel.Decks = 3);

        Assert.Equal(3, t.SavedSettings[^1].Decks);
    }

    [Fact]
    public void TheSoundSwitchIsSavedAndSet()
    {
        var t = Table.Start("10 9 9 8");
        var vm = t.ViewModel;

        vm.IsSoundOn = false;
        Assert.False(t.SavedSettings[^1].IsSoundOn);

        t.Do(() => vm.ApplySettings(vm.CurrentSettings with { IsSoundOn = true }, resetBankroll: false));
        Assert.True(vm.IsSoundOn);
        Assert.True(t.SavedSettings[^1].IsSoundOn);
    }

    // --- Dialog ---

    [Fact]
    public void TheDialogShowsTheSettings()
    {
        var settings = GameSettings.Default with { BlackjackPayout = 1.2m, Penetration = 0.67m, DealerHitsSoft17 = true, Bankroll = 750m };

        var dialog = new SettingsViewModel(settings, 750m, rulesApplyNow: false);

        Assert.Equal("6:5", dialog.Payout.Text);
        Assert.Equal("67 % of the shoe", dialog.Penetration.Text);
        Assert.Equal("Hits soft 17 (H17)", dialog.DealerRule.Text);
        Assert.Equal("Current bankroll: 750", dialog.BankrollText);
        Assert.Equal("Changed table rules apply to the next game.", dialog.RulesNote);
        Assert.Equal(["3:2", "6:5"], dialog.PayoutOptions.Select(o => o.Text));
        Assert.Equal(settings, dialog.ToSettings());
    }

    [Fact]
    public void TheDialogReturnsTheChosenSettingsAndKeepsTheBankroll()
    {
        var dialog = new SettingsViewModel(GameSettings.Default with { Bankroll = 750m }, 750m, rulesApplyNow: true)
        {
            Decks = 4,
        };
        dialog.MinBet = dialog.MinBetOptions[^1];
        dialog.StartingBankroll = dialog.StartingBankrollOptions[0];
        dialog.IsSoundOn = false;

        var settings = dialog.ToSettings();

        Assert.Equal("Changed table rules apply at once.", dialog.RulesNote);
        Assert.Equal(GameSettings.Default with { Decks = 4, MinBet = 25m, StartingBankroll = 500m, IsSoundOn = false, Bankroll = 750m }, settings);
    }
}
