namespace Blackjack.ConsoleApp.Tests;

public sealed class CardFormatterTests
{
    [Theory]
    [InlineData("AS", "A♠")]
    [InlineData("10H", "10♥")]
    [InlineData("KC", "K♣")]
    [InlineData("QD", "Q♦")]
    public void WithoutColor(string card, string expected) =>
        Assert.Equal(expected, new CardFormatter(false).Format(Cards.ParseOne(card)));

    [Theory]
    [InlineData("10H", "\u001b[31m10♥\u001b[0m")]
    [InlineData("QD", "\u001b[31mQ♦\u001b[0m")]
    [InlineData("AS", "A♠")]
    [InlineData("KC", "K♣")]
    public void RedSuitsInRed(string card, string expected) =>
        Assert.Equal(expected, new CardFormatter(true).Format(Cards.ParseOne(card)));

    [Fact]
    public void HiddenCard() =>
        Assert.Equal("[??]", new CardFormatter(true).Format((Card?)null));

    [Fact]
    public void SeveralCards() =>
        Assert.Equal("K♠ [??]", new CardFormatter(false).Format([Cards.ParseOne("KS"), null]));
}
