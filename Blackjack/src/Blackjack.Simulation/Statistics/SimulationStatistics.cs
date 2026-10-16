namespace Blackjack.Simulation.Statistics;

/// <summary>All statistics of a simulation: the total and the breakdowns by dealer upcard and starting total.</summary>
internal sealed class SimulationStatistics
{
    private readonly StatisticsBucket[] _byUpcard = NewBuckets(Breakdown.UpcardKeys.Count);
    private readonly StatisticsBucket[] _byStartingTotal = NewBuckets(Breakdown.StartingTotalKeys.Count);

    /// <summary>All rounds.</summary>
    public StatisticsBucket Total { get; } = new();

    /// <summary>One bucket per key of <see cref="Breakdown.UpcardKeys"/>, in the same order.</summary>
    public IReadOnlyList<StatisticsBucket> ByDealerUpcard => _byUpcard;

    /// <summary>One bucket per key of <see cref="Breakdown.StartingTotalKeys"/>, in the same order.</summary>
    public IReadOnlyList<StatisticsBucket> ByStartingTotal => _byStartingTotal;

    /// <summary>The bucket of the dealer upcard <paramref name="key"/> (e.g. "10", "A").</summary>
    public StatisticsBucket DealerUpcard(string key) => _byUpcard[IndexOf(Breakdown.UpcardKeys, key)];

    /// <summary>The bucket of the starting total <paramref name="key"/> (e.g. "soft 17", "Blackjack").</summary>
    public StatisticsBucket StartingTotal(string key) => _byStartingTotal[IndexOf(Breakdown.StartingTotalKeys, key)];

    /// <summary>Records one round.</summary>
    public void Record(RoundResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var facts = RoundFacts.From(result);
        var cards = result.PlayerHands[0].Cards;
        Total.Add(result, facts);
        _byUpcard[Breakdown.UpcardIndex(result.DealerCards[0])].Add(result, facts);
        _byStartingTotal[Breakdown.StartingTotalIndex(cards[0], cards[1])].Add(result, facts);
    }

    /// <summary>Adds all rounds of <paramref name="other"/>.</summary>
    public void Merge(SimulationStatistics other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Total.Merge(other.Total);
        for (var i = 0; i < _byUpcard.Length; i++)
        {
            _byUpcard[i].Merge(other._byUpcard[i]);
        }

        for (var i = 0; i < _byStartingTotal.Length; i++)
        {
            _byStartingTotal[i].Merge(other._byStartingTotal[i]);
        }
    }

    private static StatisticsBucket[] NewBuckets(int count) => [.. Enumerable.Range(0, count).Select(_ => new StatisticsBucket())];

    private static int IndexOf(IReadOnlyList<string> keys, string key)
    {
        for (var i = 0; i < keys.Count; i++)
        {
            if (keys[i] == key)
            {
                return i;
            }
        }

        throw new ArgumentException($"Unknown breakdown key '{key}'.", nameof(key));
    }
}
