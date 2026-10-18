using Blackjack.Gui.ViewModels;

namespace Blackjack.Gui.Tests;

/// <summary>Spec E7: the texts of the table print follow the rules.</summary>
public sealed class TablePrintTests
{
    [Theory]
    [InlineData("1.5", "BLACKJACK PAYS 3 TO 2")]
    [InlineData("1.2", "BLACKJACK PAYS 6 TO 5")]
    [InlineData("1", "BLACKJACK PAYS 1 TO 1")]
    [InlineData("2", "BLACKJACK PAYS 2 TO 1")]
    public void PayoutAsTheSmallestFraction(string payout, string expected) =>
        Assert.Equal(expected, DisplayText.PayoutPrint(decimal.Parse(payout, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData(false, "Dealer Must Stand on Soft 17")]
    [InlineData(true, "Dealer Must Hit on Soft 17")]
    public void DealerRule(bool hitsSoft17, string expected) =>
        Assert.Equal(expected, DisplayText.DealerRulePrint(hitsSoft17));

    [Fact]
    public void TheTableShowsThePrintOfItsRules()
    {
        var rules = Rules.Default with { BlackjackPayout = 1.2m, DealerHitsSoft17 = true };
        var vm = new TableViewModel(rules, r => new FixedCardSource(""), 1000m, TimeSpan.Zero);

        Assert.Equal("BLACKJACK PAYS 6 TO 5", vm.PayoutPrint);
        Assert.Equal("Dealer Must Hit on Soft 17", vm.DealerRulePrint);
        Assert.Equal("Insurance Pays 2 to 1", vm.InsurancePrint);
    }

    [Fact]
    public void TheDefaultTablePrint()
    {
        var vm = Table.Start("10 9 7 8").ViewModel;

        Assert.Equal("BLACKJACK PAYS 3 TO 2", vm.PayoutPrint);
        Assert.Equal("Dealer Must Stand on Soft 17", vm.DealerRulePrint);
    }
}
