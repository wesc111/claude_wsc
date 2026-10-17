using Blackjack.Gui.ViewModels;

namespace Blackjack.Gui.Tests;

/// <summary>The chip tray and the bet area of the table (spec E4).</summary>
public sealed class BetAreaTests
{
    private static IEnumerable<string> Stacks(TableViewModel vm) =>
        vm.BetStacks.Select(s => $"{s.Chip.Value}{s.CountText}");

    [Fact]
    public void TrayHasOneChipPerValue()
    {
        var t = Table.Start("10 9 7 8");

        Assert.Equal([5, 10, 25, 100], t.ViewModel.ChipTray.Select(c => c.Value));
    }

    [Fact]
    public void FirstBetIsTheMinimumBetAsOneChip()
    {
        var t = Table.Start("10 9 7 8");

        Assert.Equal(["10×1"], Stacks(t.ViewModel));
    }

    [Fact]
    public void ChipsAreStackedByValueInAscendingOrder()
    {
        var t = Table.Start("10 9 7 8");
        var vm = t.ViewModel;

        t.Chip(100);
        t.Chip(25);
        t.Chip(5);
        t.Chip(25);

        Assert.Equal(["5×1", "10×1", "25×2", "100×1"], Stacks(vm));
        Assert.Equal(165m, vm.Bet);
        Assert.Equal("Bet: 165", vm.BetText);
    }

    [Fact]
    public void ClickOnAStackTakesOneChipBack()
    {
        var t = Table.Start("10 9 7 8");
        var vm = t.ViewModel;
        t.Chip(25);
        t.Chip(25);

        t.Do(() => vm.RemoveChipCommand.Execute(25));

        Assert.Equal(["10×1", "25×1"], Stacks(vm));
        Assert.Equal(35m, vm.Bet);

        t.Do(() => vm.RemoveChipCommand.Execute(25));

        Assert.Equal(["10×1"], Stacks(vm));
        Assert.Equal(10m, vm.Bet);
        Assert.False(vm.RemoveChipCommand.CanExecute(25), "no 25 chip left in the bet area");
        Assert.True(vm.RemoveChipCommand.CanExecute(10));
    }

    [Fact]
    public void ClearEmptiesTheBetArea()
    {
        var t = Table.Start("10 9 7 8");
        var vm = t.ViewModel;
        t.Chip(100);
        t.Chip(5);

        t.Execute(vm.ClearBetCommand);

        Assert.Empty(vm.BetStacks);
        Assert.Equal(0m, vm.Bet);
        Assert.False(vm.DealCommand.CanExecute(null));
        Assert.False(vm.RemoveChipCommand.CanExecute(10));
    }

    [Fact]
    public void LastBetIsShownAsChips()
    {
        var t = Table.Start("10 9 9 8");
        var vm = t.ViewModel;
        t.Chip(100);
        t.Chip(25);
        t.Execute(vm.DealCommand);

        t.Execute(vm.StandCommand);

        Assert.Equal(TablePhase.Betting, vm.Phase);
        Assert.Equal(["10×1", "25×1", "100×1"], Stacks(vm));
        Assert.Equal(135m, vm.Bet);
    }

    [Fact]
    public void ChipsStayInTheBetAreaDuringTheRoundButCannotBeChanged()
    {
        var t = Table.Start("10 9 7 8");
        var vm = t.ViewModel;
        t.Chip(25);

        t.Execute(vm.DealCommand);

        Assert.Equal(TablePhase.Playing, vm.Phase);
        Assert.Equal(["10×1", "25×1"], Stacks(vm));
        Assert.All(vm.ChipTray, c => Assert.False(vm.AddChipCommand.CanExecute(c.Value)));
        Assert.False(vm.RemoveChipCommand.CanExecute(25));
        Assert.False(vm.ClearBetCommand.CanExecute(null));
    }

    [Fact]
    public void ChipAboveTheBankrollIsDisabled()
    {
        var t = Table.Start("10 9 7 8", bankroll: 40m);
        var vm = t.ViewModel;
        t.Chip(25);

        Assert.Equal(35m, vm.Bet);
        Assert.True(vm.AddChipCommand.CanExecute(5));
        Assert.False(vm.AddChipCommand.CanExecute(10), "45 is above the bankroll of 40");
        Assert.False(vm.AddChipCommand.CanExecute(25));
        Assert.False(vm.AddChipCommand.CanExecute(100));
    }

    [Fact]
    public void LastBetAboveAFractionalBankrollIsRoundedDownToChips()
    {
        // Bet 60 and lose: the bankroll of 47.5 cannot be made with chips, so the default bet is 45.
        var t = Table.Start("10 10 7 8", bankroll: 107.5m);
        var vm = t.ViewModel;
        t.Execute(vm.ClearBetCommand);
        t.Chip(25);
        t.Chip(25);
        t.Chip(10);
        t.Execute(vm.DealCommand);

        t.Execute(vm.StandCommand);

        Assert.Equal(47.5m, vm.Bankroll);
        Assert.Equal(["10×2", "25×1"], Stacks(vm));
        Assert.Equal(45m, vm.Bet);
        Assert.False(vm.AddChipCommand.CanExecute(5), "50 is above the bankroll of 47.5");
        Assert.True(vm.DealCommand.CanExecute(null));
    }

    [Fact]
    public void UnknownChipValueIsIgnored()
    {
        var t = Table.Start("10 9 7 8");
        var vm = t.ViewModel;
        Assert.False(vm.AddChipCommand.CanExecute(7));

        t.Do(() => vm.AddChipCommand.Execute(7));

        Assert.Equal(["10×1"], Stacks(vm));
        Assert.Equal(10m, vm.Bet);
    }
}
