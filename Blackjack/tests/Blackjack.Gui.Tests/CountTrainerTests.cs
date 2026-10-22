using Avalonia.Input;
using Blackjack.Gui.Settings;
using Blackjack.Gui.ViewModels;
using Blackjack.Gui.Views;

namespace Blackjack.Gui.Tests;

/// <summary>
/// Spec E13: the count trainer of the GUI. Deal order: player, dealer, player, hole card. The fixed shoes are short,
/// so few decks are left and the true count is high (running count × 52 / cards left).
/// </summary>
public sealed class CountTrainerTests
{
    private static Table Start(string cards, CountTrainerMode trainer, HintMode hints = HintMode.Off, bool deal = true)
    {
        var t = new Table(1000m, cards, "");
        t.ViewModel.CountTrainer = trainer;
        t.ViewModel.Hints = hints;
        t.Do(t.ViewModel.Start);
        if (deal)
        {
            t.Execute(t.ViewModel.DealCommand);
        }

        return t;
    }

    private static void Rounds(Table t, int count)
    {
        for (var i = 0; i < count; i++)
        {
            t.Execute(t.ViewModel.DealCommand);
            t.Execute(t.ViewModel.StandCommand);
        }
    }

    // --- Show the count ---

    [Fact]
    public void TheCountFollowsTheVisibleCards()
    {
        // Player 5 4, dealer 6 + hole 10, then the dealer draws 9.
        var t = Start("5 6 4 10 9", CountTrainerMode.ShowCount);
        var vm = t.ViewModel;
        Assert.True(vm.IsCountShown);
        Assert.Equal("Running count: +3", vm.RunningCountText);

        t.Execute(vm.StandCommand);

        Assert.Equal("Running count: +2", vm.RunningCountText);
        Assert.Equal("Decks left: 0.0", vm.DecksLeftText);
    }

    [Fact]
    public void TheTrueCountAndTheCountBet()
    {
        var vm = Start("5 6 4 10 9 2 2 2 2 2", CountTrainerMode.ShowCount).ViewModel;

        // Running count +3 with 6 cards left: 3 × 52 / 6 = +26.
        Assert.Equal("True count: +26.0", vm.TrueCountText);
        Assert.Equal("Count bet: 8 units = 80", vm.CountBetText);
    }

    [Fact]
    public void AFreshShoeCountsFromZero()
    {
        var vm = Start("", CountTrainerMode.ShowCount, deal: false).ViewModel;

        Assert.Equal("Running count: 0", vm.RunningCountText);
        Assert.Equal("True count: 0.0", vm.TrueCountText);
        Assert.Equal("Count bet: 1 unit = 10", vm.CountBetText);
    }

    [Fact]
    public void ANewGameStartsANewCount()
    {
        var t = new Table(1000m, "5 6 4 10 9", "10 9 9 8") { };
        var vm = t.ViewModel;
        vm.CountTrainer = CountTrainerMode.ShowCount;
        t.Do(vm.Start);
        t.Execute(vm.DealCommand);
        t.Execute(vm.StandCommand);
        Assert.Equal(2, vm.Counter.RunningCount);

        t.Execute(vm.LeaveTableCommand);
        t.Execute(vm.NewGameCommand);

        Assert.Equal(0, vm.Counter.RunningCount);
    }

    [Fact]
    public void AShuffleStartsAgainAtZero()
    {
        // The engine checks the shoe right after a round, before it asks for the next bet.
        var t = Start("5 6 4 10 9 10 9 9 8", CountTrainerMode.ShowCount);
        t.LastSource!.NeedsReshuffle = true;
        t.Execute(t.ViewModel.StandCommand);
        Assert.Equal("Running count: 0", t.ViewModel.RunningCountText);

        t.Execute(t.ViewModel.DealCommand);

        Assert.Equal(1, t.LastSource.ShuffleCount);
        Assert.Equal("Running count: -1", t.ViewModel.RunningCountText);
    }

    [Theory]
    [InlineData(CountTrainerMode.Off)]
    [InlineData(CountTrainerMode.Quiz)]
    public void OtherwiseTheCountIsHidden(CountTrainerMode trainer) =>
        Assert.False(Start("5 6 4 10 9", trainer).ViewModel.IsCountShown);

    // --- Hints with the index plays ---

