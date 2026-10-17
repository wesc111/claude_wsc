using Blackjack.Core.Tests.Helpers;

namespace Blackjack.Core.Tests;

/// <summary>SPEC 4.1 – hand evaluation.</summary>
public sealed class HandTests
{
    [Theory]
    [InlineData("A K", 21, true, true, false)]
    [InlineData("A 6", 17, true, false, false)]
    [InlineData("A 6 10", 17, false, false, false)]
    [InlineData("A A", 12, true, false, false)]
    [InlineData("A A 9", 21, true, false, false)]
    [InlineData("A A A A 7", 21, true, false, false)]
    [InlineData("10 6 6", 22, false, false, true)]
    [InlineData("7 7 7", 21, false, false, false)]
    [InlineData("5 6 Q", 21, false, false, false)]
    [InlineData("A 5 5", 21, true, false, false)]
    public void Evaluation(string cards, int total, bool soft, bool blackjack, bool bust)
    {
        var hand = new Hand(Cards.Parse(cards));

        Assert.Equal(total, hand.Total);
        Assert.Equal(soft, hand.IsSoft);
        Assert.Equal(blackjack, hand.IsBlackjack);
        Assert.Equal(bust, hand.IsBust);
    }

    [Theory]
    [InlineData("K A")]
    [InlineData("A 10")]
    [InlineData("J A")]
    public void BlackjackInAnyOrder(string cards) =>
        Assert.True(new Hand(Cards.Parse(cards)).IsBlackjack);

    [Fact]
    public void AddReevaluates()
    {
        var hand = new Hand(Cards.Parse("A 6"));
        Assert.True(hand.IsSoft);

        hand.Add(Cards.ParseOne("10"));

        Assert.Equal(17, hand.Total);
        Assert.False(hand.IsSoft);
        Assert.Equal(3, hand.Cards.Count);
    }

    [Fact]
    public void EmptyHand()
    {
        var hand = new Hand();

        Assert.Equal(0, hand.Total);
        Assert.False(hand.IsSoft);
        Assert.False(hand.IsBlackjack);
        Assert.Empty(hand.Cards);
    }

    [Theory]
    [InlineData("8 8", true)]
    [InlineData("K 10", true)]
    [InlineData("Q J", true)]
    [InlineData("A A", true)]
    [InlineData("9 10", false)]
    [InlineData("8 8 8", false)]
    [InlineData("8", false)]
    public void IsPair(string cards, bool expected) =>
        Assert.Equal(expected, new Hand(Cards.Parse(cards)).IsPair);

    [Fact]
    public void HandFromSplitIsNeverABlackjack()
    {
        var hand = new Hand(Cards.Parse("A K")) { IsFromSplit = true };

        Assert.Equal(21, hand.Total);
        Assert.False(hand.IsBlackjack);
    }
}
