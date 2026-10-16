using System.Globalization;
using Blackjack.Simulation.Statistics;

namespace Blackjack.Simulation;

/// <summary>Writes the statistics as one CSV table (invariant culture).</summary>
internal static class CsvWriter
{
    /// <summary>The header line.</summary>
    public const string Header = "Category,Key,Rounds,Wagered,Net,HouseEdgePct,WinPct,LossPct,PushPct,BlackjackPct,PlayerBustPct,DealerBustPct";

    /// <summary>Writes the header, the total row and one row per breakdown key.</summary>
    public static void Write(TextWriter output, SimulationStatistics statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        output.WriteLine(Header);
        WriteRow(output, "Total", "", statistics.Total);
        for (var i = 0; i < Breakdown.UpcardKeys.Count; i++)
        {
            WriteRow(output, "DealerUpcard", Breakdown.UpcardKeys[i], statistics.ByDealerUpcard[i]);
        }

        for (var i = 0; i < Breakdown.StartingTotalKeys.Count; i++)
        {
            WriteRow(output, "StartingTotal", Breakdown.StartingTotalKeys[i], statistics.ByStartingTotal[i]);
        }
    }

    private static void WriteRow(TextWriter output, string category, string key, StatisticsBucket b)
    {
        var inv = CultureInfo.InvariantCulture;
        string[] fields =
        [
            category,
            key,
            b.Rounds.ToString(inv),
            b.Wagered.ToString("0.##", inv),
            b.Net.ToString("0.##", inv),
            Pct(b.HouseEdgePercent),
            Pct(b.WinPercent),
            Pct(b.LossPercent),
            Pct(b.PushPercent),
            Pct(b.BlackjackPercent),
            Pct(b.PlayerBustPercent),
            Pct(b.DealerBustPercent),
        ];
        output.WriteLine(string.Join(",", fields));
    }

    // Empty field when there is nothing to divide by.
    private static string Pct(double value) => double.IsNaN(value) ? "" : value.ToString("0.####", CultureInfo.InvariantCulture);
}
