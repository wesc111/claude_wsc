using Blackjack.Core.Tests.Helpers;

namespace Blackjack.Core.Tests;

/// <summary>SPEC 4.4 – the FixedCardSource test helper.</summary>
public sealed class FixedCardSourceTests
{
    [Fact]
    public void DealsInOrderThenThrows()
    {
        var source = new FixedCardSource("A K 5");

        Assert.Equal(Cards.Parse("A K 5"), [source.Draw(), source.Draw(), source.Draw()]);
        Assert.Equal(0, source.CardsRemaining);
        Assert.Equal(3, source.TotalCards);
        var error = Assert.Throws<InvalidOperationException>(() => source.Draw());
        Assert.Contains("Unexpected draw", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NeedsReshuffleIsSettableAndShuffleOnlyCounts()
    {
        var source = new FixedCardSource("A K") { NeedsReshuffle = true };

        source.Shuffle();
        source.Shuffle();

        Assert.True(source.NeedsReshuffle);
        Assert.Equal(2, source.ShuffleCount);
        Assert.Equal(Cards.ParseOne("A"), source.Draw());
    }
}
