using Avalonia.Input;
using Blackjack.Gui.Settings;
using Blackjack.Gui.ViewModels;
using Blackjack.Gui.Views;

namespace Blackjack.Gui.Tests;

/// <summary>Spec E11: strategy hints before a decision (Show) and feedback on deviations from basic strategy (Feedback).</summary>
public sealed class StrategyHintTests
{
    private static readonly IReadOnlySet<PlayerAction> All =
        new HashSet<PlayerAction> { PlayerAction.Hit, PlayerAction.Stand, PlayerAction.Double, PlayerAction.Split };

    private static readonly IReadOnlySet<PlayerAction> HitOrStand =
        new HashSet<PlayerAction> { PlayerAction.Hit, PlayerAction.Stand };

    // Cards are dealt player, dealer, player, dealer, then drawn in order.
    private static Table Start(string cards, HintMode hints)
    {
        var t = new Table(1000m, cards, "");
        t.ViewModel.Hints = hints;
        t.Do(t.ViewModel.Start);
        t.Execute(t.ViewModel.DealCommand);
        return t;
    }

    // --- Show ---

    [Fact]
    public void ShowNamesAndHighlightsTheBasicStrategyAction()
    {
        var vm = Start("6 6 5 10 10 5", HintMode.Show).ViewModel;

        Assert.Equal("Basic strategy: Double (hard 11 vs 6)", vm.Hint);
        Assert.False(vm.IsHintMistake);
        Assert.Equal(PlayerAction.Double, vm.RecommendedAction);
        Assert.True(vm.IsDoubleRecommended);
        Assert.False(vm.IsHitRecommended || vm.IsStandRecommended || vm.IsSplitRecommended);
    }

    [Fact]
    public void ShowEndsWithTheDecision()
    {
        var t = Start("6 6 5 10 10 5", HintMode.Show);
        var vm = t.ViewModel;

        t.Execute(vm.DoubleCommand);

        Assert.Equal(TablePhase.Betting, vm.Phase);
        Assert.Equal("", vm.Hint);
        Assert.Null(vm.RecommendedAction);
    }

    [Fact]
    public void ShowOnlyRecommendsAllowedActions()
    {
        var t = Start("3 6 2 10 6", HintMode.Show);
        var vm = t.ViewModel;
        Assert.Equal("Basic strategy: Hit (hard 5 vs 6)", vm.Hint);

        t.Execute(vm.HitCommand);

        Assert.Equal("Basic strategy: Hit (hard 11 vs 6)", vm.Hint);
        Assert.True(vm.IsHitRecommended, "three cards cannot be doubled");
    }

    [Fact]
    public void ShowAdvisesEveryHandAfterASplit()
    {
        var t = Start("8 10 8 7 3", HintMode.Show);
        var vm = t.ViewModel;
        Assert.Equal("Basic strategy: Split (pair of 8s vs 10)", vm.Hint);

        t.Execute(vm.SplitCommand);

        Assert.Equal("Basic strategy: Double (hard 11 vs 10)", vm.Hint);
        Assert.True(vm.IsDoubleRecommended, "double after split");
    }

    [Fact]
    public void OffShowsNothing()
    {
        var t = Start("10 10 6 7", HintMode.Off);
        var vm = t.ViewModel;
        Assert.Equal("", vm.Hint);
        Assert.Null(vm.RecommendedAction);

        t.Execute(vm.StandCommand);

        Assert.Equal("", vm.Hint);
    }

    // --- Feedback ---

    [Fact]
    public void FeedbackReportsADeviationUntilTheNextDeal()
    {
        var t = Start("10 10 6 7 10 9 9 8", HintMode.Feedback);
        var vm = t.ViewModel;
        Assert.Equal("", vm.Hint);
        Assert.Null(vm.RecommendedAction);

        t.Execute(vm.StandCommand);

        Assert.Equal(TablePhase.Betting, vm.Phase);
        Assert.Equal("Basic strategy: Hit – you stood (hard 16 vs 10)", vm.Hint);
        Assert.True(vm.IsHintMistake);

        t.Execute(vm.DealCommand);

        Assert.Equal("", vm.Hint);
        Assert.False(vm.IsHintMistake);
    }

    [Fact]
    public void FeedbackSaysNothingAfterTheBasicStrategyAction()
    {
        var t = Start("10 10 6 7 5", HintMode.Feedback);

        t.Execute(t.ViewModel.HitCommand);

        Assert.Equal("", t.ViewModel.Hint);
    }

