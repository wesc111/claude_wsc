using Blackjack.Gui.ViewModels;

namespace Blackjack.Gui.Tests;

public sealed class HandViewModelTests
{
    private static HandViewModel Hand(params Card?[] cards)
    {
        var hand = new HandViewModel("You");
        foreach (var card in cards)
        {
            hand.Cards.Add(new CardViewModel(card));
        }

        hand.UpdateTotal();
        return hand;
    }

    [Theory]
    [InlineData("A 6", "soft 17")]
    [InlineData("10 7", "17")]
    [InlineData("10 6 6", "22 bust")]
    [InlineData("A K", "Blackjack")]
    public void Totals(string cards, string expected) =>
        Assert.Equal(expected, Hand([.. Cards.Parse(cards).Select(c => (Card?)c)]).TotalText);

    [Fact]
    public void NoTotalWhileACardIsFaceDown() =>
        Assert.Equal("", Hand(Cards.ParseOne("K"), null).TotalText);

    [Fact]
    public void AceAndTenAfterSplitIsNoBlackjack()
    {
        var hand = new HandViewModel("Hand 1") { IsFromSplit = true };
        hand.Cards.Add(new CardViewModel(Cards.ParseOne("A")));
        hand.Cards.Add(new CardViewModel(Cards.ParseOne("K")));

        hand.UpdateTotal();

        Assert.Equal("soft 21", hand.TotalText);
    }
}
