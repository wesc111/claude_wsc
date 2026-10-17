using Blackjack.Reports;

namespace Blackjack.ConsoleApp;

/// <summary>Plays rounds until the player leaves or cannot afford the minimum bet, then shows the session summary.</summary>
internal sealed class GameSession
{
    private readonly ConsoleOptions _options;
    private readonly Rules _rules;
    private readonly BlackjackGame _game;
    private readonly ConsolePlayer _player;
    private readonly TextWriter _output;
    private readonly SessionLog _log;
    private readonly string? _logDirectory;

    /// <summary>Creates a session with its own game, player and display.</summary>
    /// <param name="options">The command-line options.</param>
    /// <param name="cards">The shoe.</param>
    /// <param name="input">Where bets and actions are read from.</param>
    /// <param name="output">Where the table is shown.</param>
    /// <param name="useColor">Whether red suits are shown in red.</param>
    /// <param name="logDirectory">Where the statistics files are written at the end; <c>null</c> = no files.</param>
    /// <param name="clock">The time source of the log; the local time by default.</param>
    public GameSession(ConsoleOptions options, ICardSource cards, TextReader input, TextWriter output, bool useColor,
                       string? logDirectory = null, Func<DateTimeOffset>? clock = null)
    {
        _options = options;
        _output = output;
        _rules = options.CreateRules();
        var renderer = new TableRenderer(output, new CardFormatter(useColor));
        _game = new BlackjackGame(_rules, cards, options.Bankroll, [renderer]);
        _player = new ConsolePlayer(input, output, cards);
        _logDirectory = logDirectory;
        _log = new SessionLog(_rules, options.Bankroll, clock);
    }

    /// <summary>The statistics of the completed rounds.</summary>
    public SessionStatistics Statistics => _log.Statistics;

    /// <summary>The log of the completed rounds.</summary>
    public SessionLog Log => _log;

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

                _log.Record(result);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _output.WriteLine();
            _output.WriteLine("Game aborted; the current round does not count.");
        }

        WriteSummary();
        SaveLog();
    }

    private void SaveLog()
    {
        if (_logDirectory is null || _log.Rounds.Count == 0)
        {
            return;
        }

        try
        {
            var path = SessionLogFiles.Save(_log, _logDirectory);
            _output.WriteLine($"  Statistics saved to {SessionLogFiles.DisplayPath(path)} (and two CSV files).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _output.WriteLine($"  Could not write the statistics files: {ex.Message}");
        }
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
