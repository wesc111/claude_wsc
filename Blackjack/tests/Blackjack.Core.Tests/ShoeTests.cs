namespace Blackjack.Core.Tests;

/// <summary>SPEC 2.6 and 4.4 – shoe.</summary>
public sealed class ShoeTests
{
    private static List<Card> DrawAll(Shoe shoe)
    {
        var cards = new List<Card>();
        while (shoe.CardsRemaining > 0)
        {
            cards.Add(shoe.Draw());
        }

        return cards;
    }

    [Fact]
    public void SixDeckShoeContainsEveryCardSixTimes()
    {
        var shoe = new Shoe(6, 0.75m, seed: 1);

        var cards = DrawAll(shoe);

        Assert.Equal(312, cards.Count);
        var groups = cards.GroupBy(c => c).ToList();
        Assert.Equal(52, groups.Count);
        Assert.All(groups, g => Assert.Equal(6, g.Count()));
    }

    [Fact]
    public void OneDeckShoeContainsEveryCardOnce()
    {
        var cards = DrawAll(new Shoe(1, 0.75m, seed: 1));

        Assert.Equal(52, cards.Count);
        Assert.Equal(52, cards.Distinct().Count());
    }

    [Fact]
    public void ShoeIsShuffledOnConstruction()
    {
        var ordered = Enum.GetValues<Suit>()
            .SelectMany(s => Enum.GetValues<Rank>().Select(r => new Card(r, s)))
            .ToList();

        Assert.NotEqual(ordered, DrawAll(new Shoe(1, 1m, seed: 1)));
    }

    [Fact]
    public void SameSeedGivesSameSequenceAcrossShuffle()
    {
        var a = new Shoe(6, 0.75m, seed: 42);
        var b = new Shoe(6, 0.75m, seed: 42);

        Assert.Equal(DrawAll(a), DrawAll(b));

        a.Shuffle();
        b.Shuffle();

        Assert.Equal(DrawAll(a), DrawAll(b));
    }

    [Fact]
    public void DifferentSeedsGiveDifferentSequences() =>
        Assert.NotEqual(DrawAll(new Shoe(6, 0.75m, seed: 1)), DrawAll(new Shoe(6, 0.75m, seed: 2)));

    [Fact]
    public void ShuffleChangesTheOrder()
    {
        var shoe = new Shoe(6, 0.75m, seed: 7);
        var first = DrawAll(shoe);

        shoe.Shuffle();

        Assert.NotEqual(first, DrawAll(shoe));
    }

    [Fact]
    public void NeedsReshuffleAtPenetrationMark()
    {
        var shoe = new Shoe(6, 0.75m, seed: 1);
        for (var i = 0; i < 233; i++)
        {
            shoe.Draw();
        }

        Assert.False(shoe.NeedsReshuffle);

        shoe.Draw();

        Assert.True(shoe.NeedsReshuffle);
    }

    [Fact]
    public void ShuffleReturnsAllCards()
    {
        var shoe = new Shoe(6, 0.75m, seed: 1);
        for (var i = 0; i < 250; i++)
        {
            shoe.Draw();
        }

        shoe.Shuffle();

        Assert.Equal(312, shoe.CardsRemaining);
        Assert.False(shoe.NeedsReshuffle);
        Assert.Equal(52, DrawAll(shoe).Distinct().Count());
    }

    [Fact]
    public void DrawOnEmptyShoeThrows()
    {
        var shoe = new Shoe(1, 1m, seed: 1);
        DrawAll(shoe);

        Assert.Throws<InvalidOperationException>(() => shoe.Draw());
    }

    [Fact]
    public void ConstructorFromRules()
    {
        var shoe = new Shoe(new Rules { Decks = 2 }, seed: 1);

        Assert.Equal(104, shoe.TotalCards);
        Assert.Equal(104, shoe.CardsRemaining);
    }

    [Theory]
    [InlineData(0, "0.75")]
    [InlineData(9, "0.75")]
    [InlineData(6, "0")]
    [InlineData(6, "1.01")]
    public void InvalidArguments(int decks, string penetration) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Shoe(decks, decimal.Parse(penetration, System.Globalization.CultureInfo.InvariantCulture)));
}
