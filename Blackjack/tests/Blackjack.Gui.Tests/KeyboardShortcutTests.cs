using Avalonia.Input;
using Blackjack.Gui.ViewModels;
using Blackjack.Gui.Views;

namespace Blackjack.Gui.Tests;

/// <summary>Spec E10: keyboard shortcuts run the commands of the buttons, under the same conditions.</summary>
public sealed class KeyboardShortcutTests
{
    private static bool Press(Table t, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        var handled = false;
        t.Do(() => handled = KeyboardShortcuts.Handle(t.ViewModel, key, modifiers));
        return handled;
    }

    [Fact]
    public void EveryKeyFindsTheCommandOfItsButton()
    {
        var vm = Table.Start("10 9 9 8").ViewModel;

        Assert.Same(vm.HitCommand, KeyboardShortcuts.Find(vm, Key.H, KeyModifiers.None).Command);
        Assert.Same(vm.StandCommand, KeyboardShortcuts.Find(vm, Key.S, KeyModifiers.None).Command);
        Assert.Same(vm.DoubleCommand, KeyboardShortcuts.Find(vm, Key.D, KeyModifiers.None).Command);
        Assert.Same(vm.SplitCommand, KeyboardShortcuts.Find(vm, Key.P, KeyModifiers.None).Command);
        Assert.Same(vm.DealCommand, KeyboardShortcuts.Find(vm, Key.Enter, KeyModifiers.None).Command);
        Assert.Same(vm.DealCommand, KeyboardShortcuts.Find(vm, Key.Space, KeyModifiers.None).Command);
        Assert.Same(vm.ClearBetCommand, KeyboardShortcuts.Find(vm, Key.C, KeyModifiers.None).Command);
        Assert.Same(vm.NewGameCommand, KeyboardShortcuts.Find(vm, Key.N, KeyModifiers.None).Command);
        Assert.Same(vm.ToggleSoundCommand, KeyboardShortcuts.Find(vm, Key.M, KeyModifiers.None).Command);
    }

    [Theory]
    [InlineData(Key.D1, 5)]
    [InlineData(Key.D2, 10)]
    [InlineData(Key.D3, 25)]
    [InlineData(Key.D4, 100)]
    [InlineData(Key.NumPad1, 5)]
    [InlineData(Key.NumPad4, 100)]
    public void DigitsStandForTheChips(Key key, int value)
    {
        var vm = Table.Start("10 9 9 8").ViewModel;

        Assert.Equal((vm.AddChipCommand, (object?)value), KeyboardShortcuts.Find(vm, key, KeyModifiers.None));
        Assert.Equal((vm.RemoveChipCommand, (object?)value), KeyboardShortcuts.Find(vm, key, KeyModifiers.Shift));
    }

    [Theory]
    [InlineData(Key.L, KeyModifiers.None)]
    [InlineData(Key.X, KeyModifiers.None)]
    [InlineData(Key.H, KeyModifiers.Control)]
    [InlineData(Key.S, KeyModifiers.Shift)]
    [InlineData(Key.OemComma, KeyModifiers.Meta)]
    [InlineData(Key.D3, KeyModifiers.Meta)]
    public void OtherKeysAreNoShortcuts(Key key, KeyModifiers modifiers)
    {
        var t = Table.Start("10 9 9 8");

        Assert.False(Press(t, key, modifiers));
        Assert.Equal(TablePhase.Betting, t.ViewModel.Phase);
    }

    [Fact]
    public void ARoundPlayedWithTheKeyboard()
    {
        var t = Table.Start("10 9 9 8");
        var vm = t.ViewModel;

        Assert.True(Press(t, Key.D3));
        Assert.Equal(35m, vm.Bet);
        Assert.True(Press(t, Key.D2, KeyModifiers.Shift));
        Assert.Equal(25m, vm.Bet);
        Assert.True(Press(t, Key.C));
        Assert.Equal(0m, vm.Bet);
        Assert.True(Press(t, Key.D2));
        Assert.True(Press(t, Key.Enter));
        Assert.Equal(TablePhase.Playing, vm.Phase);

        Assert.True(Press(t, Key.S));

        Assert.Equal(1010m, vm.Bankroll);
        Assert.Equal(TablePhase.Betting, vm.Phase);
    }

    [Fact]
    public void SpaceDeals()
    {
        var t = Table.Start("10 9 9 8");

        Press(t, Key.Space);

        Assert.Equal(TablePhase.Playing, t.ViewModel.Phase);
    }

    [Fact]
    public void ADisabledCommandDoesNothing()
    {
        var t = Table.Start("10 9 9 8");
        var vm = t.ViewModel;

        Assert.True(Press(t, Key.H), "H is a shortcut, also while Hit is disabled");
        Assert.True(Press(t, Key.N));

        Assert.Equal(TablePhase.Betting, vm.Phase);
        Assert.Empty(vm.PlayerHands);

        Press(t, Key.Enter);
        Assert.True(Press(t, Key.D4));
        Assert.Equal(10m, vm.Bet);
    }

    [Fact]
    public void NStartsANewGameAfterLeavingTheTable()
    {
        var t = new Table(1000m, "10 9 9 8", "10 9 9 8");
        var vm = t.ViewModel;
        t.Do(vm.Start);
        t.Execute(vm.LeaveTableCommand);

        Press(t, Key.N);

        Assert.Equal(TablePhase.Betting, vm.Phase);
    }

    [Fact]
    public void MSwitchesTheSound()
    {
        var t = Table.Start("10 9 9 8");

        Press(t, Key.M);
        Assert.False(t.ViewModel.IsSoundOn);

        Press(t, Key.M);
        Assert.True(t.ViewModel.IsSoundOn);
    }

    [Fact]
    public void TheChipTooltipsNameTheirKeys()
    {
        Assert.Equal("Bet 25 (key 3)", new ChipViewModel(25).TrayTip);
        Assert.Equal("Click to take a chip back (Shift+1)", new ChipViewModel(5).StackTip);
    }
}
