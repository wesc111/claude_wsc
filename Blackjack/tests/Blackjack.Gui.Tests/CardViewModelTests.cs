using Blackjack.Gui.ViewModels;

namespace Blackjack.Gui.Tests;

public sealed class CardViewModelTests
{
    [Theory]
    [InlineData("AS", "ace_of_spades")]
    [InlineData("10H", "10_of_hearts")]
    [InlineData("2C", "2_of_clubs")]
    [InlineData("JD", "jack_of_diamonds")]
    [InlineData("QH", "queen_of_hearts")]
    [InlineData("KC", "king_of_clubs")]
    public void ImageNames(string card, string expected) =>
        Assert.Equal(expected, new CardViewModel(Cards.ParseOne(card)).ImageName);

    [Fact]
    public void FaceDownCardShowsTheBack()
    {
        var card = new CardViewModel(null);

        Assert.False(card.IsFaceUp);
        Assert.Equal("card_back", card.ImageName);
    }

    [Fact]
    public void TurningOverNotifiesTheView()
    {
        var card = new CardViewModel(null);
        var changed = new List<string?>();
        card.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        card.Card = Cards.ParseOne("KS");

        Assert.Equal("king_of_spades", card.ImageName);
        Assert.Contains(nameof(CardViewModel.ImageName), changed);
        Assert.Contains(nameof(CardViewModel.IsFaceUp), changed);
    }

    [Fact]
    public void EveryCardHasAnImageFile()
    {
        var folder = FindCardFolder();
        var names = Enum.GetValues<Suit>()
            .SelectMany(s => Enum.GetValues<Rank>().Select(r => CardViewModel.ImageNameOf(new Card(r, s))))
            .Append(CardViewModel.BackImage);

        Assert.All(names, n => Assert.True(File.Exists(Path.Combine(folder, n + ".png")), $"{n}.png is missing"));
    }

    private static string FindCardFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "cards-png");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("cards-png not found above the test directory.");
    }
}