    [Fact]
    public void FeedbackKeepsTheLastDeviationOfTheRound()
    {
        var t = Start("6 6 5 10 2 5", HintMode.Feedback);
        var vm = t.ViewModel;

        t.Execute(vm.HitCommand);
        Assert.Equal("Basic strategy: Double – you hit (hard 11 vs 6)", vm.Hint);

        // Hard 13 vs 6: Stand is right, the deviation stays visible.
        t.Execute(vm.StandCommand);
        Assert.Equal("Basic strategy: Double – you hit (hard 11 vs 6)", vm.Hint);
    }

    [Fact]
    public void FeedbackAlsoJudgesTheKeyboard()
    {
        var t = Start("10 10 6 7", HintMode.Feedback);

        t.Do(() => KeyboardShortcuts.Handle(t.ViewModel, Key.S, KeyModifiers.None));

        Assert.Equal("Basic strategy: Hit – you stood (hard 16 vs 10)", t.ViewModel.Hint);
    }

    // --- Texts ---

    [Theory]
    [InlineData("A 7", "A", "soft 18 vs A")]
    [InlineData("A A", "6", "pair of Aces vs 6")]
    [InlineData("8 8", "K", "pair of 8s vs 10")]
    [InlineData("K 10", "5", "pair of 10s vs 5")]
    [InlineData("9 2", "7", "hard 11 vs 7")]
    public void TheSituationNamesHandAndUpcard(string cards, string upcard, string expected) =>
        Assert.Equal(expected, DisplayText.Situation(Context(cards, upcard, All)));

    [Fact]
    public void APairThatCannotBeSplitIsNamedByItsTotal() =>
        Assert.Equal("hard 16 vs 10", DisplayText.Situation(Context("8 8", "10", HitOrStand)));

    [Theory]
    [InlineData(PlayerAction.Hit, "you hit")]
    [InlineData(PlayerAction.Stand, "you stood")]
    [InlineData(PlayerAction.Double, "you doubled")]
    [InlineData(PlayerAction.Split, "you split")]
    public void TheFeedbackNamesTheChosenAction(PlayerAction chosen, string expected) =>
        Assert.Equal($"Basic strategy: Stand – {expected} (soft 18 vs A)",
                     DisplayText.StrategyFeedback(PlayerAction.Stand, chosen, Context("A 7", "A", All)));

    // --- Setting ---

    [Fact]
    public void TheHintsAreSavedAndApplied()
    {
        var t = Table.Start("10 9 9 8");
        var vm = t.ViewModel;

        vm.Hints = HintMode.Show;
        Assert.Equal(HintMode.Show, t.SavedSettings[^1].Hints);

        t.Do(() => vm.ApplySettings(vm.CurrentSettings with { Hints = HintMode.Feedback }, resetBankroll: false));
        Assert.Equal(HintMode.Feedback, vm.Hints);
        Assert.Equal(HintMode.Feedback, t.SavedSettings[^1].Hints);
    }

    [Fact]
    public void TheHintsAreOffByDefault() => Assert.Equal(HintMode.Off, GameSettings.Default.Hints);

    [Fact]
    public void AHintModeThatIsNotOfferedFallsBackToOff() =>
        Assert.Equal(HintMode.Off, (GameSettings.Default with { Hints = (HintMode)7 }).Normalized().Hints);

    [Fact]
    public void TheDialogOffersTheHints()
    {
        var dialog = new SettingsViewModel(GameSettings.Default with { Hints = HintMode.Feedback }, 1000m, rulesApplyNow: true);

        Assert.True(dialog.AreHintsAvailable);
        Assert.Equal(["Off", "Show the basic strategy action", "Tell me when I deviate"], dialog.HintOptions.Select(o => o.Text));
        Assert.Equal(HintMode.Feedback, dialog.Hints.Value);

        dialog.Hints = dialog.HintOptions[1];
        Assert.Equal(HintMode.Show, dialog.ToSettings().Hints);
    }

    private static DecisionContext Context(string cards, string upcard, IReadOnlySet<PlayerAction> allowed)
    {
        var hand = new Hand(Cards.Parse(cards));
        return new DecisionContext(hand.Cards.ToArray(), hand.Total, hand.IsSoft, Cards.ParseOne(upcard), allowed);
    }
}
