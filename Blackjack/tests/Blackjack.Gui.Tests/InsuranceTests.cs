using Avalonia.Input;
using Blackjack.Gui.Settings;
using Blackjack.Gui.ViewModels;
using Blackjack.Gui.Views;

namespace Blackjack.Gui.Tests;

/// <summary>Spec E12: the insurance box of the GUI (deal order: player, dealer, player, hole card).</summary>
public sealed class InsuranceTests
{
    private static Table Deal(string cards, HintMode hints = HintMode.Off, Rules? rules = null)
    {
        var t = new Table(rules ?? Rules.Default, 1000m, null, cards, "");
        t.ViewModel.Hints = hints;
        t.Do(t.ViewModel.Start);
        t.Execute(t.ViewModel.DealCommand);
        return t;
    }

    [Fact]
    public void AnAceOpensTheInsuranceBox()
    {
        var vm = Deal("10 A 7 9").ViewModel;

        Assert.Equal(TablePhase.Insurance, vm.Phase);
        Assert.True(vm.IsInsuranceOpen);
        Assert.False(vm.IsEvenMoney);
        Assert.Equal("Insurance for 5?", vm.InsuranceQuestion);
        Assert.Equal("Insurance pays 2 to 1 if the dealer has Blackjack.", vm.InsuranceNote);
        Assert.Equal(("Insurance", "No insurance"), (vm.TakeInsuranceText, vm.DeclineInsuranceText));
        Assert.Equal("Insurance?", vm.Status);
    }

    [Fact]
    public void WhileTheBoxIsOpenNothingElseCanBeDone()
    {
        var vm = Deal("10 A 7 9").ViewModel;

        Assert.True(vm.TakeInsuranceCommand.CanExecute(null));
        Assert.True(vm.DeclineInsuranceCommand.CanExecute(null));
        Assert.False(vm.HitCommand.CanExecute(null) || vm.StandCommand.CanExecute(null) || vm.DealCommand.CanExecute(null));
        Assert.False(vm.LeaveTableCommand.CanExecute(null) || vm.ClearBetCommand.CanExecute(null) || vm.AddChipCommand.CanExecute(5));
        Assert.False(vm.IsBetweenRounds);
    }