    [Fact]
    public void ShowNamesTheCountPlay()
    {
        // Player 5 5 (a pair, played as 10) vs 10 at running count +1, 2 cards left: true count +26 ≥ +4, so Double.
        var vm = Start("5 10 5 7 6 2", CountTrainerMode.ShowCount, HintMode.Show).ViewModel;

        Assert.Equal("Count play: Double (pair of 5s vs 10, true count +26.0 ≥ +4)", vm.Hint);
        Assert.True(vm.IsDoubleRecommended);
    }

    [Fact]
    public void FeedbackReportsAMissedCountPlay()
    {
        var t = Start("5 10 5 7 6 2", CountTrainerMode.ShowCount, HintMode.Feedback);

        t.Execute(t.ViewModel.HitCommand);

        Assert.Equal("Count play: Double – you hit (pair of 5s vs 10, true count +26.0 ≥ +4)", t.ViewModel.Hint);
    }

    [Fact]
    public void WithoutAnIndexPlayTheHintIsBasicStrategy() =>
        Assert.Equal("Basic strategy: Stand (hard 19 vs 8)", Start("10 8 9 8", CountTrainerMode.ShowCount, HintMode.Show).ViewModel.Hint);

    [Fact]
    public void TheQuizDoesNotGiveTheCountAwayInHints() =>
        Assert.Equal("Basic strategy: Hit (pair of 5s vs 10)", Start("5 10 5 7 6 2", CountTrainerMode.Quiz, HintMode.Show).ViewModel.Hint);

    [Fact]
    public void ShowRecommendsInsuranceAtAHighCount()
    {
        // Player 5 6 vs Ace at running count +1, 2 cards left.
        var vm = Start("5 A 6 9 2 3", CountTrainerMode.ShowCount, HintMode.Show).ViewModel;

        Assert.True(vm.IsInsuranceOpen);
        Assert.Equal("Count play: Insurance (true count +26.0 ≥ +3)", vm.Hint);
        Assert.True(vm.IsTakeInsuranceRecommended);
        Assert.False(vm.IsNoInsuranceRecommended);
    }

    [Fact]
    public void FeedbackReportsADeclinedCountInsurance()
    {
        var t = Start("5 A 6 9 2 3", CountTrainerMode.ShowCount, HintMode.Feedback);

        t.Execute(t.ViewModel.DeclineInsuranceCommand);

        Assert.Equal("Count play: Insurance – you declined (true count +26.0 ≥ +3)", t.ViewModel.Hint);
    }

    [Fact]
    public void TakingTheCountInsuranceIsNoMistake()
    {
        var t = Start("5 A 6 9 2 3", CountTrainerMode.ShowCount, HintMode.Feedback);

        t.Execute(t.ViewModel.TakeInsuranceCommand);

        Assert.Equal("", t.ViewModel.Hint);
    }

    // --- Quiz ---

    private const string FiveRounds = "10 9 9 8  10 9 9 8  10 9 9 8  10 9 9 8  10 9 9 8";

    [Fact]
    public void TheQuizAsksAfterEveryFifthRound()
    {
        var t = Start(FiveRounds, CountTrainerMode.Quiz, deal: false);
        var vm = t.ViewModel;

        Rounds(t, 4);
        Assert.False(vm.IsQuizOpen);

        Rounds(t, 1);

        Assert.True(vm.IsQuizOpen);
        Assert.Equal("What is the running count?", vm.QuizQuestion);
        Assert.False(vm.DealCommand.CanExecute(null) || vm.AddChipCommand.CanExecute(5) || vm.LeaveTableCommand.CanExecute(null));
        Assert.False(vm.IsBetweenRounds);
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("−5")]
    [InlineData(" -5 ")]
    public void ACorrectAnswer(string answer)
    {
        var t = Start(FiveRounds, CountTrainerMode.Quiz, deal: false);
        var vm = t.ViewModel;
        Rounds(t, 5);

        vm.QuizInput = answer;
        t.Execute(vm.SubmitQuizCommand);

        Assert.False(vm.IsQuizOpen);
        Assert.StartsWith("Count quiz: correct, the running count is -5. Place your bet", vm.Status, StringComparison.Ordinal);
        Assert.Equal((1, 1), (vm.QuizAnswered, vm.QuizCorrect));
        Assert.True(vm.DealCommand.CanExecute(null));
    }

