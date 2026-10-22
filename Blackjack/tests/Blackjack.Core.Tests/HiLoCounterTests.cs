using Blackjack.Core.Tests.Helpers;
using static Blackjack.Core.PlayerAction;

namespace Blackjack.Core.Tests;

/// <summary>SPEC E13 – the Hi-Lo counter as an observer of the engine.</summary>
public sealed class HiLoCounterTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("2", 1)]
    [InlineData("6", 1)]
    [InlineData("7", 0)]
    [InlineData("9", 0)]
    [InlineData("10", -1)]
    [InlineData("K", -1)]
    [InlineData("A", -1)]
    public void CardValues(string card, int value) => Assert.Equal(value, HiLoCounter.Value(Cards.ParseOne(card)));

    [Fact]
    public void AFullDeckSumsToZero() =>
        Assert.Equal(0, Cards.Parse("2 3 4 5 6 7 8 9 10 J Q K A").Sum(HiLoCounter.Value) * 4);

    [Fact]
    public async Task TheHoleCardCountsOnlyWhenRevealed()
    {
        // Player 5 6, dealer 2 + hole 10; the player stands: the hole card turns over, the dealer draws 4 and 3.
        var source = new FixedCardSource("5 2 6 10 4 3");
        var counter = new HiLoCounter(source);
        var before = new CountBeforeDecision(counter);
        var game = new BlackjackGame(Rules.Default, source, 1000m, [before, counter]);

        await game.PlayRoundAsync(new ScriptedPlayer(10m, Stand), Ct);

        Assert.Equal(3, before.Seen);
        Assert.Equal(4, counter.RunningCount); // +1 +1 +1, then −1 +1 +1
    }

    [Fact]
    public async Task AShuffleStartsAgainAtZero()
    {
        var source = new FixedCardSource("5 2 6 10 4");
        var counter = new HiLoCounter(source);

        await counter.OnCardDealtAsync(new CardDealtNotification(CardRecipient.Player(0), Cards.ParseOne("5"), true), Ct);
        await counter.OnCardDealtAsync(new CardDealtNotification(CardRecipient.Dealer, null, false), Ct);
        Assert.Equal(1, counter.RunningCount);

        await counter.OnShoeShuffledAsync(Ct);

        Assert.Equal(0, counter.RunningCount);
    }

    [Fact]
    public async Task TheTrueCountIsTheRunningCountPerDeckRemaining()
    {
        var shoe = new Shoe(new Rules(), 1);
        var counter = new HiLoCounter(shoe);
        for (var i = 0; i < 156; i++)
        {
            shoe.Draw();
        }

        foreach (var card in Cards.Parse("2 3 4 5 6 2"))
        {
            await counter.OnCardDealtAsync(new CardDealtNotification(CardRecipient.Dealer, card, true), Ct);
        }

        Assert.Equal(3m, counter.DecksRemaining);
        Assert.Equal(6, counter.RunningCount);
        Assert.Equal(2m, counter.TrueCount);
    }

    [Fact]
    public void AnEmptyShoeGivesTheRunningCount()
    {
        var counter = new HiLoCounter(new FixedCardSource(""));

        Assert.Equal(0m, counter.DecksRemaining);
        Assert.Equal(0m, counter.TrueCount);
    }

    // Records the running count when the player is asked, i.e. while the hole card is still face down.
    private sealed class CountBeforeDecision(HiLoCounter counter) : IGameObserver
    {
        public int Seen { get; private set; }

        public ValueTask OnPlayerHandFinishedAsync(PlayerHandFinishedNotification notification, CancellationToken ct)
        {
            Seen = counter.RunningCount;
            return ValueTask.CompletedTask;
        }
    }
}
