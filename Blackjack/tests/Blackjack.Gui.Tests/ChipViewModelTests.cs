using Blackjack.Gui.ViewModels;

namespace Blackjack.Gui.Tests;

public sealed class ChipViewModelTests
{
    [Theory]
    [InlineData("10", new[] { 10 })]
    [InlineData("35", new[] { 25, 10 })]
    [InlineData("135", new[] { 100, 25, 10 })]
    [InlineData("500", new[] { 100, 100, 100, 100, 100 })]
    [InlineData("45", new[] { 25, 10, 10 })]
    [InlineData("47.5", new[] { 25, 10, 10 })]
    [InlineData("4", new int[0])]
    [InlineData("0", new int[0])]
    public void SplitUsesAsFewChipsAsPossible(string amount, int[] expected)
    {
        Assert.Equal(expected, ChipViewModel.Split(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void EachValueHasItsOwnColour()
    {
        var colours = ChipViewModel.Values.Select(v => new ChipViewModel(v).Color).ToList();

        Assert.Equal(["#c62828", "#1565c0", "#2e7d32", "#212121"], colours);
    }

    [Fact]
    public void TextIsTheValue()
    {
        Assert.Equal("25", new ChipViewModel(25).Text);
    }

    [Fact]
    public void StackDrawsAtMostEightChips()
    {
        var stack = new ChipStackViewModel(new ChipViewModel(5));

        Assert.Equal("×1", stack.CountText);
        Assert.Equal([0d], stack.Layers.Select(l => l.Lift));

        stack.Count = 3;
        Assert.Equal([0d, 5d, 10d], stack.Layers.Select(l => l.Lift));

        stack.Count = 12;
        Assert.Equal("×12", stack.CountText);
        Assert.Equal(ChipStackViewModel.MaxVisibleLayers, stack.Layers.Count);
        Assert.All(stack.Layers, l => Assert.Equal(5, l.Chip.Value));
    }
}
