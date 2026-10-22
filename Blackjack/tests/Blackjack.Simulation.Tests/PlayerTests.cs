using Blackjack.Simulation.Players;

namespace Blackjack.Simulation.Tests;

/// <summary>SPEC 6.1 – the automated players.</summary>
public sealed class PlayerTests
{
    private static readonly IReadOnlySet<PlayerAction> HitOrStand = new HashSet<PlayerAction> { PlayerAction.Hit, PlayerAction.Stand };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DecisionContext Context(string cards, string upcard)
    {
        var hand = new Hand(Cards.Parse(cards));
        return new DecisionContext(hand.Cards.ToArray(), hand.Total, hand.IsSoft, Cards.ParseOne(upcard), HitOrStand);
    }

    [Theory]
    [InlineData("basic")]
    [InlineData("basic-hs")]
    [InlineData("mimic")]
    [InlineData("neverbust")]
    public async Task EveryPlayerDeclinesInsurance(string name)
    {
        // Spec E12: basic strategy never takes insurance, so the simulation results stay as before.
        IPlayerStrategy player = PlayerFactory.Create(name, 10m, Rules.Default);

        Assert.False(await player.DecideInsuranceAsync(new InsuranceContext(Cards.Parse("A K"), 21, true, Cards.ParseOne("A"), 5m), Ct));
    }

    [Theory]
    [InlineData("10 2", "3", PlayerAction.Hit)]
    [InlineData("10 2", "4", PlayerAction.Stand)]
    [InlineData("10 6", "10", PlayerAction.Hit)]
    [InlineData("10 6", "6", PlayerAction.Stand)]
    [InlineData("A 7", "9", PlayerAction.Hit)]
    [InlineData("A 7", "8", PlayerAction.Stand)]
    [InlineData("A 6", "2", PlayerAction.Hit)]
    [InlineData("5 4 A 2", "4", PlayerAction.Stand)]
    public void BasicStrategy(string cards, string upcard, PlayerAction expected) =>
        Assert.Equal(expected, new BasicStrategyPlayer(10m, BasicStrategyTable.HitStand).Decide(Context(cards, upcard)));

    [Fact]
    public async Task FullBasicStrategyDoublesAndSplitsWhenAllowed()
    {
        var all = new HashSet<PlayerAction> { PlayerAction.Hit, PlayerAction.Stand, PlayerAction.Double, PlayerAction.Split };
        var player = PlayerFactory.Create("basic", 10m, Rules.Default);
        var eleven = new Hand(Cards.Parse("6 5"));
        var eights = new Hand(Cards.Parse("8 8"));

        Assert.Equal(PlayerAction.Double, await player.DecideAsync(new DecisionContext(eleven.Cards.ToArray(), 11, false, Cards.ParseOne("6"), all), Ct));
        Assert.Equal(PlayerAction.Split, await player.DecideAsync(new DecisionContext(eights.Cards.ToArray(), 16, false, Cards.ParseOne("10"), all), Ct));
    }

    [Theory]
    [InlineData("10 6", PlayerAction.Hit)]
    [InlineData("10 7", PlayerAction.Stand)]
    [InlineData("A 5", PlayerAction.Hit)]
    [InlineData("A 6", PlayerAction.Stand)]
    [InlineData("A 6 10", PlayerAction.Stand)]
    [InlineData("2 3", PlayerAction.Hit)]
    public void MimicDealerUnderS17(string cards, PlayerAction expected) =>
        Assert.Equal(expected, new MimicDealerPlayer(10m, Rules.Default).Decide(Context(cards, "10")));

    [Fact]
    public void MimicDealerHitsSoft17UnderH17() =>
        Assert.Equal(PlayerAction.Hit, new MimicDealerPlayer(10m, new Rules { DealerHitsSoft17 = true }).Decide(Context("A 6", "10")));

    [Theory]
    [InlineData("10", "9 2", PlayerAction.Hit)]
    [InlineData("10", "10 2", PlayerAction.Stand)]
    [InlineData("10", "10 6", PlayerAction.Stand)]
    [InlineData("2", "A 6", PlayerAction.Hit)]
    [InlineData("2", "A 7", PlayerAction.Stand)]
    [InlineData("2", "A 9", PlayerAction.Stand)]
    [InlineData("2", "A A", PlayerAction.Hit)]
    public void NeverBust(string upcard, string cards, PlayerAction expected) =>
        Assert.Equal(expected, new NeverBustPlayer(10m).Decide(Context(cards, upcard)));

    [Theory]
    [InlineData("basic", typeof(BasicStrategyPlayer))]
    [InlineData("basic-hs", typeof(BasicStrategyPlayer))]
    [InlineData("mimic", typeof(MimicDealerPlayer))]
    [InlineData("neverbust", typeof(NeverBustPlayer))]
    public async Task FactoryCreatesFlatBetPlayers(string name, Type type)
    {
        var player = PlayerFactory.Create(name, 25m, Rules.Default);

        Assert.IsType(type, player);
        Assert.Equal(25m, await player.GetBetAsync(new BetContext(null, Rules.Default), Ct));
        Assert.Equal(25m, await player.GetBetAsync(new BetContext(null, Rules.Default), Ct));
    }

    [Fact]
    public void UnknownStrategyThrows() =>
        Assert.Throws<ArgumentException>(() => PlayerFactory.Create("random", 10m, Rules.Default));

    [Fact]
    public void BetMustBePositive() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new NeverBustPlayer(0m));

    [Fact]
    public async Task DecisionTasksAreCachedAndAllowed()
    {
        var player = new NeverBustPlayer(10m);
        var context = Context("10 6", "10");

        var first = player.DecideAsync(context, Ct);
        var second = player.DecideAsync(context, Ct);

        Assert.Same(first, second);
        Assert.Contains(await first, context.AllowedActions);
    }
}
