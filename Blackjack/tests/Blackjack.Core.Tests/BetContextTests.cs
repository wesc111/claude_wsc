namespace Blackjack.Core.Tests;

/// <summary>SPEC 2.5 – the shared bet check used by the engine and by human players.</summary>
public sealed class BetContextTests
{
    [Theory]
    [InlineData("1000", "500")]
    [InlineData("200", "200")]
    [InlineData("500", "500")]
    public void MaxAllowedBetIsLimitedByBankroll(string bankroll, string expected) =>
        Assert.Equal(D(expected), new BetContext(D(bankroll), Rules.Default).MaxAllowedBet);

    [Fact]
    public void MaxAllowedBetWithUnlimitedBankroll() =>
        Assert.Equal(500m, new BetContext(null, Rules.Default).MaxAllowedBet);

    [Theory]
    [InlineData("10", true)]
    [InlineData("12.5", true)]
    [InlineData("200", true)]
    [InlineData("9.99", false)]
    [InlineData("200.01", false)]
    [InlineData("0", false)]
    [InlineData("-10", false)]
    public void IsValidBet(string bet, bool expected) =>
        Assert.Equal(expected, new BetContext(200m, Rules.Default).IsValidBet(D(bet)));

    private static decimal D(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
