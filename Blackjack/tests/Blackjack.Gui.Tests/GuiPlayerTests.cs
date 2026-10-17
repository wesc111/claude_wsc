using Blackjack.Gui.ViewModels;

namespace Blackjack.Gui.Tests;

public sealed class GuiPlayerTests
{
    private static readonly IReadOnlySet<PlayerAction> HitOrStand = new HashSet<PlayerAction> { PlayerAction.Hit, PlayerAction.Stand };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DecisionContext Decision() => new(Cards.Parse("10 6"), 16, false, Cards.ParseOne("9"), HitOrStand);

    [Fact]
    public async Task BetIsPendingUntilPlaced()
    {
        var player = new GuiPlayer();
        BetContext? requested = null;
        player.BetRequested += (_, c) => requested = c;

        var bet = player.GetBetAsync(new BetContext(1000m, Rules.Default), Ct);

        Assert.False(bet.IsCompleted);
        Assert.NotNull(requested);
        Assert.NotNull(player.PendingBet);
        Assert.True(player.PlaceBet(25m));
        Assert.Equal(25m, await bet);
        Assert.Null(player.PendingBet);
    }

    [Theory]
    [InlineData("5")]
    [InlineData("600")]
    [InlineData("0")]
    public void InvalidBetIsRejectedAndStaysPending(string amount)
    {
        var player = new GuiPlayer();
        var bet = player.GetBetAsync(new BetContext(1000m, Rules.Default), Ct);

        Assert.False(player.PlaceBet(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));
        Assert.False(bet.IsCompleted);
        Assert.NotNull(player.PendingBet);
    }

    [Fact]
    public async Task LeaveTableReturnsNull()
    {
        var player = new GuiPlayer();
        var bet = player.GetBetAsync(new BetContext(1000m, Rules.Default), Ct);

        Assert.True(player.LeaveTable());
        Assert.Null(await bet);
    }

    [Fact]
    public void NothingPendingRejectsEverything()
    {
        var player = new GuiPlayer();

        Assert.False(player.PlaceBet(10m));
        Assert.False(player.LeaveTable());
        Assert.False(player.Choose(PlayerAction.Hit));
        Assert.False(player.CanChoose(PlayerAction.Hit));
    }

    [Fact]
    public async Task OnlyAllowedActionsCanBeChosen()
    {
        var player = new GuiPlayer();
        DecisionContext? requested = null;
        player.DecisionRequested += (_, c) => requested = c;
        var decision = player.DecideAsync(Decision(), Ct);

        Assert.NotNull(requested);
        Assert.True(player.CanChoose(PlayerAction.Stand));
        Assert.False(player.CanChoose(PlayerAction.Double));
        Assert.False(player.Choose(PlayerAction.Double));
        Assert.False(decision.IsCompleted);
        Assert.True(player.Choose(PlayerAction.Stand));
        Assert.Equal(PlayerAction.Stand, await decision);
        Assert.Null(player.PendingDecision);
    }

    [Fact]
    public async Task CancellationEndsTheWait()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var player = new GuiPlayer();
        var decision = player.DecideAsync(Decision(), cts.Token);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decision);
    }
}
