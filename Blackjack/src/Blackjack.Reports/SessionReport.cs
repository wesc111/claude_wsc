using System.Globalization;
using System.Text;

namespace Blackjack.Reports;

/// <summary>Writes a <see cref="SessionLog"/> as readable text and as CSV (SPEC 8a, E2). No file access here.</summary>
public static class SessionReport
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>The header of the rounds CSV.</summary>
    public const string RoundsCsvHeader =
        "Round,Time,Hand,Bet,PlayerCards,PlayerTotal,Doubled,FromSplit,DealerCards,DealerTotal,Outcome,Net,BankrollAfter";

    /// <summary>Writes the readable report: header, statistics and the list of rounds.</summary>
    public static void WriteText(TextWriter output, SessionLog log, DateTimeOffset endedAt)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(log);
        var s = log.Statistics;

        output.WriteLine("Blackjack – session statistics");
        output.WriteLine($"Played:   {log.StartedAt.ToString("yyyy-MM-dd HH:mm", Invariant)} – {endedAt.ToString("HH:mm", Invariant)} ({Duration(endedAt - log.StartedAt)})");
        output.WriteLine($"Rules:    {ReportText.Rules(log.Rules)}");
        output.WriteLine();

        foreach (var (name, value) in StatisticsLines(s))
        {
            output.WriteLine($"{name,-25}{value}");
        }

        output.WriteLine();
        output.WriteLine("Rounds");
        WriteTable(output, RoundRows(log), rightAligned: [0, 2, 6, 7]);
    }

    /// <summary>Writes the statistics as <c>Metric,Value</c> rows.</summary>
    public static void WriteStatisticsCsv(TextWriter output, SessionLog log, DateTimeOffset endedAt)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(log);
        var s = log.Statistics;
        (string Metric, string Value)[] rows =
        [
            ("Started", log.StartedAt.ToString("yyyy-MM-dd HH:mm:ss", Invariant)),
            ("Ended", endedAt.ToString("yyyy-MM-dd HH:mm:ss", Invariant)),
            ("Rules", ReportText.Rules(log.Rules)),
            ("StartingBankroll", Optional(s.StartingBankroll)),
            ("FinalBankroll", Optional(s.FinalBankroll)),
            ("HighestBankroll", Optional(s.HighestBankroll)),
            ("LowestBankroll", Optional(s.LowestBankroll)),
            ("Rounds", Number(s.Rounds)),
            ("Hands", Number(s.Hands)),
            ("Wins", Number(s.Wins)),
            ("Losses", Number(s.Losses)),
            ("Pushes", Number(s.Pushes)),
            ("Blackjacks", Number(s.Blackjacks)),
            ("Doubles", Number(s.Doubles)),
            ("DoublesWon", Number(s.DoublesWon)),
            ("Splits", Number(s.Splits)),
            ("PlayerBusts", Number(s.PlayerBusts)),
            ("DealerBusts", Number(s.DealerBusts)),
            ("InitialBets", ReportText.Money(s.InitialBets)),
            ("TotalWagered", ReportText.Money(s.TotalWagered)),
            ("Net", ReportText.Money(s.Net)),
            ("NetPercentOfInitialBets", s.NetPercentOfInitialBets is { } p ? p.ToString("0.##", Invariant) : ""),
            ("BiggestWin", Optional(s.BiggestWin)),
            ("BiggestWinRound", s.BiggestWinRound?.ToString(Invariant) ?? ""),
            ("BiggestLoss", Optional(s.BiggestLoss)),
            ("BiggestLossRound", s.BiggestLossRound?.ToString(Invariant) ?? ""),
        ];

        output.WriteLine("Metric,Value");
        foreach (var (metric, value) in rows)
        {
            output.WriteLine($"{metric},{Csv(value)}");
        }
    }

    /// <summary>Writes one row per hand, see <see cref="RoundsCsvHeader"/>.</summary>
    public static void WriteRoundsCsv(TextWriter output, SessionLog log)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(log);
        output.WriteLine(RoundsCsvHeader);
        foreach (var round in log.Rounds)
        {
            var r = round.Result;
            for (var i = 0; i < r.PlayerHands.Count; i++)
            {
                var h = r.PlayerHands[i];
                string[] fields =
                [
                    Number(round.Number),
                    round.Time.ToString("yyyy-MM-dd HH:mm:ss", Invariant),
                    Number(i + 1),
                    ReportText.Money(h.Bet),
                    ReportText.Cards(h.Cards),
                    Number(h.Total),
                    h.IsDoubled ? "yes" : "no",
                    h.IsFromSplit ? "yes" : "no",
                    ReportText.Cards(r.DealerCards),
                    Number(r.DealerTotal),
                    h.Outcome.ToString(),
                    ReportText.Money(h.Net),
                    Optional(r.Bankroll),
                ];
                output.WriteLine(string.Join(",", fields.Select(Csv)));
            }
        }
    }

    private static IEnumerable<(string Name, string Value)> StatisticsLines(SessionStatistics s)
    {
        yield return ("Bankroll", $"start {Bankroll(s.StartingBankroll)}, end {Bankroll(s.FinalBankroll)}, " +
                                  $"highest {Bankroll(s.HighestBankroll)}, lowest {Bankroll(s.LowestBankroll)}");
        yield return ("Rounds", $"{s.Rounds}   ({s.Hands} hands)");
        yield return ("Wins / losses / pushes",
            $"{s.Wins} / {s.Losses} / {s.Pushes}   ({ReportText.Percent(s.Wins, s.Hands)} / {ReportText.Percent(s.Losses, s.Hands)} / {ReportText.Percent(s.Pushes, s.Hands)} of hands)");
        yield return ("Blackjacks", $"{s.Blackjacks}   ({ReportText.Percent(s.Blackjacks, s.Rounds)} of rounds)");
        yield return ("Doubles", $"{s.Doubles}   (won {s.DoublesWon})");
        yield return ("Splits", $"{s.Splits}");
        yield return ("Player busts", $"{s.PlayerBusts}   ({ReportText.Percent(s.PlayerBusts, s.Hands)} of hands)");
        yield return ("Dealer busts", $"{s.DealerBusts}   ({ReportText.Percent(s.DealerBusts, s.Rounds)} of rounds)");
        yield return ("Initial bets", ReportText.Money(s.InitialBets));
        yield return ("Total wagered", $"{ReportText.Money(s.TotalWagered)}   (including Double and Split)");
        var percent = s.NetPercentOfInitialBets is { } p
            ? $"   ({(p > 0 ? "+" : "")}{p.ToString("0.0", Invariant)} % of initial bets)"
            : "";
        yield return ("Net result", ReportText.SignedMoney(s.Net) + percent);
        yield return ("Biggest win", s.BiggestWin is { } win ? $"{ReportText.SignedMoney(win)} (round {s.BiggestWinRound})" : "–");
        yield return ("Biggest loss", s.BiggestLoss is { } loss ? $"{ReportText.SignedMoney(loss)} (round {s.BiggestLossRound})" : "–");
    }

    private static List<string[]> RoundRows(SessionLog log)
    {
        var rows = new List<string[]> { new[] { "#", "Time", "Bet", "Player", "Dealer", "Result", "Net", "Bankroll" } };
        foreach (var round in log.Rounds)
        {
            var r = round.Result;
            var dealer = $"{ReportText.Cards(r.DealerCards)} ({ReportText.Total(r.DealerCards)})";
            for (var i = 0; i < r.PlayerHands.Count; i++)
            {
                var h = r.PlayerHands[i];
                var player = $"{ReportText.Cards(h.Cards)} ({ReportText.Total(h.Cards, h.IsFromSplit)})" + (h.IsDoubled ? " doubled" : "");
                if (r.PlayerHands.Count > 1)
                {
                    player = $"Hand {i + 1}: {player}";
                }

                var first = i == 0;
                var last = i == r.PlayerHands.Count - 1;
                rows.Add(
                [
                    first ? Number(round.Number) : "",
                    first ? round.Time.ToString("HH:mm:ss", Invariant) : "",
                    ReportText.Money(h.Bet),
                    player,
                    first ? dealer : "",
                    ReportText.Outcome(h.Outcome),
                    ReportText.SignedMoney(h.Net),
                    last ? Bankroll(r.Bankroll) : "",
                ]);
            }
        }

        return rows;
    }

    private static void WriteTable(TextWriter output, List<string[]> rows, int[] rightAligned)
    {
        var widths = Enumerable.Range(0, rows[0].Length).Select(c => rows.Max(r => r[c].Length)).ToArray();
        foreach (var row in rows)
        {
            var line = new StringBuilder();
            for (var c = 0; c < row.Length; c++)
            {
                if (c > 0)
                {
                    line.Append("  ");
                }

                line.Append(rightAligned.Contains(c) ? row[c].PadLeft(widths[c]) : row[c].PadRight(widths[c]));
            }

            output.WriteLine(line.ToString().TrimEnd());
        }
    }

    private static string Duration(TimeSpan duration) =>
        duration < TimeSpan.FromMinutes(1) ? "< 1 min" : $"{(int)duration.TotalMinutes} min";

    private static string Bankroll(decimal? bankroll) => bankroll is { } b ? ReportText.Money(b) : "unlimited";

    private static string Optional(decimal? amount) => amount is { } a ? ReportText.Money(a) : "";

    private static string Number(int value) => value.ToString(Invariant);

    private static string Csv(string value) =>
        value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal)
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
}
