namespace Blackjack.ConsoleApp.Tests;

/// <summary>End-to-end sessions with a predefined card sequence and scripted input.</summary>
public sealed class GameSessionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly StringWriter _output = new() { NewLine = "\n" };

    private async Task<GameSession> RunAsync(string cards, TextReader input, ConsoleOptions? options = null, CancellationToken? ct = null,
                                             string? logDirectory = null)
    {
        var session = new GameSession(options ?? new ConsoleOptions(), new FixedCardSource(cards), input, _output, useColor: false, logDirectory);
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

            Shoe: 4 of 4 cards left.
            Bankroll: 1000   Your bet (10–500, Enter = 10, q = quit): 
            Dealer:  K♠ [??]
            You:     10♠ 9♠   (19)
            (h)it, (s)tand, (d)ouble, (q)uit after this round: Dealer:  K♠ 8♠   (18)
            Result:  You win  +25   Bankroll: 1025

            Shoe: 0 of 4 cards left.
            Bankroll: 1025   Your bet (10–500, Enter = 25, q = quit): 
            Session summary
              Rounds: 1   Wins: 1   Losses: 0   Pushes: 0
              Net result: +25   Final bankroll: 1025

            """.ReplaceLineEndings("\n"),
            Output);
    }

    [Fact]
    public async Task SplitAndDoubleRound()
    {
        // 8 8 split; hand 1 draws 3 → 11 and doubles (10 → 21); hand 2 draws 2 → 10 and stands; dealer 10 7.
        var session = await RunAsync("8 10 8 7 3 10 2", "10", "p", "d", "s", "q");

        Assert.Equal(
            """
            Dealer:  10♠ [??]
            You:     8♠ 8♠   (16)
            (h)it, (s)tand, (d)ouble, s(p)lit, (q)uit after this round: You split into Hand 1 and Hand 2.
            Hand 1:  8♠ 3♠   (11)
            Hand 1 – (h)it, (s)tand, (d)ouble, (q)uit after this round: Hand 1 doubled: bet 20, one more card.
            Hand 1:  8♠ 3♠ 10♠   (21)
            Hand 2:  8♠ 2♠   (10)
            Hand 2 – (h)it, (s)tand, (d)ouble, (q)uit after this round: Dealer:  10♠ 7♠   (17)
            Hand 1:  You win  +20
            Hand 2:  You lose  -10
            Result:  +10   Bankroll: 1010
            """.ReplaceLineEndings("\n"),
            string.Join("\n", Output.Split('\n').SkipWhile(l => !l.StartsWith("Dealer:", StringComparison.Ordinal)).Take(11)));
        Assert.Equal(1, session.Statistics.Rounds);
        Assert.Equal(1, session.Statistics.Wins);
        Assert.Equal(1, session.Statistics.Losses);
        Assert.Equal(1010m, session.Bankroll);
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

    private static string TempLogDirectory() =>
        Path.Combine(Path.GetTempPath(), "blackjack-console-tests-" + Guid.NewGuid().ToString("N"), "log");

    private static void DeleteRoot(string logDirectory)
    {
        var root = Path.GetDirectoryName(logDirectory)!;
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StatisticsFilesAreWrittenWhenThePlayerQuits()
    {
        var log = TempLogDirectory();
        try
        {
            await RunAsync("10 K 9 8", new StringReader("25\ns\nq\n"), logDirectory: log);

            var files = Directory.GetFiles(log).Select(Path.GetFileName).ToList();
            Assert.Equal(3, files.Count);
            Assert.Contains(files, f => f!.EndsWith(".txt", StringComparison.Ordinal));
            Assert.Contains(files, f => f!.EndsWith("_statistics.csv", StringComparison.Ordinal));
            Assert.Contains(files, f => f!.EndsWith("_rounds.csv", StringComparison.Ordinal));
            Assert.Contains("  Statistics saved to ", Output, StringComparison.Ordinal);
            Assert.Contains("(and two CSV files).", Output, StringComparison.Ordinal);
            var text = await File.ReadAllTextAsync(Directory.GetFiles(log, "*.txt").Single(), Ct);
            Assert.Contains("Rounds                   1   (1 hands)", text, StringComparison.Ordinal);
        }
        finally
        {
            DeleteRoot(log);
        }
    }

    [Fact]
    public async Task NoFilesWithoutARound()
    {
        var log = TempLogDirectory();
        try
        {
            await RunAsync("10 K 9 8", new StringReader("q\n"), logDirectory: log);

            Assert.False(Directory.Exists(log));
            Assert.DoesNotContain("Statistics saved", Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteRoot(log);
        }
    }

    [Fact]
    public async Task StatisticsFilesAreWrittenWhenTheBankrollRunsOut()
    {
        var log = TempLogDirectory();
        try
        {
            await RunAsync("10 K 7 8", new StringReader("\ns\n"), new ConsoleOptions { Bankroll = 10m }, logDirectory: log);

            Assert.Contains("Your bankroll is below the minimum bet.", Output, StringComparison.Ordinal);
            Assert.Equal(3, Directory.GetFiles(log).Length);
        }
        finally
        {
            DeleteRoot(log);
        }
    }

    [Fact]
    public async Task StatisticsFilesAreWrittenAfterCtrlC()
    {
        var log = TempLogDirectory();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            // One round is played, then Ctrl+C arrives at the next bet prompt.
            var input = new BlockingReader(cts.Cancel, "25", "s");

            await RunAsync("10 K 9 8", input, ct: cts.Token, logDirectory: log);

            Assert.Contains("Game aborted", Output, StringComparison.Ordinal);
            var rounds = await File.ReadAllLinesAsync(Directory.GetFiles(log, "*_rounds.csv").Single(), Ct);
            Assert.Equal(2, rounds.Length);
        }
        finally
        {
            DeleteRoot(log);
        }
    }

    [Fact]
    public async Task WriteErrorIsReportedAndTheProgramContinues()
    {
        var log = TempLogDirectory();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            await File.WriteAllTextAsync(log, "a file where the directory should be", Ct);

            await RunAsync("10 K 9 8", new StringReader("25\ns\nq\n"), logDirectory: log);

            Assert.Contains("  Could not write the statistics files: ", Output, StringComparison.Ordinal);
            Assert.Contains("Session summary", Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteRoot(log);
        }
    }
}
