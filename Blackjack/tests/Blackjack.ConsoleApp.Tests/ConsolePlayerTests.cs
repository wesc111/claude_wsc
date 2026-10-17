namespace Blackjack.ConsoleApp.Tests;

public sealed class ConsolePlayerTests
{
    private static readonly IReadOnlySet<PlayerAction> HitOrStand = new HashSet<PlayerAction> { PlayerAction.Hit, PlayerAction.Stand };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly StringWriter _output = new() { NewLine = "\n" };

    private ConsolePlayer Player(params string[] lines) => new(new StringReader(string.Concat(lines.Select(l => l + "\n"))), _output);

    private static BetContext Bet(decimal bankroll = 1000m) => new(bankroll, Rules.Default);

    private static readonly IReadOnlySet<PlayerAction> All =
        new HashSet<PlayerAction> { PlayerAction.Hit, PlayerAction.Stand, PlayerAction.Double, PlayerAction.Split };

    private static DecisionContext Decision() =>
        new(Cards.Parse("10 6"), 16, false, Cards.ParseOne("9"), HitOrStand);

    private static DecisionContext Decision(IReadOnlySet<PlayerAction> allowed, int handIndex = 0, int handCount = 1) =>
        new(Cards.Parse("8 8"), 16, false, Cards.ParseOne("9"), allowed, handIndex, handCount);

    private int Count(string text) => _output.ToString().Split(text).Length - 1;

    [Fact]
    public async Task ValidBet() =>
        Assert.Equal(25m, await Player("25").GetBetAsync(Bet(), Ct));

    [Fact]
    public async Task CommaAsDecimalSeparator() =>
        Assert.Equal(12.5m, await Player("12,5").GetBetAsync(Bet(), Ct));

    [Fact]
    public async Task PromptShowsBankrollLimitsAndDefault()
    {
        await Player("25").GetBetAsync(Bet(), Ct);

        Assert.StartsWith("Bankroll: 1000   Your bet (10–500, Enter = 10, q = quit): ", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnterUsesMinBetFirstThenLastBet()
    {
        var player = Player("", "25", "");

        Assert.Equal(10m, await player.GetBetAsync(Bet(), Ct));
        Assert.Equal(25m, await player.GetBetAsync(Bet(), Ct));
        Assert.Equal(25m, await player.GetBetAsync(Bet(), Ct));
    }

    [Fact]
    public async Task DefaultBetIsLimitedByBankroll()
    {
        var player = Player("400", "");

        await player.GetBetAsync(Bet(1000m), Ct);

        Assert.Equal(300m, await player.GetBetAsync(Bet(300m), Ct));
    }

    [Fact]
    public async Task InvalidBetsRepeatThePrompt()
    {
        var bet = await Player("abc", "5", "600", "25").GetBetAsync(Bet(), Ct);

        Assert.Equal(25m, bet);
        Assert.Equal(4, Count("Your bet"));
        Assert.Equal(1, Count("Please enter an amount, or q to quit."));
        Assert.Equal(2, Count("The bet must be between 10 and 500."));
    }

    [Fact]
    public async Task BetAboveBankrollIsRejected()
    {
        var bet = await Player("150", "50").GetBetAsync(Bet(100m), Ct);

        Assert.Equal(50m, bet);
        Assert.Contains("The bet must be between 10 and 100.", _output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("q")]
    [InlineData("Q")]
    public async Task QuitAtBetPrompt(string input)
    {
        var player = Player(input);

        Assert.Null(await player.GetBetAsync(Bet(), Ct));
        Assert.True(player.QuitRequested);
    }

    [Fact]
    public async Task EndOfInputAtBetPromptQuits()
    {
        var player = Player();

        Assert.Null(await player.GetBetAsync(Bet(), Ct));
        Assert.True(player.QuitRequested);
    }

    [Theory]
    [InlineData("h", PlayerAction.Hit)]
    [InlineData("H", PlayerAction.Hit)]
    [InlineData("s", PlayerAction.Stand)]
    [InlineData(" S ", PlayerAction.Stand)]
    public async Task Actions(string input, PlayerAction expected) =>
        Assert.Equal(expected, await Player(input).DecideAsync(Decision(), Ct));

    [Fact]
    public async Task InvalidActionRepeatsThePrompt()
    {
        var action = await Player("x", "hit", "h").DecideAsync(Decision(), Ct);

        Assert.Equal(PlayerAction.Hit, action);
        Assert.Equal(3, Count("(h)it, (s)tand, (q)uit after this round: "));
        Assert.Equal(2, Count("Please enter h, s or q."));
    }

    [Fact]
    public async Task QuitDuringTurnAsksAgainAndLeavesAfterTheRound()
    {
        var player = Player("q", "s");

        Assert.Equal(PlayerAction.Stand, await player.DecideAsync(Decision(), Ct));
        Assert.True(player.QuitRequested);
        Assert.Contains("You will leave the table after this round.", _output.ToString(), StringComparison.Ordinal);

        Assert.Null(await player.GetBetAsync(Bet(), Ct));
        Assert.Equal(0, Count("Your bet"));
    }

    [Fact]
    public async Task EndOfInputDuringTurnStandsAndQuits()
    {
        var player = Player();

        Assert.Equal(PlayerAction.Stand, await player.DecideAsync(Decision(), Ct));
        Assert.True(player.QuitRequested);
    }

    [Fact]
    public async Task CancellationStopsWaitingForInput()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var player = new ConsolePlayer(new BlockingReader(cts.Cancel), _output);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => player.DecideAsync(Decision(), cts.Token));
    }

    [Fact]
    public async Task PromptListsOnlyAllowedActions()
    {
        await Player("s").DecideAsync(Decision(All), Ct);

        Assert.Equal("(h)it, (s)tand, (d)ouble, s(p)lit, (q)uit after this round: ", _output.ToString());
    }

    [Theory]
    [InlineData("d", PlayerAction.Double)]
    [InlineData("D", PlayerAction.Double)]
    [InlineData("p", PlayerAction.Split)]
    [InlineData("P", PlayerAction.Split)]
    public async Task DoubleAndSplitCommands(string input, PlayerAction expected) =>
        Assert.Equal(expected, await Player(input).DecideAsync(Decision(All), Ct));

    [Fact]
    public async Task CommandThatIsNotAllowedRepeatsThePrompt()
    {
        var action = await Player("d", "p", "s").DecideAsync(Decision(), Ct);

        Assert.Equal(PlayerAction.Stand, action);
        Assert.Equal(1, Count("Double is not possible now. Please enter h, s or q."));
        Assert.Equal(1, Count("Split is not possible now. Please enter h, s or q."));
    }

    [Fact]
    public async Task PromptNamesTheHandAfterASplit()
    {
        await Player("s").DecideAsync(Decision(HitOrStand, handIndex: 1, handCount: 2), Ct);

        Assert.StartsWith("Hand 2 – (h)it, (s)tand, (q)uit after this round: ", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResplittableAcesOfferOnlyStandAndSplit()
    {
        var standOrSplit = new HashSet<PlayerAction> { PlayerAction.Stand, PlayerAction.Split };

        var action = await Player("h", "p").DecideAsync(Decision(standOrSplit), Ct);

        Assert.Equal(PlayerAction.Split, action);
        Assert.StartsWith("(s)tand, s(p)lit, (q)uit after this round: ", _output.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, Count("Hit is not possible now. Please enter s, p or q."));
    }
}