    [Fact]
    public void AWrongAnswer()
    {
        var t = Start(FiveRounds, CountTrainerMode.Quiz, deal: false);
        var vm = t.ViewModel;
        Rounds(t, 5);

        vm.QuizInput = "+2";
        t.Execute(vm.SubmitQuizCommand);

        Assert.StartsWith("Count quiz: the running count is -5, you said +2.", vm.Status, StringComparison.Ordinal);
        Assert.Equal((1, 0), (vm.QuizAnswered, vm.QuizCorrect));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.5")]
    public void AnAnswerThatIsNoWholeNumberIsAskedAgain(string answer)
    {
        var t = Start(FiveRounds, CountTrainerMode.Quiz, deal: false);
        var vm = t.ViewModel;
        Rounds(t, 5);

        vm.QuizInput = answer;
        t.Execute(vm.SubmitQuizCommand);

        Assert.True(vm.IsQuizOpen);
        Assert.Equal("Please enter a whole number, e.g. +4 or -3.", vm.QuizError);
        Assert.Equal(0, vm.QuizAnswered);
    }

    [Fact]
    public void AtTheEndOfTheShoeTheQuizAsksForTheCountBeforeTheShuffle()
    {
        var t = Start("5 6 4 10 9 10 9 9 8", CountTrainerMode.Quiz);
        var vm = t.ViewModel;
        t.LastSource!.NeedsReshuffle = true;

        t.Execute(vm.StandCommand);

        Assert.True(vm.IsQuizOpen);
        Assert.Equal("End of the shoe: what was the running count before the shuffle?", vm.QuizQuestion);
        Assert.Equal(0, vm.Counter.RunningCount);

        vm.QuizInput = "+2";
        t.Execute(vm.SubmitQuizCommand);

        Assert.StartsWith("Count quiz: correct, the running count was +2.", vm.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void TheChipKeysDoNothingWhileTheQuizIsOpen()
    {
        var t = Start(FiveRounds, CountTrainerMode.Quiz, deal: false);
        Rounds(t, 5);
        var bet = t.ViewModel.Bet;

        t.Do(() => KeyboardShortcuts.Handle(t.ViewModel, Key.D4, KeyModifiers.None));

        Assert.Equal(bet, t.ViewModel.Bet);
    }

    [Theory]
    [InlineData(CountTrainerMode.Off)]
    [InlineData(CountTrainerMode.ShowCount)]
    public void OnlyTheQuizModeAsks(CountTrainerMode trainer)
    {
        var t = Start(FiveRounds, trainer, deal: false);

        Rounds(t, 5);

        Assert.False(t.ViewModel.IsQuizOpen);
    }

    // --- Setting ---

    [Fact]
    public void TheCountTrainerIsSavedAndApplied()
    {
        var t = Table.Start("10 9 9 8");
        var vm = t.ViewModel;

        vm.CountTrainer = CountTrainerMode.Quiz;
        Assert.Equal(CountTrainerMode.Quiz, t.SavedSettings[^1].CountTrainer);

        t.Do(() => vm.ApplySettings(vm.CurrentSettings with { CountTrainer = CountTrainerMode.ShowCount }, resetBankroll: false));
        Assert.Equal(CountTrainerMode.ShowCount, vm.CountTrainer);
        Assert.True(vm.IsCountShown);
    }

    [Fact]
    public void TheCountTrainerIsOffByDefaultAndFallsBackToOff()
    {
        Assert.Equal(CountTrainerMode.Off, GameSettings.Default.CountTrainer);
        Assert.Equal(CountTrainerMode.Off, (GameSettings.Default with { CountTrainer = (CountTrainerMode)9 }).Normalized().CountTrainer);
    }

    [Fact]
    public void TheDialogOffersTheCountTrainer()
    {
        var dialog = new SettingsViewModel(GameSettings.Default with { CountTrainer = CountTrainerMode.Quiz }, 1000m, rulesApplyNow: true);

        Assert.Equal(["Off", "Show the count", "Quiz me on the count"], dialog.CountTrainerOptions.Select(o => o.Text));
        Assert.Equal(CountTrainerMode.Quiz, dialog.CountTrainer.Value);

        dialog.CountTrainer = dialog.CountTrainerOptions[1];
        Assert.Equal(CountTrainerMode.ShowCount, dialog.ToSettings().CountTrainer);
    }
}
