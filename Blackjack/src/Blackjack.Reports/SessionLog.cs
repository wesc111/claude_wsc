namespace Blackjack.Reports;

/// <summary>
/// Collects the completed rounds of one playing session with their time, plus the session statistics.
/// Record only the results that <see cref="BlackjackGame.PlayRoundAsync"/> returns, so aborted rounds are never logged.
/// </summary>
public sealed class SessionLog
{
    private readonly List<LoggedRound> _rounds = [];
    private readonly Func<DateTimeOffset> _clock;

    /// <summary>Starts a session log now.</summary>
    /// <param name="rules">The table rules of the session.</param>
    /// <param name="startingBankroll">The bankroll at the start, or <c>null</c> if unlimited.</param>
    /// <param name="clock">The time source; the local time by default.</param>
    public SessionLog(Rules rules, decimal? startingBankroll, Func<DateTimeOffset>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Rules = rules;
        _clock = clock ?? (() => DateTimeOffset.Now);
        StartedAt = _clock();
        Statistics = new SessionStatistics(startingBankroll);
    }

    /// <summary>The table rules of the session.</summary>
    public Rules Rules { get; }

    /// <summary>When the session started.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>The statistics of the recorded rounds.</summary>
    public SessionStatistics Statistics { get; }

    /// <summary>The recorded rounds in order.</summary>
    public IReadOnlyList<LoggedRound> Rounds => _rounds;

    /// <summary>The current time of the log's clock.</summary>
    public DateTimeOffset Now => _clock();

    /// <summary>Records a completed round.</summary>
    /// <param name="result">The result returned by <see cref="BlackjackGame.PlayRoundAsync"/>.</param>
    public void Record(RoundResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Statistics.Record(result);
        _rounds.Add(new LoggedRound(_rounds.Count + 1, _clock(), result));
    }
}
