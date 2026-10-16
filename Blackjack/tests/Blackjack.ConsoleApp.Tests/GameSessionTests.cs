namespace Blackjack.ConsoleApp.Tests;

/// <summary>End-to-end sessions with a predefined card sequence and scripted input.</summary>
public sealed class GameSessionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly StringWriter _output = new() { NewLine = "\n" };

    private async Task<GameSession> RunAsync(string cards, TextReader input, ConsoleOptions? options = null, CancellationToken? ct = null)
    {
        var session = new GameSession(options ?? new ConsoleOptions(), new FixedCardSource(cards), input, _output, useColor: false);
        await session.RunAsync(ct ?? Ct);
        return session;
    }

    private Task<GameSession> RunAsync(string cards, params string[] lines) =>
        RunAsync(cards, new StringReader(string.Concat(lines.Select(l => l + "\n"))));

    private string Output => _output.ToString();

    [Fact]
    public async Task CompleteSessionTranscript()
    {
        await RunAsync("10 K 9 8", "25", "s", "q");

        Assert.Equal(
            """
            Blackjack – bankroll 1000, bets 10–500

            Bankroll: 1000   Your bet (10–500, Enter = 10, q = quit): 
            Dealer:  K♠ [??]
            You:     10♠ 9♠   (19)
            (h)it, (s)tand, (q)uit after this round: Dealer:  K♠ 8♠   (18)
            Result:  You win  +25   Bankroll: 1025

            Bankroll: 1025   Your bet (10–500, Enter = 25, q = quit): 
            Session summary
              Rounds: 1   Wins: 1   Losses: 0   Pushes: 0
              Net result: +25   Final bankroll: 1025

            """.ReplaceLineEndings("\n"),
            Output);
    }

    [Fact]
    public async Task SeveralRoundsAreCounted()
    {
        // Win (19 vs 18), loss by bust, push (18 vs 18).
        var session = await RunAsync("10 K 9 8  10 K 6 7 9  10 K 8 8", "10", "s", "20", "h", "", "s", "q");

        Assert.Equal(3, session.Statistics.Rounds);
        Assert.Equal(1, session.Statistics.Wins);
        Assert.Equal(1, session.Statistics.Losses);
        Assert.Equal(1, session.Statistics.Pushes);
        Assert.Equal(-10m, session.Statistics.Net);
        Assert.Equal(990m, session.Bankroll);
        Assert.Contains("Rounds: 3   Wins: 1   Losses: 1   Pushes: 1", Output, StringComparison.Ordinal);
        Assert.Contains("Net result: -10   Final bankroll: 990", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DealerBlackjackNeedsNoDecision()
    {
        await RunAsync("10 A 7 K", "10", "q");

        Assert.Contains("Dealer:  A♠ K♠   (Blackjack)", Output, StringComparison.Ordinal);
        Assert.Contains("Result:  Dealer has Blackjack – you lose  -10   Bankroll: 990", Output, StringComparison.Ordinal);
        Assert.DoesNotContain("(h)it", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QuitDuringTurnEndsSessionAfterTheRound()
    {
        var session = await RunAsync("10 K 9 8", "25", "q", "s");

        Assert.Equal(1, session.Statistics.Rounds);
        Assert.Contains("You will leave the table after this round.", Output, StringComparison.Ordinal);
        Assert.Single(Output.Split("Your bet").Skip(1));
    }

    [Fact]
    public async Task GameEndsWhenBankrollIsBelowMinBet()
    {
        var session = await RunAsync(
            "10 K 7 8",
            new StringReader("\ns\n"),
            new ConsoleOptions { Bankroll = 10m });

        Assert.Equal(1, session.Statistics.Rounds);
        Assert.Equal(0m, session.Bankroll);
        Assert.Contains("Your bankroll is below the minimum bet.", Output, StringComparison.Ordinal);
        Assert.Contains("Net result: -10   Final bankroll: 0", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EndOfInputEndsSession()
    {
        var session = await RunAsync("10 K 9 8");

        Assert.Equal(0, session.Statistics.Rounds);
        Assert.Contains("Session summary", Output, StringComparison.Ordinal);
        Assert.Contains("Final bankroll: 1000", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationDuringRoundRestoresBankrollAndShowsSummary()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        // The bet is entered, then the player hesitates and Ctrl+C is pressed.
        var input = new BlockingReader(cts.Cancel, "100");

        var session = await RunAsync("10 K 9 8", input, ct: cts.Token);

        Assert.Equal(0, session.Statistics.Rounds);
        Assert.Equal(1000m, session.Bankroll);
        Assert.Contains("Game aborted; the current round does not count.", Output, StringComparison.Ordinal);
        Assert.Contains("Rounds: 0", Output, StringComparison.Ordinal);
        Assert.Contains("Final bankroll: 1000", Output, StringComparison.Ordinal);
    }

    [Fact(Timeout = 10_000)]
    public async Task CancellationWorksWhileConsoleReadBlocks()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        // Like Console.In, this reader blocks the calling thread; Ctrl+C arrives from another thread.
        using var input = new SynchronousBlockingReader(() => cts.CancelAfter(50), "100");

        var session = await RunAsync("10 K 9 8", input, ct: cts.Token);

        Assert.Equal(1000m, session.Bankroll);
        Assert.Contains("Game aborted; the current round does not count.", Output, StringComparison.Ordinal);
        Assert.Contains("Session summary", Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CustomLimitsAreUsed()
    {
        await RunAsync("10 K 9 8", new StringReader("q\n"), new ConsoleOptions { Bankroll = 300m, MinBet = 5m, MaxBet = 50m });

        Assert.Contains("Blackjack – bankroll 300, bets 5–50", Output, StringComparison.Ordinal);
        Assert.Contains("Your bet (5–50, Enter = 5, q = quit)", Output, StringComparison.Ordinal);
    }
}
