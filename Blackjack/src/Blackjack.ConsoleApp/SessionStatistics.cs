namespace Blackjack.ConsoleApp;

/// <summary>Counts the completed rounds of a session. Aborted rounds are never recorded.</summary>
internal sealed class SessionStatistics
{
    /// <summary>Number of completed rounds.</summary>
    public int Rounds { get; private set; }

    /// <summary>Number of won hands.</summary>
    public int Wins { get; private set; }

    /// <summary>Number of lost hands.</summary>
    public int Losses { get; private set; }

    /// <summary>Number of pushed hands.</summary>
    public int Pushes { get; private set; }

    /// <summary>Sum of the net results.</summary>
    public decimal Net { get; private set; }

    /// <summary>Records a completed round.</summary>
    public void Record(RoundResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Rounds++;
        Net += result.Net;
        foreach (var hand in result.PlayerHands)
        {
            if (hand.Outcome.IsWin())
            {
                Wins++;
            }
            else if (hand.Outcome.IsLoss())
            {
                Losses++;
            }
            else
            {
                Pushes++;
            }
        }
    }
}