    [Theory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    [InlineData(Key.Escape)]
    [InlineData(Key.I)]
    [InlineData(Key.S)]
    public void NoKeyAnswersTheBox(Key key)
    {
        var t = Deal("10 A 7 9");

        t.Do(() => KeyboardShortcuts.Handle(t.ViewModel, key, KeyModifiers.None));

        Assert.Equal(TablePhase.Insurance, t.ViewModel.Phase);
    }

    [Fact]
    public void OtherUpcardsOpenNoBox()
    {
        var vm = Deal("10 K 7 9").ViewModel;

        Assert.Equal(TablePhase.Playing, vm.Phase);
        Assert.False(vm.IsInsuranceOpen);
    }

    [Fact]
    public void DecliningGoesOnWithThePlayersTurn()
    {
        var t = Deal("10 A 7 9");
        var vm = t.ViewModel;

        t.Execute(vm.DeclineInsuranceCommand);

        Assert.Equal(TablePhase.Playing, vm.Phase);
        Assert.False(vm.IsInsuranceOpen);
        Assert.Equal("", vm.PlayerHands[0].InsuranceText);
    }

    [Fact]
    public void ALostInsuranceIsShownAtTheHandAndInTheResult()
    {
        var t = Deal("10 A 7 9");
        var vm = t.ViewModel;

        t.Execute(vm.TakeInsuranceCommand);
        Assert.Equal("Insurance lost -5", vm.PlayerHands[0].InsuranceText);
        Assert.Equal(TablePhase.Playing, vm.Phase);

        t.Execute(vm.StandCommand);

        Assert.Equal("You lose -10 · Insurance -5", vm.LastResult);
        Assert.Equal(985m, vm.Bankroll);
    }

    [Fact]
    public void AWonInsuranceAgainstADealerBlackjack()
    {
        var t = Deal("10 A 7 K");
        var vm = t.ViewModel;

        t.Execute(vm.TakeInsuranceCommand);

        Assert.Equal(TablePhase.Betting, vm.Phase);
        Assert.Equal("Insurance wins +10", vm.PlayerHands[0].InsuranceText);
        Assert.Equal("Dealer has Blackjack – you lose -10 · Insurance +10", vm.LastResult);
        Assert.Equal(1000m, vm.Bankroll);
    }

    [Fact]
    public void WithABlackjackAt3To2ItIsEvenMoney()
    {
        var t = Deal("A A K 9");
        var vm = t.ViewModel;

        Assert.True(vm.IsEvenMoney);
        Assert.Equal("You have Blackjack. Take even money: +10 now?", vm.InsuranceQuestion);
        Assert.Equal("Even money wins 1 to 1 whether or not the dealer has Blackjack.", vm.InsuranceNote);
        Assert.Equal(("Even money", "No"), (vm.TakeInsuranceText, vm.DeclineInsuranceText));
        Assert.Equal("Even money?", vm.Status);

        t.Execute(vm.TakeInsuranceCommand);

        Assert.Equal(1010m, vm.Bankroll);
    }

    [Fact]
    public void At6To5ABlackjackIsOfferedInsurance()
    {
        var vm = Deal("A A K 9", rules: Rules.Default with { BlackjackPayout = 1.2m }).ViewModel;

        Assert.False(vm.IsEvenMoney);
        Assert.Equal("Insurance for 5?", vm.InsuranceQuestion);
    }

    [Fact]
    public void TheNextRoundClearsTheInsurance()
    {
        var t = Deal("10 A 7 K 10 9 9 8");
        var vm = t.ViewModel;
        t.Execute(vm.TakeInsuranceCommand);

        t.Execute(vm.DealCommand);

        Assert.Equal("", vm.PlayerHands[0].InsuranceText);
        Assert.Equal("", vm.LastResult);
    }

    // --- Strategy hints (E11) ---

    [Fact]
    public void ShowRecommendsNoInsuranceInTheBox()
    {
        var t = Deal("10 A 7 9", HintMode.Show);
        var vm = t.ViewModel;

        Assert.Equal("Basic strategy: No insurance", vm.Hint);
        Assert.True(vm.IsNoInsuranceRecommended);
        Assert.False(vm.IsHintOnTable, "the hint is shown in the box");

        t.Execute(vm.DeclineInsuranceCommand);

        Assert.False(vm.IsNoInsuranceRecommended);
        Assert.Equal("Basic strategy: Stand (hard 17 vs A)", vm.Hint);
        Assert.True(vm.IsHintOnTable);
    }

    [Fact]
    public void ShowRecommendsNoEvenMoney() =>
        Assert.Equal("Basic strategy: No even money", Deal("A A K 9", HintMode.Show).ViewModel.Hint);

    [Fact]
    public void FeedbackReportsTakenInsurance()
    {
        var t = Deal("10 A 7 9", HintMode.Feedback);
        var vm = t.ViewModel;
        Assert.Equal("", vm.Hint);

        t.Execute(vm.TakeInsuranceCommand);
        t.Execute(vm.StandCommand);

        Assert.Equal("Basic strategy: No insurance – you took insurance", vm.Hint);
        Assert.True(vm.IsHintMistake);
    }

    [Fact]
    public void FeedbackSaysNothingWhenDeclining()
    {
        var t = Deal("A A K 9", HintMode.Feedback);

        t.Execute(t.ViewModel.DeclineInsuranceCommand);

        Assert.Equal("", t.ViewModel.Hint);
        Assert.Equal(1015m, t.ViewModel.Bankroll);
    }

    [Fact]
    public void FeedbackOnTakenEvenMoney()
    {
        var t = Deal("A A K 9", HintMode.Feedback);

        t.Execute(t.ViewModel.TakeInsuranceCommand);

        Assert.Equal("Basic strategy: No even money – you took even money", t.ViewModel.Hint);
    }
}
