namespace Blackjack.ConsoleApp;

/// <summary>Plays rounds until the player leaves or cannot afford the minimum bet, then shows the session summary.</summary>
internal sealed class GameSession
{
    private readonly ConsoleOptions _options;
    private readonly Rules _rules;
    private readonly BlackjackGame _game;
    private readonly ConsolePlayer _player;
    private readonly TextWriter _output;

    /// <summary>Creates a session with its own game, player and display.</summary>
    public GameSession(ConsoleOptions options, ICardSource cards, TextReader input, TextWriter output, bool useColor)
    {
        _options = options;
        _output = output;
        _rules = new Rules { MinBet = options.MinBet, MaxBet = options.MaxBet };
        var renderer = new TableRenderer(output, new CardFormatter(useColor));
        _game = new BlackjackGame(_rules, cards, options.Bankroll, [renderer]);
        _player = new ConsolePlayer(input, output);
    }

    /// <summary>The statistics of the completed rounds.</summary>
    public SessionStatistics Statistics { get; } = new();

    /// <summary>The current bankroll.</summary>
    public decimal Bankroll => _game.Bankroll ?? 0m;

    /// <summary>Runs the session. Cancelling <paramref name="ct"/> (Ctrl+C) aborts the current round and ends the session.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        _output.WriteLine($"Blackjack – bankroll {Money.Format(_options.Bankroll)}, bets {Money.Format(_rules.MinBet)}–{Money.Format(_rules.MaxBet)}");
        _output.WriteLine();

        try
        {
            while (true)
            {
                if (Bankroll < _rules.MinBet)
                {
                    _output.WriteLine("Your bankroll is below the minimum bet.");
                    break;
                }

                var result = await _game.PlayRoundAsync(_player, ct);
                if (result is null)
                {
                    break;
                }

                Statistics.Record(result);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _output.WriteLine();
            _output.WriteLine("Game aborted; the current round does not count.");
        }

        WriteSummary();
    }

    private void WriteSummary()
    {
        var s = Statistics;
        _output.WriteLine();
        _output.WriteLine("Session summary");
        _output.WriteLine($"  Rounds: {s.Rounds}   Wins: {s.Wins}   Losses: {s.Losses}   Pushes: {s.Pushes}");
        _output.WriteLine($"  Net result: {Money.FormatSigned(s.Net)}   Final bankroll: {Money.Format(Bankroll)}");
    }
}
