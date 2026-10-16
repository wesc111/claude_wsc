using Blackjack.Core.Tests.Helpers;
using static Blackjack.Core.PlayerAction;

namespace Blackjack.Core.Tests;

/// <summary>SPEC 3.5 and 4.3a – observer notifications.</summary>
public sealed class BlackjackGameObserverTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly string[] InitialDeal =
        ["RoundStarted", "CardDealt:Player", "CardDealt:Dealer", "CardDealt:Player", "CardDealt:Dealer"];

    private static async Task<RecordingObserver> PlayAsync(string cards, ScriptedPlayer player)
    {
        var observer = new RecordingObserver();
        var game = new BlackjackGame(Rules.Default, new FixedCardSource(cards), 1000m, [observer]);
        await game.PlayRoundAsync(player, Ct);
        return observer;
    }

    [Fact]
    public async Task NormalRoundOrder()
    {
        // Player 10 2, hits 5 → 17, stands. Dealer 10 6 draws K and busts.
        var observer = await PlayAsync("10 10 2 6 5 K", new ScriptedPlayer(10m, Hit, Stand));

        Assert.Equal(
            [.. InitialDeal, "CardDealt:Player", "PlayerHandFinished", "HoleCardRevealed", "CardDealt:Dealer", "HandSettled", "RoundCompleted"],
            observer.Log);
    }

    [Fact]
    public async Task PlayerBustOrder()
    {
        var observer = await PlayAsync("10 10 6 7 9", new ScriptedPlayer(10m, Hit));

        Assert.Equal(
            [.. InitialDeal, "CardDealt:Player", "PlayerHandFinished", "HoleCardRevealed", "HandSettled", "RoundCompleted"],
            observer.Log);
    }

    [Fact]
    public async Task PlayerBlackjackOrder()
    {
        var observer = await PlayAsync("A 9 K 7", new ScriptedPlayer(10m));

        Assert.Equal(
            [.. InitialDeal, "PlayerHandFinished", "HoleCardRevealed", "HandSettled", "RoundCompleted"],
            observer.Log);
    }

    [Fact]
    public async Task DealerBlackjackOrder()
    {
        var observer = await PlayAsync("10 A 7 K", new ScriptedPlayer(10m));

        Assert.Equal(
            [.. InitialDeal, "HoleCardRevealed", "PlayerHandFinished", "HandSettled", "RoundCompleted"],
            observer.Log);
    }

    [Theory]
    [InlineData("10 10 2 6 5 K", new[] { Hit, Stand })]
    [InlineData("10 10 6 7 9", new[] { Hit })]
    [InlineData("A 9 K 7", new PlayerAction[0])]
    [InlineData("10 A 7 K", new PlayerAction[0])]
    public async Task PlayerHandFinishedExactlyOncePerHand(string cards, PlayerAction[] actions)
    {
        var observer = await PlayAsync(cards, new ScriptedPlayer(10m, actions));

        Assert.Equal(0, Assert.Single(observer.HandsFinished).HandIndex);
        Assert.Single(observer.HoleCardsRevealed);
    }

    [Fact]
    public async Task HoleCardIsHiddenUntilRevealed()
    {
        var observer = await PlayAsync("10 9 8 8", new ScriptedPlayer(10m, Stand));

        var hole = observer.CardsDealt[3];
        Assert.True(hole.Recipient.IsDealer);
        Assert.False(hole.IsFaceUp);
        Assert.Null(hole.Card);
        Assert.Equal(Cards.ParseOne("8"), Assert.Single(observer.HoleCardsRevealed).Card);
    }

    [Fact]
    public async Task FaceUpCardsCarryTheirValueAndRecipient()
    {
        var observer = await PlayAsync("10 9 8 8", new ScriptedPlayer(10m, Stand));

        Assert.Equal(
            [
                new CardDealtNotification(CardRecipient.Player(0), Cards.ParseOne("10"), true),
                new CardDealtNotification(CardRecipient.Dealer, Cards.ParseOne("9"), true),
                new CardDealtNotification(CardRecipient.Player(0), Cards.ParseOne("8"), true),
                new CardDealtNotification(CardRecipient.Dealer, null, false),
            ],
            observer.CardsDealt);
    }

    [Fact]
    public async Task NotificationData()
    {
        var observer = await PlayAsync("A 10 6 8", new ScriptedPlayer(25m, Stand));

        Assert.Equal(new RoundStartedNotification(25m, 1000m), Assert.Single(observer.RoundsStarted));
        Assert.Equal(new PlayerHandFinishedNotification(0, 17, true, false, false), Assert.Single(observer.HandsFinished));
        Assert.Equal(new HandSettledNotification(0, HandOutcome.Loss, -25m), Assert.Single(observer.HandsSettled));
        var result = Assert.Single(observer.RoundsCompleted);
        Assert.Equal(975m, result.Bankroll);
        Assert.Equal(Cards.Parse("10 8"), result.DealerCards);
    }

    [Fact]
    public async Task ReshuffleIsReportedBeforeRoundStarted()
    {
        var observer = new RecordingObserver();
        var source = new FixedCardSource("10 10 9 8") { NeedsReshuffle = true };
        var game = new BlackjackGame(Rules.Default, source, 1000m, [observer]);

        await game.PlayRoundAsync(new ScriptedPlayer(10m, Stand), Ct);

        Assert.Equal(1, source.ShuffleCount);
        Assert.Equal(["ShoeShuffled", "RoundStarted"], observer.Log.Take(2));
    }

    [Fact]
    public async Task NoReshuffleIsReportedWhenNotNeeded()
    {
        var observer = await PlayAsync("10 10 9 8", new ScriptedPlayer(10m, Stand));

        Assert.DoesNotContain("ShoeShuffled", observer.Log);
    }

    [Fact]
    public async Task ReshuffleMarkPassedMidRoundTakesEffectBeforeNextRound()
    {
        var log = new List<string>();
        var source = new FixedCardSource("10 10 9 8 10 10 9 8");
        var observer = new RecordingObserver(log);
        var marker = new ReshuffleMarkObserver(source);
        var game = new BlackjackGame(Rules.Default, source, 1000m, [marker, observer]);

        await game.PlayRoundAsync(new ScriptedPlayer(10m, Stand), Ct);

        Assert.Equal(0, source.ShuffleCount);
        Assert.Equal(4, source.CardsDrawn);

        log.Clear();
        await game.PlayRoundAsync(new ScriptedPlayer(10m, Stand), Ct);

        Assert.Equal(1, source.ShuffleCount);
        Assert.Equal(["ShoeShuffled", "RoundStarted"], log.Take(2));
    }

    [Fact]
    public async Task EngineAwaitsEachNotification()
    {
        var source = new FixedCardSource("10 10 2 6 5 K");
        var slow = new SlowObserver(source);
        var game = new BlackjackGame(Rules.Default, source, 1000m, [slow]);

        await game.PlayRoundAsync(new ScriptedPlayer(10m, Hit, Stand), Ct);

        Assert.Equal(6, slow.Calls);
        Assert.False(slow.DrawDuringNotification);
    }

    [Fact]
    public async Task SeveralObserversInRegistrationOrder()
    {
        var log = new List<string>();
        var game = new BlackjackGame(
            Rules.Default,
            new FixedCardSource("A 9 K 7"),
            1000m,
            [new RecordingObserver(log, "A:"), new RecordingObserver(log, "B:")]);

        await game.PlayRoundAsync(new ScriptedPlayer(10m), Ct);

        Assert.Equal(18, log.Count);
        for (var i = 0; i < log.Count; i += 2)
        {
            Assert.StartsWith("A:", log[i], StringComparison.Ordinal);
            Assert.Equal("B:" + log[i][2..], log[i + 1]);
        }
    }

    [Fact]
    public async Task ObserverExceptionPropagatesAndRestoresBankroll()
    {
        var game = new BlackjackGame(Rules.Default, new FixedCardSource("10 10 9 8"), 1000m, [new ThrowingObserver()]);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => game.PlayRoundAsync(new ScriptedPlayer(10m, Stand), Ct));
        Assert.Equal(ThrowingObserver.Message, error.Message);
        Assert.Equal(1000m, game.Bankroll);
    }

    [Fact]
    public async Task ObserverImplementingNothingIsAllowed()
    {
        var game = new BlackjackGame(Rules.Default, new FixedCardSource("10 10 9 8"), 1000m, [new EmptyObserver()]);

        var result = await game.PlayRoundAsync(new ScriptedPlayer(10m, Stand), Ct);

        Assert.Equal(HandOutcome.Win, Assert.Single(result!.PlayerHands).Outcome);
    }

    /// <summary>Sets the reshuffle flag as soon as the first card is dealt.</summary>
    private sealed class ReshuffleMarkObserver(FixedCardSource source) : IGameObserver
    {
        public ValueTask OnCardDealtAsync(CardDealtNotification notification, CancellationToken ct)
        {
            source.NeedsReshuffle = true;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Delays every card notification and checks that no card is drawn meanwhile.</summary>
    private sealed class SlowObserver(FixedCardSource source) : IGameObserver
    {
        public int Calls { get; private set; }

        public bool DrawDuringNotification { get; private set; }

        public async ValueTask OnCardDealtAsync(CardDealtNotification notification, CancellationToken ct)
        {
            Calls++;
            var before = source.CardsDrawn;
            await Task.Delay(20, ct);
            DrawDuringNotification |= source.CardsDrawn != before;
        }
    }

    /// <summary>Fails after the bankroll has been updated.</summary>
    private sealed class ThrowingObserver : IGameObserver
    {
        public const string Message = "Observer failure";

        public ValueTask OnRoundCompletedAsync(RoundResult result, CancellationToken ct) =>
            throw new InvalidOperationException(Message);
    }

    private sealed class EmptyObserver : IGameObserver;
}
