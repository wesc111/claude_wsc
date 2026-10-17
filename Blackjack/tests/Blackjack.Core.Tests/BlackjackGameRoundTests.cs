using Blackjack.Core.Tests.Helpers;
using static Blackjack.Core.PlayerAction;

namespace Blackjack.Core.Tests;

/// <summary>
/// SPEC 2.4, 2.5 and 4.3 – round flow with predefined card sequences.
/// Deal order: player, dealer upcard, player, dealer hole card, player hits, dealer draws.
/// </summary>
public sealed class BlackjackGameRoundTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(RoundResult Result, BlackjackGame Game, FixedCardSource Source)> PlayAsync(
        string cards, ScriptedPlayer player, Rules? rules = null, decimal? bankroll = 1000m)
    {
        var source = new FixedCardSource(cards);
        var game = new BlackjackGame(rules ?? Rules.Default, source, bankroll);
        var result = await game.PlayRoundAsync(player, Ct);
        Assert.NotNull(result);
        return (result, game, source);
    }

    [Fact]
    public async Task DealerBlackjackWithAceUpcard()
    {
        var player = new ScriptedPlayer(10m);

        var (result, game, _) = await PlayAsync("10 A 7 K", player);

        Assert.Equal(HandOutcome.DealerBlackjack, Assert.Single(result.PlayerHands).Outcome);
        Assert.Equal(-10m, result.Net);
        Assert.Empty(player.Decisions);
        Assert.Equal(990m, game.Bankroll);
    }

    [Fact]
    public async Task BothBlackjackIsPush()
    {
        var player = new ScriptedPlayer(10m);

        var (result, game, _) = await PlayAsync("A 10 K A", player);

        Assert.Equal(HandOutcome.Push, Assert.Single(result.PlayerHands).Outcome);
        Assert.Equal(0m, result.Net);
        Assert.Empty(player.Decisions);
        Assert.Equal(1000m, game.Bankroll);
    }

    [Fact]
    public async Task PlayerBlackjackWithNineUpcard()
    {
        var player = new ScriptedPlayer(10m);

        var (result, game, source) = await PlayAsync("A 9 K 7", player);

        Assert.Equal(HandOutcome.BlackjackWin, Assert.Single(result.PlayerHands).Outcome);
        Assert.Equal(15m, result.Net);
        Assert.Equal(2, result.DealerCards.Count);
        Assert.Equal(4, source.CardsDrawn);
        Assert.Empty(player.Decisions);
        Assert.Equal(1015m, game.Bankroll);
    }

    [Fact]
    public async Task PlayerBlackjackAfterPeekWithoutDealerBlackjack()
    {
        var (result, _, _) = await PlayAsync("A 10 K 9", new ScriptedPlayer(10m));

        Assert.Equal(HandOutcome.BlackjackWin, Assert.Single(result.PlayerHands).Outcome);
        Assert.Equal(15m, result.Net);
    }

    [Fact]
    public async Task NoDealerBlackjackAfterPeekContinues()
    {
        var player = new ScriptedPlayer(10m, Stand);

        var (result, _, _) = await PlayAsync("10 A 7 9", player);

        Assert.Single(player.Decisions);
        Assert.Equal(HandOutcome.Loss, Assert.Single(result.PlayerHands).Outcome);
        Assert.Equal(20, result.DealerTotal);
    }

    [Fact]
    public async Task BlackjackPaysThreeToTwoOnOddBet()
    {
        var (result, game, _) = await PlayAsync("A 9 K 7", new ScriptedPlayer(15m));

        Assert.Equal(22.5m, result.Net);
        Assert.Equal(1022.5m, game.Bankroll);
    }

    [Fact]
    public async Task BlackjackPaysSixToFive()
    {
        var (result, _, _) = await PlayAsync("A 9 K 7", new ScriptedPlayer(10m), new Rules { BlackjackPayout = 1.2m });

        Assert.Equal(HandOutcome.BlackjackWin, Assert.Single(result.PlayerHands).Outcome);
        Assert.Equal(12m, result.Net);
    }

    [Fact]
    public async Task PlayerBustEndsRoundWithoutDealerDrawing()
    {
        // The dealer holds 16 and would draw if the dealer played; the source has no card left for that.
        var (result, game, source) = await PlayAsync("10 6 6 10 6", new ScriptedPlayer(10m, Hit));

        var hand = Assert.Single(result.PlayerHands);
        Assert.Equal(HandOutcome.PlayerBust, hand.Outcome);
        Assert.Equal(22, hand.Total);
        Assert.Equal(2, result.DealerCards.Count);
        Assert.Equal(5, source.CardsDrawn);
        Assert.Equal(990m, game.Bankroll);
    }

    [Theory]
    [InlineData("10 10 8 8", HandOutcome.Push, 0)]
    [InlineData("10 10 7 8", HandOutcome.Loss, -10)]
    [InlineData("10 10 9 8", HandOutcome.Win, 10)]
    public async Task StandAgainstDealer18(string cards, HandOutcome outcome, int net)
    {
        var (result, _, _) = await PlayAsync(cards, new ScriptedPlayer(10m, Stand));

        Assert.Equal(outcome, Assert.Single(result.PlayerHands).Outcome);
        Assert.Equal(net, result.Net);
        Assert.Equal(18, result.DealerTotal);
    }

    [Fact]
    public async Task DealerBustsAfterPlayerStandsOn12()
    {
        var (result, game, _) = await PlayAsync("10 6 2 10 K", new ScriptedPlayer(10m, Stand));

        Assert.Equal(HandOutcome.DealerBust, Assert.Single(result.PlayerHands).Outcome);
        Assert.Equal(10m, result.Net);
        Assert.Equal(26, result.DealerTotal);
        Assert.Equal(1010m, game.Bankroll);
    }

    [Fact]
    public async Task TwentyOneWithThreeCardsEndsTurnAutomatically()
    {
        // ScriptedPlayer fails the test if it is asked a second time.
        var player = new ScriptedPlayer(10m, Hit);

        var (result, _, _) = await PlayAsync("5 10 6 7 Q", player);

        Assert.Single(player.Decisions);
        var hand = Assert.Single(result.PlayerHands);
        Assert.Equal(21, hand.Total);
        Assert.Equal(HandOutcome.Win, hand.Outcome);
    }

    [Fact]
    public async Task DecisionContextDescribesTheHand()
    {
        var player = new ScriptedPlayer(10m, Stand);

        await PlayAsync("A 9 6 7 10", player);

        var context = Assert.Single(player.Decisions);
        Assert.Equal(Cards.Parse("A 6"), context.PlayerCards);
        Assert.Equal(17, context.PlayerTotal);
        Assert.True(context.IsSoft);
        Assert.Equal(Cards.ParseOne("9"), context.DealerUpcard);
        // Since M4 a two-card hand may also be doubled (SPEC 7.2); A, 6 is no pair, so no Split.
        Assert.Equal(new HashSet<PlayerAction> { Hit, Stand, PlayerAction.Double }, context.AllowedActions);
        Assert.Equal(0, context.HandIndex);
        Assert.Equal(1, context.HandCount);
    }

    [Fact]
    public async Task IllegalActionThrows()
    {
        var game = new BlackjackGame(Rules.Default, new FixedCardSource("10 10 6 7"), 1000m);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => game.PlayRoundAsync(new ScriptedPlayer(10m, (PlayerAction)42), Ct));
        Assert.Equal(1000m, game.Bankroll);
    }

    [Theory]
    [InlineData("9.99", "1000")]
    [InlineData("500.01", "1000")]
    [InlineData("0", "1000")]
    [InlineData("-10", "1000")]
    [InlineData("150", "100")]
    public async Task InvalidBetThrows(string bet, string bankroll)
    {
        var source = new FixedCardSource("10 10 9 8");
        var startingBankroll = decimal.Parse(bankroll, System.Globalization.CultureInfo.InvariantCulture);
        var game = new BlackjackGame(Rules.Default, source, startingBankroll);
        var player = new ScriptedPlayer(decimal.Parse(bet, System.Globalization.CultureInfo.InvariantCulture), Stand);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => game.PlayRoundAsync(player, Ct));
        Assert.Equal(startingBankroll, game.Bankroll);
        Assert.Equal(0, source.CardsDrawn);
    }

    [Fact]
    public async Task FractionalBetIsAccepted()
    {
        var (result, game, _) = await PlayAsync("10 10 9 8", new ScriptedPlayer(12.5m, Stand));

        Assert.Equal(12.5m, result.TotalBet);
        Assert.Equal(12.5m, result.Net);
        Assert.Equal(1012.5m, game.Bankroll);
    }

    [Fact]
    public async Task BetEqualToLimitsAndBankrollIsAccepted()
    {
        var (result, _, _) = await PlayAsync("10 10 9 8", new ScriptedPlayer(500m, Stand), bankroll: 500m);

        Assert.Equal(1000m, result.Bankroll);
    }

    [Fact]
    public async Task BankrollBelowMinBetThrowsWithoutAskingForBet()
    {
        var player = new ScriptedPlayer(10m);
        var game = new BlackjackGame(Rules.Default, new FixedCardSource("10 10 9 8"), 5m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => game.PlayRoundAsync(player, Ct));
        Assert.Equal(0, player.BetRequests);
    }

    [Fact]
    public async Task NullBetLeavesTheTable()
    {
        var source = new FixedCardSource("10 10 9 8");
        var game = new BlackjackGame(Rules.Default, source, 1000m);

        var result = await game.PlayRoundAsync(new ScriptedPlayer(null), Ct);

        Assert.Null(result);
        Assert.Equal(0, source.CardsDrawn);
        Assert.Equal(1000m, game.Bankroll);
    }

    [Fact]
    public async Task UnlimitedBankroll()
    {
        var (result, game, _) = await PlayAsync("10 10 9 8", new ScriptedPlayer(500m, Stand), bankroll: null);

        Assert.Equal(500m, result.Net);
        Assert.Null(result.Bankroll);
        Assert.Null(game.Bankroll);
    }

    [Fact]
    public async Task CancellationDuringDecisionRestoresBankroll()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var observer = new RecordingObserver();
        var game = new BlackjackGame(Rules.Default, new FixedCardSource("10 10 6 7"), 1000m, [observer]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => game.PlayRoundAsync(new CancellingPlayer(cts), cts.Token));
        Assert.Equal(1000m, game.Bankroll);
        Assert.Empty(observer.RoundsCompleted);
    }

    [Fact]
    public async Task SecondRoundWhileRunningThrows()
    {
        var player = new WaitingBetPlayer();
        var game = new BlackjackGame(Rules.Default, new FixedCardSource(""), 1000m);

        var first = game.PlayRoundAsync(player, Ct);
        await Assert.ThrowsAsync<InvalidOperationException>(() => game.PlayRoundAsync(player, Ct));

        player.Release(null);
        Assert.Null(await first);
    }

    [Fact]
    public async Task BankrollCarriesOverBetweenRounds()
    {
        var game = new BlackjackGame(Rules.Default, new FixedCardSource("10 10 9 8 10 A 7 K"), 1000m);

        await game.PlayRoundAsync(new ScriptedPlayer(50m, Stand), Ct);
        var second = await game.PlayRoundAsync(new ScriptedPlayer(20m), Ct);

        Assert.Equal(1030m, second?.Bankroll);
        Assert.Equal(1030m, game.Bankroll);
    }

    [Fact]
    public void MaxBetBelowMinBetThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BlackjackGame(new Rules { MinBet = 100m, MaxBet = 50m }, new FixedCardSource(""), 1000m));

    [Fact]
    public void NoPeekIsNotSupported() =>
        Assert.Throws<NotSupportedException>(
            () => new BlackjackGame(new Rules { DealerPeeks = false }, new FixedCardSource(""), 1000m));

    [Fact]
    public async Task DealerHitsSoft17UnderH17()
    {
        // Player stands on 18; dealer A + 6 (soft 17) draws a 2 under H17 and ends on 19.
        var (result, _, _) = await PlayAsync("10 A 8 6 2", new ScriptedPlayer(10m, Stand), new Rules { DealerHitsSoft17 = true });

        Assert.Equal(19, result.DealerTotal);
        Assert.Equal(HandOutcome.Loss, Assert.Single(result.PlayerHands).Outcome);
    }

    [Fact]
    public async Task DealerStandsOnSoft17UnderS17()
    {
        var (result, _, _) = await PlayAsync("10 A 8 6", new ScriptedPlayer(10m, Stand));

        Assert.Equal(17, result.DealerTotal);
        Assert.Equal(HandOutcome.Win, Assert.Single(result.PlayerHands).Outcome);
    }

    /// <summary>Cancels the round from within the decision, then waits for the cancellation.</summary>
    private sealed class CancellingPlayer(CancellationTokenSource cts) : IPlayerStrategy
    {
        public Task<decimal?> GetBetAsync(BetContext context, CancellationToken ct = default) => Task.FromResult<decimal?>(10m);

        public async Task<PlayerAction> DecideAsync(DecisionContext context, CancellationToken ct = default)
        {
            await cts.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
            return Stand;
        }
    }

    /// <summary>Keeps the round waiting in GetBetAsync until released.</summary>
    private sealed class WaitingBetPlayer : IPlayerStrategy
    {
        private readonly TaskCompletionSource<decimal?> _bet = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release(decimal? bet) => _bet.SetResult(bet);

        public Task<decimal?> GetBetAsync(BetContext context, CancellationToken ct = default) => _bet.Task;

        public Task<PlayerAction> DecideAsync(DecisionContext context, CancellationToken ct = default) => Task.FromResult(Stand);
    }
}
