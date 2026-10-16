using System.Globalization;
using Blackjack.Simulation.Statistics;

namespace Blackjack.Simulation;

/// <summary>Writes the simulation report as formatted text.</summary>
internal static class ReportWriter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Writes the full report.</summary>
    public static void Write(TextWriter output, SimulationOptions options, int seed, SimulationResult result)
    {
        var rules = options.CreateRules();
        var total = result.Statistics.Total;

        output.WriteLine($"Strategy: {options.Strategy}   Rounds: {Count(total.Rounds)}   Bet: {Money(options.Bet)}   Seed: {seed}   Workers: {options.Workers}");
        output.WriteLine($"Rules: {DescribeRules(rules)}");
        output.WriteLine($"Time: {result.Elapsed.TotalSeconds.ToString("0.00", Invariant)} s ({Count((long)result.RoundsPerSecond)} rounds/s)");
        output.WriteLine();
        output.WriteLine($"Total wagered      {Money(total.Wagered)}");
        output.WriteLine($"Net result         {Money(total.Net)}");
        output.WriteLine($"House edge         {Percent(total.HouseEdgePercent)}   (SE {Percent(total.StandardErrorPercent)}, 95 % CI {Percent(total.ConfidenceLowPercent)} to {Percent(total.ConfidenceHighPercent)})");
        output.WriteLine($"Win / loss / push  {Percent(total.WinPercent)} / {Percent(total.LossPercent)} / {Percent(total.PushPercent)}");
        output.WriteLine($"Blackjack rate     {Percent(total.BlackjackPercent)}");
        output.WriteLine($"Player bust rate   {Percent(total.PlayerBustPercent)}");
        output.WriteLine($"Dealer bust rate   {Percent(total.DealerBustPercent)}   (of {Count(total.DealerPlayed)} rounds the dealer played out)");
        output.WriteLine();
        WriteBreakdown(output, "By dealer upcard", "Upcard", Breakdown.UpcardKeys, result.Statistics.ByDealerUpcard);
        output.WriteLine();
        WriteBreakdown(output, "By player starting total", "Start", Breakdown.StartingTotalKeys, result.Statistics.ByStartingTotal);
    }

    /// <summary>Describes the rules, e.g. <c>6 decks, S17, 3:2, penetration 75 %</c>.</summary>
    public static string DescribeRules(Rules rules) =>
        $"{rules.Decks} deck{(rules.Decks == 1 ? "" : "s")}, {(rules.DealerHitsSoft17 ? "H17" : "S17")}, " +
        $"{DescribePayout(rules.BlackjackPayout)}, penetration {(rules.Penetration * 100).ToString("0.##", Invariant)} %";

    /// <summary>Describes a payout as a ratio such as <c>3:2</c> or <c>6:5</c> where possible.</summary>
    public static string DescribePayout(decimal payout)
    {
        for (var denominator = 1; denominator <= 10; denominator++)
        {
            var numerator = payout * denominator;
            if (numerator == decimal.Truncate(numerator))
            {
                return $"{numerator.ToString("0", Invariant)}:{denominator}";
            }
        }

        return $"{payout.ToString("0.####", Invariant)}:1";
    }

    private static void WriteBreakdown(TextWriter output, string title, string keyHeader, IReadOnlyList<string> keys, IReadOnlyList<StatisticsBucket> buckets)
    {
        output.WriteLine(title);
        output.WriteLine($"{keyHeader,-10}{"Rounds",12}{"Edge %",10}{"Win %",9}{"Loss %",9}{"Push %",9}");
        for (var i = 0; i < keys.Count; i++)
        {
            var b = buckets[i];
            if (b.Rounds == 0)
            {
                continue;
            }

            output.WriteLine($"{keys[i],-10}{Count(b.Rounds),12}{Number(b.HouseEdgePercent),10}{Number(b.WinPercent),9}{Number(b.LossPercent),9}{Number(b.PushPercent),9}");
        }
    }

    private static string Count(long value) => value.ToString("N0", Invariant);

    private static string Money(decimal value) => value.ToString("#,0.##", Invariant);

    private static string Number(double value) => double.IsNaN(value) ? "n/a" : value.ToString("0.00", Invariant);

    private static string Percent(double value) => double.IsNaN(value) ? "n/a" : value.ToString("0.00", Invariant) + " %";
}
