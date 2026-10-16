namespace Blackjack.Core.Tests;

/// <summary>SPEC 2.1 – rules and their validation.</summary>
public sealed class RulesTests
{
    [Fact]
    public void Defaults()
    {
        var rules = Rules.Default;

        Assert.Equal(6, rules.Decks);
        Assert.Equal(0.75m, rules.Penetration);
        Assert.False(rules.DealerHitsSoft17);
        Assert.Equal(1.5m, rules.BlackjackPayout);
        Assert.True(rules.DealerPeeks);
        Assert.Equal(10m, rules.MinBet);
        Assert.Equal(500m, rules.MaxBet);
        Assert.Equal(new Rules(), rules);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(-1)]
    public void DecksOutOfRange(int decks) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rules { Decks = decks });

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public void DecksAtLimits(int decks) =>
        Assert.Equal(decks, new Rules { Decks = decks }.Decks);

    [Theory]
    [InlineData("0")]
    [InlineData("-0.5")]
    [InlineData("1.01")]
    public void PenetrationOutOfRange(string penetration) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rules { Penetration = decimal.Parse(penetration, System.Globalization.CultureInfo.InvariantCulture) });

    [Fact]
    public void PenetrationOfOneIsAllowed() =>
        Assert.Equal(1m, new Rules { Penetration = 1m }.Penetration);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BlackjackPayoutOutOfRange(int payout) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rules { BlackjackPayout = payout });

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void MinBetOutOfRange(int minBet) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rules { MinBet = minBet });

    [Fact]
    public void WithExpressionIsValidated() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Rules.Default with { Decks = 0 });

    [Fact]
    public void LimitsCanBeRaisedInAnyOrder()
    {
        var rules = new Rules { MinBet = 1000m, MaxBet = 5000m };

        Assert.Equal(1000m, rules.MinBet);
        Assert.Equal(5000m, rules.MaxBet);
    }
}
