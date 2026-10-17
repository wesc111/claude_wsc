using Blackjack.Simulation.Statistics;

namespace Blackjack.Simulation.Tests;

/// <summary>SPEC 6.4 – console report and CSV output.</summary>
public sealed class OutputTests
{
    private static SimulationResult Result()
    {
        var s = new SimulationStatistics();
        s.Record(Rounds.Create("10 9", "10 6 K", HandOutcome.DealerBust, 10m));
        s.Record(Rounds.Create("A K", "7 10", HandOutcome.BlackjackWin, 15m));
        s.Record(Rounds.Create("10 6 8", "9 7", HandOutcome.PlayerBust, -10m));
        s.Record(Rounds.Create("10 8", "10 8", HandOutcome.Push, 0m));
        return new SimulationResult(s, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ReportContainsAllMetrics()
    {
        var output = new StringWriter();
        ReportWriter.Write(output, new SimulationOptions { Strategy = "basic", Rounds = 4, Workers = 2 }, 42, Result());
        var text = output.ToString();

        Assert.Contains("Strategy: basic   Rounds: 4   Bet: 10   Seed: 42   Workers: 2", text, StringComparison.Ordinal);
        Assert.Contains("Rules: 6 decks, S17, 3:2, penetration 75 %", text, StringComparison.Ordinal);
        Assert.Contains("Time: 2.00 s (2 rounds/s)", text, StringComparison.Ordinal);
        Assert.Contains("Total wagered      40", text, StringComparison.Ordinal);
        Assert.Contains("Net result         15", text, StringComparison.Ordinal);
        Assert.Contains("House edge         -37.50 %", text, StringComparison.Ordinal);
        Assert.Contains("95 % CI", text, StringComparison.Ordinal);
        Assert.Contains("Win / loss / push  50.00 % / 25.00 % / 25.00 %", text, StringComparison.Ordinal);
        Assert.Contains("Blackjack rate     25.00 %", text, StringComparison.Ordinal);
        Assert.Contains("Player bust rate   25.00 %", text, StringComparison.Ordinal);
        Assert.Contains("Dealer bust rate   50.00 %   (of 2 rounds the dealer played out)", text, StringComparison.Ordinal);
        Assert.Contains("By dealer upcard", text, StringComparison.Ordinal);
        Assert.Contains("By player starting total", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportSkipsEmptyBreakdownRows()
    {
        var output = new StringWriter();
        ReportWriter.Write(output, new SimulationOptions { Strategy = "basic" }, 1, Result());
        var lines = output.ToString().Split(Environment.NewLine);

        Assert.Contains(lines, l => l.StartsWith("Blackjack ", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.StartsWith("hard 4 ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("1.5", "3:2")]
    [InlineData("1.2", "6:5")]
    [InlineData("1", "1:1")]
    [InlineData("2", "2:1")]
    [InlineData("1.3333", "1.3333:1")]
    public void PayoutDescription(string payout, string expected) =>
        Assert.Equal(expected, ReportWriter.DescribePayout(decimal.Parse(payout, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void RulesDescription() =>
        Assert.Equal("1 deck, H17, 6:5, penetration 50 %",
            ReportWriter.DescribeRules(new Rules { Decks = 1, DealerHitsSoft17 = true, BlackjackPayout = 1.2m, Penetration = 0.5m }));

    [Fact]
    public void CsvHasHeaderTotalAndAllBreakdownRows()
    {
        var output = new StringWriter { NewLine = "\n" };
        CsvWriter.Write(output, Result().Statistics);
        var lines = output.ToString().TrimEnd('\n').Split('\n');

        Assert.Equal(CsvWriter.Header, lines[0]);
        Assert.Equal(1 + 1 + Breakdown.UpcardKeys.Count + Breakdown.StartingTotalKeys.Count, lines.Length);
        Assert.Equal("Total,,4,40,40,15,-37.5,50,25,25,25,25,50,0,0", lines[1]);
        Assert.Contains("DealerUpcard,10,2,20,20,10,-50,50,0,50,0,0,50,0,0", lines);
        Assert.Contains("StartingTotal,Blackjack,1,10,10,15,-150,100,0,0,100,0,,0,0", lines);
        Assert.Contains("StartingTotal,hard 4,0,0,0,0,,,,,,,,,", lines);
        Assert.All(lines, l => Assert.Equal(15, l.Split(',').Length));
    }
}
