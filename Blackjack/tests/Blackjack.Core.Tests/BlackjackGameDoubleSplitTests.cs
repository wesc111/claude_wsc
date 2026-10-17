using Blackjack.Core.Tests.Helpers;
using static Blackjack.Core.PlayerAction;

namespace Blackjack.Core.Tests;

/// <summary>
/// SPEC 7 – PlayerAction.Double and Split with predefined card sequences.
/// Deal order: player, dealer upcard, player, hole card; on a split the current hand receives a card immediately,
/// the new (appended) hand receives its second card when its turn starts.
/// </summary>
public sealed class BlackjackGameDoubleSplitTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(RoundResult Result, BlackjackGame Game, FixedCardSource Source, RecordingObserver Observer)> PlayAsync(
        string cards, ScriptedPlayer player, Rules? rules = null, decimal? bankroll = 1000m)
    {
        var source = new FixedCardSource(cards);
        var observer = new RecordingObserver();
        var game = new BlackjackGame(rules ?? Rules.Default, source, bankroll, [observer]);
        var result = await game.PlayRoundAsync(player, Ct);
        Assert.NotNull(result);
        return (result, game, source, observer);
    }

    [Fact]
    public async Task DoubleDoublesTheBetAndDealsExactlyOneCard()
    {
        // Player 6 5 = 11 doubles and draws 10 → 21; dealer 10 7 = 17.
        var player = new ScriptedPlayer(10m, PlayerAction.Double);

        var (result, game, source, observer) = await PlayAsync("6 10 5 7 10", player);

        var hand = Assert.Single(result.PlayerHands);
        Assert.True(hand.IsDoubled);
        Assert.Equal(20m, hand.Bet);
        Assert.Equal(3, hand.Cards.Count);
        Assert.Equal(HandOutcome.Win, hand.Outcome);
        Assert.Equal(20m, result.Net);
        Assert.Equal(20m, result.TotalBet);
        Assert.Equal(10m, result.InitialBet);
        Assert.Single(player.Decisions);
        Assert.Equal(5, source.CardsDrawn);
        Assert.Equal(1020m, game.Bankroll);
        Assert.Equal(new HandDoubledNotification(0, 20m), Assert.Single(observer.Doubles));
    }

    [Fact]
    public async Task DoubledHandLosesTwiceTheBet()
    {
        var (result, game, _, _) = await PlayAsync("6 10 5 9 2", new ScriptedPlayer(10m, PlayerAction.Double));

        Assert.Equal(HandOutcome.Loss, Assert.Single(result.PlayerHands).Outcome);
        Assert.Equal(-20m, result.Net);
        Assert.Equal(980m, game.Bankroll);
    }

    [Fact]
    public async Task NoDoubleWithThreeCards()
    {
        var player = new ScriptedPlayer(10m, Hit, Stand);

        await PlayAsync("2 10 3 7 4", player);

        Assert.Contains(PlayerAction.Double, player.Decisions[0].AllowedActions);
        Assert.DoesNotContain(PlayerAction.Double, player.Decisions[1].AllowedActions);
    }

    [Theory]
    [InlineData("19", false)]
    [InlineData("20", true)]
    public async Task DoubleAndSplitNeedTheAdditionalBetCovered(string bankroll, bool offered)
    {
        var player = new ScriptedPlayer(10m, Stand);

        await PlayAsync("8 10 8 7", player, bankroll: decimal.Parse(bankroll, System.Globalization.CultureInfo.InvariantCulture));

        var allowed = player.Decisions[0].AllowedActions;
        Assert.Equal(offered, allowed.Contains(PlayerAction.Double));
        Assert.Equal(offered, allowed.Contains(Split));
        Assert.Contains(Hit, allowed);
        Assert.Contains(Stand, allowed);
    }

    [Fact]
    public async Task UnlimitedBankrollAlwaysCoversAdditionalBets()
    {
        var player = new ScriptedPlayer(500m, Stand);

        await PlayAsync("8 10 8 7", player, new Rules { MaxBet = 500m }, bankroll: null);

        Assert.Contains(Split, player.Decisions[0].AllowedActions);
        Assert.Contains(PlayerAction.Double, player.Decisions[0].AllowedActions);
    }

    [Fact]
    public async Task SplitEightsPlaysTwoHandsWithTheirOwnBets()
    {
        // 8 8 split; hand 0 draws 3 → 11 and stands; hand 1 draws 10 → 18 and stands; dealer 10 7 = 17.
        var player = new ScriptedPlayer(10m, Split, Stand, Stand);

        var (result, game, _, _) = await PlayAsync("8 10 8 7 3 10", player);

        Assert.Equal(2, result.PlayerHands.Count);
        Assert.Equal(Cards.Parse("8 3"), result.PlayerHands[0].Cards);
        Assert.Equal(Cards.Parse("8 10"), result.PlayerHands[1].Cards);
        Assert.All(result.PlayerHands, h => Assert.Equal(10m, h.Bet));
        Assert.All(result.PlayerHands, h => Assert.True(h.IsFromSplit));
        Assert.Equal(HandOutcome.Loss, result.PlayerHands[0].Outcome);
        Assert.Equal(HandOutcome.Win, result.PlayerHands[1].Outcome);
        Assert.Equal(0m, result.Net);
        Assert.Equal(20m, result.TotalBet);
        Assert.Equal(10m, result.InitialBet);
        Assert.Equal(1000m, game.Bankroll);

        Assert.Equal((0, 1), (player.Decisions[0].HandIndex, player.Decisions[0].HandCount));
        Assert.Equal((0, 2), (player.Decisions[1].HandIndex, player.Decisions[1].HandCount));
        Assert.Equal((1, 2), (player.Decisions[2].HandIndex, player.Decisions[2].HandCount));
        Assert.Equal(Cards.Parse("8 10"), player.Decisions[2].PlayerCards);
    }

    [Fact]
    public async Task TenValuedCardsOfDifferentRankCanBeSplit()
    {
        var player = new ScriptedPlayer(10m, Stand);

        await PlayAsync("K 9 10 8", player);

        Assert.Contains(Split, player.Decisions[0].AllowedActions);
    }

    [Fact]
    public async Task CardsOfDifferentValueCannotBeSplit()
    {
        var player = new ScriptedPlayer(10m, Stand);

        await PlayAsync("9 9 10 8", player);

        Assert.DoesNotContain(Split, player.Decisions[0].AllowedActions);
    }

    [Fact]
    public async Task ResplitUpToFourHands()
    {
        // Three splits give four hands of 8; the fourth 8 on hand 0 can no longer be split.
        var player = new ScriptedPlayer(10m, Split, Split, Split, Stand, Stand, Stand, Stand);

        var (result, game, _, observer) = await PlayAsync("8 10 8 7 8 8 8 2 3 4", player);

        Assert.Equal(4, result.PlayerHands.Count);
        Assert.Equal((0, 4), (player.Decisions[3].HandIndex, player.Decisions[3].HandCount));
        Assert.True(new Hand(player.Decisions[3].PlayerCards).IsPair);
        Assert.DoesNotContain(Split, player.Decisions[3].AllowedActions);
        Assert.Equal([16, 10, 11, 12], result.PlayerHands.Select(h => h.Total));
        Assert.Equal(40m, result.TotalBet);
        Assert.Equal(-40m, result.Net);
        Assert.Equal(960m, game.Bankroll);
        Assert.Equal([(0, 1), (0, 2), (0, 3)], observer.Splits.Select(s => (s.HandIndex, s.NewHandIndex)));
    }

    [Fact]
    public async Task SplitAcesReceiveOneCardEachWithoutDecision()
    {
        // A A split; hand 0 draws 5 (soft 16), hand 1 draws K (21, not a Blackjack); dealer 10 7.
        var player = new ScriptedPlayer(10m, Split);

        var (result, _, _, _) = await PlayAsync("A 10 A 7 5 K", player);

        Assert.Single(player.Decisions);
        Assert.Equal(2, result.PlayerHands.Count);
        Assert.Equal(2, result.PlayerHands[0].Cards.Count);
        Assert.Equal(HandOutcome.Loss, result.PlayerHands[0].Outcome);
        Assert.Equal(HandOutcome.Win, result.PlayerHands[1].Outcome);
        Assert.Equal(21, result.PlayerHands[1].Total);
        Assert.Equal(0m, result.Net);
    }

    [Fact]
    public async Task AceAndTenAfterSplitIsNotABlackjack()
    {
        // K K split: hand 0 draws A → 21 (paid 1:1), hand 1 draws 9 → 19; dealer 10 8 = 18.
        var player = new ScriptedPlayer(10m, Split, Stand);

        var (result, _, _, observer) = await PlayAsync("K 10 K 8 A 9", player);

        Assert.Equal(HandOutcome.Win, result.PlayerHands[0].Outcome);
        Assert.Equal(10m, result.PlayerHands[0].Net);
        Assert.False(observer.HandsFinished[0].IsBlackjack);
        Assert.Equal(20m, result.Net);
    }

    [Fact]
    public async Task SplitAcesAreNotResplit()
    {
        var player = new ScriptedPlayer(10m, Split);

        var (result, _, _, _) = await PlayAsync("A 10 A 7 A K", player);

        Assert.Single(player.Decisions);
        Assert.Equal(2, result.PlayerHands.Count);
        Assert.Equal(Cards.Parse("A A"), result.PlayerHands[0].Cards);
    }

    [Fact]
    public async Task ResplitAcesWhenAllowed()
    {
        var player = new ScriptedPlayer(10m, Split, Split);

        var (result, _, _, _) = await PlayAsync("A 10 A 7 A K 5 9", player, new Rules { ResplitAces = true });

        Assert.Equal(2, player.Decisions.Count);
        Assert.Equal(new HashSet<PlayerAction> { Stand, Split }, player.Decisions[1].AllowedActions);
        Assert.Equal(3, result.PlayerHands.Count);
        Assert.Equal([21, 16, 20], result.PlayerHands.Select(h => h.Total));
    }

    [Fact]
    public async Task DoubleAfterSplit()
    {
        // 8 8 split; hand 0 draws 3 → 11, doubles, draws 10 → 21; hand 1 draws 2 → 10, stands; dealer 17.
        var player = new ScriptedPlayer(10m, Split, PlayerAction.Double, Stand);

        var (result, game, _, _) = await PlayAsync("8 10 8 7 3 10 2", player);

        Assert.Contains(PlayerAction.Double, player.Decisions[1].AllowedActions);
        Assert.True(result.PlayerHands[0].IsDoubled);
        Assert.Equal(20m, result.PlayerHands[0].Bet);
        Assert.Equal(20m, result.PlayerHands[0].Net);
        Assert.Equal(-10m, result.PlayerHands[1].Net);
        Assert.Equal(30m, result.TotalBet);
        Assert.Equal(1010m, game.Bankroll);
    }

    [Fact]
    public async Task NoDoubleAfterSplitWhenDisabled()
    {
        var player = new ScriptedPlayer(10m, Split, Stand, Stand);

        await PlayAsync("8 10 8 7 3 10", player, new Rules { DoubleAfterSplit = false });

        Assert.Contains(PlayerAction.Double, player.Decisions[0].AllowedActions);
        Assert.DoesNotContain(PlayerAction.Double, player.Decisions[1].AllowedActions);
        Assert.DoesNotContain(PlayerAction.Double, player.Decisions[2].AllowedActions);
    }

    [Fact]
    public async Task NoResplitWithMaxSplitHandsTwo()
    {
        var player = new ScriptedPlayer(10m, Split, Stand, Stand);

        var (result, _, _, _) = await PlayAsync("8 10 8 7 8 2", player, new Rules { MaxSplitHands = 2 });

        Assert.DoesNotContain(Split, player.Decisions[1].AllowedActions);
        Assert.Equal(2, result.PlayerHands.Count);
    }

    [Fact]
    public async Task NoSplitWithMaxSplitHandsOne()
    {
        var player = new ScriptedPlayer(10m, Stand);

        await PlayAsync("8 10 8 7", player, new Rules { MaxSplitHands = 1 });

        Assert.DoesNotContain(Split, player.Decisions[0].AllowedActions);
    }

    [Fact]
    public async Task DealerBlackjackEndsRoundBeforeAnyDecision()
    {
        var player = new ScriptedPlayer(10m);

        var (result, game, _, _) = await PlayAsync("8 A 8 K", player);

        Assert.Empty(player.Decisions);
        Assert.Equal(HandOutcome.DealerBlackjack, Assert.Single(result.PlayerHands).Outcome);
        Assert.Equal(-10m, result.Net);
        Assert.Equal(990m, game.Bankroll);
    }

    [Fact]
    public async Task DealerDrawsNothingWhenAllSplitHandsBust()
    {
        // Dealer 10 6 = 16 would draw, but there is no card left for that.
        var player = new ScriptedPlayer(10m, Split, Hit, Hit);

        var (result, _, source, _) = await PlayAsync("8 10 8 6 6 K 6 Q", player);

        Assert.All(result.PlayerHands, h => Assert.Equal(HandOutcome.PlayerBust, h.Outcome));
        Assert.Equal(2, result.DealerCards.Count);
        Assert.Equal(8, source.CardsDrawn);
        Assert.Equal(-20m, result.Net);
    }

    [Fact]
    public async Task DealerPlaysWhenOneSplitHandStands()
    {
        var player = new ScriptedPlayer(10m, Split, Hit, Stand);

        var (result, _, _, _) = await PlayAsync("8 10 8 6 6 K 10 5", player);

        Assert.Equal(HandOutcome.PlayerBust, result.PlayerHands[0].Outcome);
        Assert.Equal(HandOutcome.Loss, result.PlayerHands[1].Outcome);
        Assert.Equal(3, result.DealerCards.Count);
        Assert.Equal(21, result.DealerTotal);
    }

    [Theory]
    [InlineData("2 10 3 7 4", new[] { Hit, PlayerAction.Double })]
    [InlineData("9 10 8 7", new[] { Split })]
    public async Task ActionThatIsNotAllowedThrows(string cards, PlayerAction[] actions)
    {
        var game = new BlackjackGame(Rules.Default, new FixedCardSource(cards), 1000m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => game.PlayRoundAsync(new ScriptedPlayer(10m, actions), Ct));
        Assert.Equal(1000m, game.Bankroll);
    }

    [Fact]
    public async Task NotificationOrderWithSplitAndDouble()
    {
        var (_, _, _, observer) = await PlayAsync("8 10 8 7 3 10 2", new ScriptedPlayer(10m, Split, PlayerAction.Double, Stand));

        Assert.Equal(
            [
                "RoundStarted", "CardDealt:Player", "CardDealt:Dealer", "CardDealt:Player", "CardDealt:Dealer",
                "HandSplit", "CardDealt:Player", "HandDoubled", "CardDealt:Player", "PlayerHandFinished",
                "CardDealt:Player", "PlayerHandFinished", "HoleCardRevealed", "HandSettled", "HandSettled", "RoundCompleted",
            ],
            observer.Log);
        Assert.Equal(new HandSplitNotification(0, 1, Cards.ParseOne("8")), Assert.Single(observer.Splits));
        Assert.Equal(new HandDoubledNotification(0, 20m), Assert.Single(observer.Doubles));
        Assert.Equal([0, 0, 0, 0, 1], observer.CardsDealt.Where(c => !c.Recipient.IsDealer).Select(c => c.Recipient.HandIndex));
        Assert.Equal([0, 1], observer.HandsFinished.Select(h => h.HandIndex));
        Assert.Equal([0, 1], observer.HandsSettled.Select(h => h.HandIndex));
    }

    [Fact]
    public async Task CancellationDuringSplitRoundRestoresBankroll()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var observer = new RecordingObserver();
        var game = new BlackjackGame(Rules.Default, new FixedCardSource("8 10 8 7 3 10"), 1000m, [observer]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => game.PlayRoundAsync(new SplitThenCancelPlayer(cts), cts.Token));
        Assert.Equal(1000m, game.Bankroll);
        Assert.Empty(observer.RoundsCompleted);
        Assert.Single(observer.Splits);
    }

    /// <summary>Splits on the first decision and cancels the round on the second.</summary>
    private sealed class SplitThenCancelPlayer(CancellationTokenSource cts) : IPlayerStrategy
    {
        private int _decisions;

        public Task<decimal?> GetBetAsync(BetContext context, CancellationToken ct = default) => Task.FromResult<decimal?>(10m);

        public async Task<PlayerAction> DecideAsync(DecisionContext context, CancellationToken ct = default)
        {
            if (_decisions++ == 0)
            {
                return Split;
            }

            await cts.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
            return Stand;
        }
    }
}
