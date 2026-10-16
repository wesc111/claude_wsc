using Blackjack.Core.Tests.Helpers;

namespace Blackjack.Core.Tests;

public sealed class CardTests
{
    [Theory]
    [InlineData("10", true)]
    [InlineData("J", true)]
    [InlineData("Q", true)]
    [InlineData("K", true)]
    [InlineData("9", false)]
    [InlineData("A", false)]
    public void IsTenValued(string card, bool expected) =>
        Assert.Equal(expected, Cards.ParseOne(card).IsTenValued);

    [Theory]
    [InlineData("A", 11)]
    [InlineData("K", 10)]
    [InlineData("Q", 10)]
    [InlineData("J", 10)]
    [InlineData("10", 10)]
    [InlineData("7", 7)]
    [InlineData("2", 2)]
    public void BaseValue(string card, int expected) =>
        Assert.Equal(expected, Cards.ParseOne(card).BaseValue);

    [Theory]
    [InlineData("AS", "A♠")]
    [InlineData("10H", "10♥")]
    [InlineData("QD", "Q♦")]
    [InlineData("2C", "2♣")]
    public void ToStringUsesSuitSymbols(string card, string expected) =>
        Assert.Equal(expected, Cards.ParseOne(card).ToString());
}
