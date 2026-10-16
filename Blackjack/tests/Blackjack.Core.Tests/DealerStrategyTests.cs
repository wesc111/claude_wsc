using Blackjack.Core.Tests.Helpers;

namespace Blackjack.Core.Tests;

/// <summary>SPEC 4.2 – dealer strategy.</summary>
public sealed class DealerStrategyTests
{
    [Theory]
    [InlineData("10 6", true)]
    [InlineData("10 7", false)]
    [InlineData("A 6", false)]
    [InlineData("A 5", true)]
    [InlineData("A 6 10", false)]
    [InlineData("A 7", false)]
    [InlineData("2 3 A", true)]
    public void StandsOnSoft17(string cards, bool shouldHit)
    {
        var strategy = new DealerStrategy(Rules.Default);

        Assert.Equal(shouldHit, strategy.ShouldHit(new Hand(Cards.Parse(cards))));
    }

    [Theory]
    [InlineData("A 6", true)]
    [InlineData("2 4 A", true)]
    [InlineData("A 6 10", false)]
    [InlineData("A 7", false)]
    [InlineData("10 7", false)]
    public void HitsSoft17(string cards, bool shouldHit)
    {
        var strategy = new DealerStrategy(new Rules { DealerHitsSoft17 = true });

        Assert.Equal(shouldHit, strategy.ShouldHit(new Hand(Cards.Parse(cards))));
    }
}
