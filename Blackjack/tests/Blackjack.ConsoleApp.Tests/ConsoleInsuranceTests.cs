namespace Blackjack.ConsoleApp.Tests;

/// <summary>Spec E12: the insurance question and its results on the console.</summary>
public sealed class ConsoleInsuranceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly StringWriter _output = new() { NewLine = "\n" };

    private ConsolePlayer Player(params string[] lines) => new(new StringReader(string.Concat(lines.Select(l => l + "\n"))), _output);

    private static InsuranceContext Offer(string cards = "10 7", bool blackjack = false) =>
        new(Cards.Parse(cards), blackjack ? 21 : 17, blackjack, Cards.ParseOne("A"), 5m);

    [Theory]
    [InlineData("y", true)]
    [InlineData("Y", true)]
    [InlineData("n", false)]
    public async Task TheAnswerDecides(string answer, bool expected)
    {
        Assert.Equal(expected, await Player(answer).DecideInsuranceAsync(Offer(), Ct));
        Assert.Equal("The dealer shows an Ace. Insurance for 5? (y/n): ", _output.ToString());
    }

    [Fact]
    public async Task OtherInputIsAskedAgain()
    {
        Assert.True(await Player("x", "y").DecideInsuranceAsync(Offer(), Ct));
        Assert.Contains("Please enter y or n.", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheEndOfTheInputDeclines() =>
        Assert.False(await Player().DecideInsuranceAsync(Offer(), Ct));

    [Fact]
    public async Task WithABlackjackAt3To2ItIsEvenMoney()
    {
        var player = Player("10", "y");
        await player.GetBetAsync(new BetContext(1000m, Rules.Default), Ct);
        _output.GetStringBuilder().Clear();

        Assert.True(await player.DecideInsuranceAsync(Offer("A K", blackjack: true), Ct));
        Assert.Equal("You have Blackjack, the dealer shows an Ace. Even money (+10 now)? (y/n): ", _output.ToString());
    }

    [Fact]
    public async Task At6To5ABlackjackIsOfferedInsurance()
    {
        var player = Player("10", "n");
        await player.GetBetAsync(new BetContext(1000m, Rules.Default with { BlackjackPayout = 1.2m }), Ct);
        _output.GetStringBuilder().Clear();

        await player.DecideInsuranceAsync(Offer("A K", blackjack: true), Ct);
        Assert.Equal("The dealer shows an Ace. Insurance for 5? (y/n): ", _output.ToString());
    }

    [Fact]
    public async Task TheRendererShowsTheInsurance()
    {
        var renderer = new TableRenderer(_output, new CardFormatter(false));
        await renderer.OnRoundStartedAsync(new RoundStartedNotification(10m, 1000m), Ct);
        await renderer.OnInsuranceSettledAsync(new InsuranceSettledNotification(5m, 10m), Ct);
        await renderer.OnHandSettledAsync(new HandSettledNotification(0, HandOutcome.DealerBlackjack, -10m), Ct);
        await renderer.OnRoundCompletedAsync(new RoundResult(10m, [], Cards.Parse("A K"), 21, 0m, 1000m, 10m, 5m, 10m), Ct);

        Assert.Equal(
            "Insurance wins +10.\n" +
            "You:     Dealer has Blackjack – you lose  -10\n" +
            "Insurance wins  +10\n" +
            "Result:  0   Bankroll: 1000\n\n",
            _output.ToString());
    }

    [Fact]
    public async Task ALostInsurance()
    {
        var renderer = new TableRenderer(_output, new CardFormatter(false));

        await renderer.OnInsuranceSettledAsync(new InsuranceSettledNotification(5m, -5m), Ct);

        Assert.Equal("Insurance lost -5.\n", _output.ToString());
    }
}
