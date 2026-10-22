using static Blackjack.Core.PlayerAction;

namespace Blackjack.Core.Tests;

/// <summary>SPEC E13 – the Illustrious 18, the insurance index and the bet spread.</summary>
public sealed class CountingStrategyTests
{
    private static readonly IReadOnlySet<PlayerAction> All = new HashSet<PlayerAction> { Hit, Stand, PlayerAction.Double, Split };
    private static readonly IReadOnlySet<PlayerAction> HitOrStand = new HashSet<PlayerAction> { Hit, Stand };

    private static DecisionContext Context(string cards, string upcard, IReadOnlySet<PlayerAction>? allowed = null)
    {
        var hand = new Hand(Cards.Parse(cards));
        return new DecisionContext(hand.Cards.ToArray(), hand.Total, hand.IsSoft, Cards.ParseOne(upcard), allowed ?? All);
    }

    // Each play just short of its index (basic strategy) and at it (deviation); "below" plays the other way round.
    [Theory]
    [InlineData("10 6", "10", -0.1, 0, Hit, Stand)]
    [InlineData("10 5", "10", 3.9, 4, Hit, Stand)]
    [InlineData("10 K", "5", 4.9, 5, Stand, Split)]
    [InlineData("Q J", "6", 3.9, 4, Stand, Split)]
    [InlineData("6 4", "10", 3.9, 4, Hit, PlayerAction.Double)]
    [InlineData("10 2", "3", 1.9, 2, Hit, Stand)]
    [InlineData("10 2", "2", 2.9, 3, Hit, Stand)]
    [InlineData("6 5", "A", 0.9, 1, Hit, PlayerAction.Double)]
    [InlineData("5 4", "2", 0.9, 1, Hit, PlayerAction.Double)]
    [InlineData("6 4", "A", 3.9, 4, Hit, PlayerAction.Double)]
    [InlineData("5 4", "7", 2.9, 3, Hit, PlayerAction.Double)]
    [InlineData("10 6", "9", 4.9, 5, Hit, Stand)]
    [InlineData("10 3", "2", -1, -1.1, Stand, Hit)]
    [InlineData("10 2", "4", 0, -0.1, Stand, Hit)]
    [InlineData("10 2", "5", -2, -2.1, Stand, Hit)]
    [InlineData("10 2", "6", -1, -1.1, Stand, Hit)]
    [InlineData("10 3", "3", -2, -2.1, Stand, Hit)]
    public void TheIndexPlays(string cards, string upcard, double before, double at, PlayerAction basic, PlayerAction deviation)
    {
        var context = Context(cards, upcard);

        Assert.Equal((basic, (IndexPlay?)null), CountingStrategy.Decide(context, (decimal)before));
        var (action, play) = CountingStrategy.Decide(context, (decimal)at);
        Assert.Equal(deviation, action);
        Assert.NotNull(play);
    }

    [Fact]
    public void ThereAreSeventeenIndexPlaysPlusInsurance() => Assert.Equal(17, CountingStrategy.IndexPlays.Count);

    [Theory]
    [InlineData(2.9, false)]
    [InlineData(3, true)]
    public void InsuranceAtTrueCountThree(double trueCount, bool take) =>
        Assert.Equal(take, CountingStrategy.TakeInsurance((decimal)trueCount));

    [Fact]
    public void ADeviationThatIsNotAllowedFallsBackToBasicStrategy()
    {
        Assert.Equal(Hit, CountingStrategy.Decide(Context("2 4 4", "10", HitOrStand), 6m).Action);
        Assert.Equal(Stand, CountingStrategy.Decide(Context("10 K", "6", HitOrStand), 6m).Action);
    }

    [Fact]
    public void ASplitPairIsNotPlayedAsItsTotal() =>
        Assert.Equal(Split, CountingStrategy.Decide(Context("8 8", "10"), 6m).Action);

    [Fact]
    public void ThreeCardHandsDeviateToo() =>
        Assert.Equal(Stand, CountingStrategy.Decide(Context("10 2 4", "10"), 0m).Action);

    [Fact]
    public void SoftHandsKeepBasicStrategy() =>
        Assert.Equal((Hit, (IndexPlay?)null), CountingStrategy.Decide(Context("A 5", "10"), 10m));

    [Theory]
    [InlineData(-3, 1)]
    [InlineData(1.9, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 6)]
    [InlineData(4.9, 6)]
    [InlineData(5, 8)]
    [InlineData(9, 8)]
    public void TheBetSpread(double trueCount, int units) => Assert.Equal(units, CountingStrategy.BetUnits((decimal)trueCount));

    [Theory]
    [InlineData(1000, 3, 40)]
    [InlineData(1000, 9, 80)]
    [InlineData(55, 9, 55)]
    public void TheBetIsLimitedByTheBankroll(int bankroll, int trueCount, int bet) =>
        Assert.Equal(bet, CountingStrategy.Bet(new BetContext(bankroll, Rules.Default), trueCount));
}
