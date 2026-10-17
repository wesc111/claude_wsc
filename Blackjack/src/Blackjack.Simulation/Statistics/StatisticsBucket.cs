namespace Blackjack.Simulation.Statistics;

/// <summary>
/// Accumulates the results of a set of rounds (all rounds, or one breakdown row).
/// Money totals are <c>decimal</c>; the variance of the per-round result is computed in <c>double</c>, in units of the bet.
/// </summary>
internal sealed class StatisticsBucket
{
    private double _sumResult;
    private double _sumResultSquared;

    /// <summary>Number of rounds.</summary>
    public long Rounds { get; private set; }

    /// <summary>Number of player hands (equal to <see cref="Rounds"/> until splits exist).</summary>
    public long Hands { get; private set; }

    /// <summary>Sum of the initial bets (before Double and Split); the base of the house edge.</summary>
    public decimal InitialBets { get; private set; }

    /// <summary>Sum of all bets, including the additional bets of Double and Split.</summary>
    public decimal Wagered { get; private set; }

    /// <summary>Sum of all net results.</summary>
    public decimal Net { get; private set; }

    /// <summary>Number of won hands.</summary>
    public long Wins { get; private set; }

    /// <summary>Number of lost hands.</summary>
    public long Losses { get; private set; }

    /// <summary>Number of pushed hands.</summary>
    public long Pushes { get; private set; }

    /// <summary>Number of player Blackjacks.</summary>
    public long Blackjacks { get; private set; }

    /// <summary>Number of busted player hands.</summary>
    public long PlayerBusts { get; private set; }

    /// <summary>Number of rounds in which the dealer played out the hand.</summary>
    public long DealerPlayed { get; private set; }

    /// <summary>Number of rounds in which the dealer busted.</summary>
    public long DealerBusts { get; private set; }

    /// <summary>Number of rounds with at least one doubled hand.</summary>
    public long DoubleRounds { get; private set; }

    /// <summary>Number of rounds with at least one split.</summary>
    public long SplitRounds { get; private set; }

    /// <summary>House edge in %: −net / initial bets (SPEC 7.7). <c>NaN</c> without rounds.</summary>
    public double HouseEdgePercent => InitialBets == 0 ? double.NaN : (double)(-Net / InitialBets) * 100;

    /// <summary>Standard error of the per-round result, in % of the initial bet. <c>NaN</c> with fewer than 2 rounds.</summary>
    public double StandardErrorPercent
    {
        get
        {
            if (Rounds < 2)
            {
                return double.NaN;
            }

            var n = (double)Rounds;
            var variance = (_sumResultSquared - (_sumResult * _sumResult / n)) / (n - 1);
            return Math.Sqrt(Math.Max(variance, 0) / n) * 100;
        }
    }

    /// <summary>Lower bound of the 95 % confidence interval of the house edge, in %.</summary>
    public double ConfidenceLowPercent => HouseEdgePercent - (1.96 * StandardErrorPercent);

    /// <summary>Upper bound of the 95 % confidence interval of the house edge, in %.</summary>
    public double ConfidenceHighPercent => HouseEdgePercent + (1.96 * StandardErrorPercent);

    /// <summary>Won hands in %.</summary>
    public double WinPercent => Percent(Wins, Hands);

    /// <summary>Lost hands in %.</summary>
    public double LossPercent => Percent(Losses, Hands);

    /// <summary>Pushed hands in %.</summary>
    public double PushPercent => Percent(Pushes, Hands);

    /// <summary>Player Blackjacks in % of rounds.</summary>
    public double BlackjackPercent => Percent(Blackjacks, Rounds);

    /// <summary>Busted player hands in %.</summary>
    public double PlayerBustPercent => Percent(PlayerBusts, Hands);

    /// <summary>Dealer busts in % of the rounds in which the dealer played out the hand.</summary>
    public double DealerBustPercent => Percent(DealerBusts, DealerPlayed);

    /// <summary>Rounds with a doubled hand in % of rounds.</summary>
    public double DoublePercent => Percent(DoubleRounds, Rounds);

    /// <summary>Rounds with a split in % of rounds.</summary>
    public double SplitPercent => Percent(SplitRounds, Rounds);

    /// <summary>Adds one classified round.</summary>
    public void Add(RoundResult result, in RoundFacts facts)
    {
        Rounds++;
        InitialBets += result.InitialBet;
        Wagered += result.TotalBet;
        Net += result.Net;

        var perRound = (double)(result.Net / result.InitialBet);
        _sumResult += perRound;
        _sumResultSquared += perRound * perRound;

        if (result.PlayerHands.Count > 1)
        {
            SplitRounds++;
        }

        var doubled = false;
        foreach (var hand in result.PlayerHands)
        {
            Hands++;
            doubled |= hand.IsDoubled;
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

            if (hand.Outcome == HandOutcome.PlayerBust)
            {
                PlayerBusts++;
            }
        }

        if (doubled)
        {
            DoubleRounds++;
        }

        if (facts.PlayerBlackjack)
        {
            Blackjacks++;
        }

        if (facts.DealerPlayed)
        {
            DealerPlayed++;
            if (facts.DealerBust)
            {
                DealerBusts++;
            }
        }
    }

    /// <summary>Adds all rounds of <paramref name="other"/>.</summary>
    public void Merge(StatisticsBucket other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Rounds += other.Rounds;
        Hands += other.Hands;
        InitialBets += other.InitialBets;
        Wagered += other.Wagered;
        Net += other.Net;
        Wins += other.Wins;
        Losses += other.Losses;
        Pushes += other.Pushes;
        Blackjacks += other.Blackjacks;
        PlayerBusts += other.PlayerBusts;
        DealerPlayed += other.DealerPlayed;
        DealerBusts += other.DealerBusts;
        DoubleRounds += other.DoubleRounds;
        SplitRounds += other.SplitRounds;
        _sumResult += other._sumResult;
        _sumResultSquared += other._sumResultSquared;
    }

    private static double Percent(long count, long total) => total == 0 ? double.NaN : 100.0 * count / total;
}
