using Blackjack.Core.Tests.Helpers;
using static Blackjack.Core.PlayerAction;

namespace Blackjack.Core.Tests;

/// <summary>
/// SPEC E12 – insurance and even money. Deal order: player, dealer upcard, player, hole card.
/// </summary>
public sealed class BlackjackGameInsuranceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(RoundResult Result, BlackjackGame Game, RecordingObserver Observer)> PlayAsync(
        string cards, IPlayerStrategy player, decimal? bankroll = 1000m)
    {
        var observer = new RecordingObserver();
        var game = new BlackjackGame(Rules.Default, new FixedCardSource(cards), bankroll, [observer]);
        var result = await game.PlayRoundAsync(player, Ct);
        Assert.NotNull(result);
        return (result, game, observer);
    }

    [Fact]
    public async Task AnAceOffersInsuranceForHalfTheBet()
    {
        var player = new ScriptedPlayer(10m, Stand);

        await PlayAsync("10 A 7 9", player);

        var offer = Assert.Single(player.InsuranceOffers);
        Assert.Equal(5m, offer.Amount);
        Assert.Equal(17, offer.PlayerTotal);
        Assert.False(offer.IsBlackjack);
        Assert.True(offer.DealerUpcard.IsAce);
        Assert.Equal(2, offer.PlayerCards.Count);
    }

    [Theory]
    [InlineData("10 K 7 9")]
    [InlineData("10 6 7 9 5")]
    public async Task OtherUpcardsOfferNoInsurance(string cards)
    {
        var player = new ScriptedPlayer(10m, Stand) { TakesInsurance = true };

        var (result, _, observer) = await PlayAsync(cards, player);

        Assert.Empty(player.InsuranceOffers);
        Assert.Equal(0m, result.InsuranceBet);
        Assert.Empty(observer.Insurances);
    }

    [Fact]
    public async Task LostInsuranceCostsHalfTheBetAndTheRoundGoesOn()
    {
        // Player 10 7 = 17 stands; dealer A 9 = soft 20.
        var player = new ScriptedPlayer(10m, Stand) { TakesInsurance = true };

        var (result, game, observer) = await PlayAsync("10 A 7 9", player);

        Assert.Single(player.Decisions);
        Assert.Equal(5m, result.InsuranceBet);
        Assert.Equal(-5m, result.InsuranceNet);
        Assert.Equal(-15m, result.Net);
        Assert.Equal(10m, result.TotalBet);
        Assert.Equal(985m, game.Bankroll);
        Assert.Equal(new InsuranceSettledNotification(5m, -5m), Assert.Single(observer.Insurances));
    }

    [Fact]
    public async Task InsuranceAgainstADealerBlackjackPaysTwoToOne()
    {
        var player = new ScriptedPlayer(10m) { TakesInsurance = true };

        var (result, game, observer) = await PlayAsync("10 A 7 K", player);

        Assert.Empty(player.Decisions);
        Assert.Equal(HandOutcome.DealerBlackjack, Assert.Single(result.PlayerHands).Outcome);
        Assert.Equal(10m, result.InsuranceNet);
        Assert.Equal(0m, result.Net);
        Assert.Equal(1000m, game.Bankroll);
        Assert.Equal(new InsuranceSettledNotification(5m, 10m), Assert.Single(observer.Insurances));
    }

    [Fact]
    public async Task WithoutInsuranceADealerBlackjackCostsTheBet()
    {
        var player = new ScriptedPlayer(10m);

        var (result, game, observer) = await PlayAsync("10 A 7 K", player);

        Assert.Single(player.InsuranceOffers);
        Assert.Equal(0m, result.InsuranceBet);
        Assert.Equal(-10m, result.Net);
        Assert.Equal(990m, game.Bankroll);
        Assert.Empty(observer.Insurances);
    }

    [Theory]
    [InlineData("A A K 9")] // dealer has no Blackjack: Blackjack +15, insurance −5
    [InlineData("A A K K")] // both Blackjack: push, insurance +10
    public async Task EvenMoneyWinsTheBetEitherWay(string cards)
    {
        var player = new ScriptedPlayer(10m) { TakesInsurance = true };

        var (result, _, _) = await PlayAsync(cards, player);

        Assert.True(Assert.Single(player.InsuranceOffers).IsBlackjack);
        Assert.Equal(10m, result.Net);
    }

    [Fact]
    public async Task TheInsuranceIsSettledWhenTheDealerPeeks()
    {
        var withBlackjack = await PlayAsync("10 A 7 K", new ScriptedPlayer(10m) { TakesInsurance = true });
        var withoutBlackjack = await PlayAsync("10 A 7 9", new ScriptedPlayer(10m, Stand) { TakesInsurance = true });
        var playerBlackjack = await PlayAsync("A A K 9", new ScriptedPlayer(10m) { TakesInsurance = true });

        Assert.Equal(
            ["HoleCardRevealed", "InsuranceSettled", "PlayerHandFinished", "HandSettled", "RoundCompleted"],
            withBlackjack.Observer.Log[^5..]);
        Assert.Equal(["CardDealt:Dealer", "InsuranceSettled", "PlayerHandFinished", "HoleCardRevealed"], withoutBlackjack.Observer.Log[4..8]);
        Assert.Equal(["CardDealt:Dealer", "InsuranceSettled", "PlayerHandFinished", "HoleCardRevealed"], playerBlackjack.Observer.Log[4..8]);
    }

    [Theory]
    [InlineData("14", false)]
    [InlineData("15", true)]
    public async Task InsuranceNeedsTheBankrollToCoverIt(string bankroll, bool offered)
    {
        var player = new ScriptedPlayer(10m, Stand);

        await PlayAsync("10 A 7 9", player, decimal.Parse(bankroll, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(offered, player.InsuranceOffers.Count == 1);
    }

    [Fact]
    public async Task WithAnUnlimitedBankrollInsuranceIsAlwaysOffered()
    {
        var player = new ScriptedPlayer(10m, Stand);

        await PlayAsync("10 A 7 9", player, bankroll: null);

        Assert.Single(player.InsuranceOffers);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ALostInsuranceIsNotAvailableForDouble(bool insured, bool doubleOffered)
    {
        // Bankroll 24, bet 10: Double needs 20, with insurance 25.
        var player = new ScriptedPlayer(10m, Stand) { TakesInsurance = insured };

        await PlayAsync("6 A 5 9", player, 24m);

        Assert.Equal(doubleOffered, player.Decisions[0].AllowedActions.Contains(PlayerAction.Double));
    }

    [Fact]
    public async Task ByDefaultAStrategyDeclinesInsurance()
    {
        var (result, _, _) = await PlayAsync("10 A 7 K", new DefaultPlayer());

        Assert.Equal(0m, result.InsuranceBet);
        Assert.Equal(-10m, result.Net);
    }

    [Fact]
    public async Task CancellationDuringTheInsuranceOfferRestoresTheBankroll()
    {
        using var cts = new CancellationTokenSource();
        var game = new BlackjackGame(Rules.Default, new FixedCardSource("10 A 7 9"), 1000m);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => game.PlayRoundAsync(new CancellingPlayer(cts), cts.Token));

        Assert.Equal(1000m, game.Bankroll);
    }

    // Implements only the required members, so the default insurance decision applies.
    private sealed class DefaultPlayer : IPlayerStrategy
    {
        public Task<decimal?> GetBetAsync(BetContext context, CancellationToken ct = default) => Task.FromResult<decimal?>(10m);

        public Task<PlayerAction> DecideAsync(DecisionContext context, CancellationToken ct = default) => Task.FromResult(Stand);
    }

    private sealed class CancellingPlayer(CancellationTokenSource cts) : IPlayerStrategy
    {
        public Task<decimal?> GetBetAsync(BetContext context, CancellationToken ct = default) => Task.FromResult<decimal?>(10m);

        public Task<PlayerAction> DecideAsync(DecisionContext context, CancellationToken ct = default) => Task.FromResult(Stand);

        public Task<bool> DecideInsuranceAsync(InsuranceContext context, CancellationToken ct = default)
        {
            cts.Cancel();
            return Task.FromResult(true);
        }
    }
}
