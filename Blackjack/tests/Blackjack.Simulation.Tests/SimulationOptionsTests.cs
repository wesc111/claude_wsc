namespace Blackjack.Simulation.Tests;

public sealed class SimulationOptionsTests
{
    [Fact]
    public void Defaults()
    {
        Assert.True(SimulationOptions.TryParse(["--strategy", "basic"], out var o, out _));

        Assert.Equal("basic", o.Strategy);
        Assert.Equal(1_000_000, o.Rounds);
        Assert.Null(o.Seed);
        Assert.Equal(10m, o.Bet);
        Assert.Equal(Environment.ProcessorCount, o.Workers);
        Assert.Null(o.CsvPath);
        Assert.Equal(6, o.Decks);
        Assert.False(o.DealerHitsSoft17);
        Assert.Equal(1.5m, o.Payout);
        Assert.Equal(0.75m, o.Penetration);
    }

    [Fact]
    public void AllArguments()
    {
        Assert.True(SimulationOptions.TryParse(
            ["--strategy=mimic", "--rounds", "2,000,000", "--seed", "42", "--bet", "12,5", "--workers", "3",
             "--csv", "out.csv", "--decks", "2", "--h17", "--payout", "1.2", "--penetration", "0.6"],
            out var o, out _));

        Assert.Equal("mimic", o.Strategy);
        Assert.Equal(2_000_000, o.Rounds);
        Assert.Equal(42, o.Seed);
        Assert.Equal(12.5m, o.Bet);
        Assert.Equal(3, o.Workers);
        Assert.Equal("out.csv", o.CsvPath);
        Assert.Equal(2, o.Decks);
        Assert.True(o.DealerHitsSoft17);
        Assert.Equal(1.2m, o.Payout);
        Assert.Equal(0.6m, o.Penetration);
    }

    [Fact]
    public void RoundsAcceptUnderscores()
    {
        Assert.True(SimulationOptions.TryParse(["--strategy", "basic", "--rounds", "1_000"], out var o, out _));
        Assert.Equal(1000, o.Rounds);
    }

    [Fact]
    public void HelpNeedsNoStrategy()
    {
        Assert.True(SimulationOptions.TryParse(["--help"], out var o, out _));
        Assert.True(o.ShowHelp);
    }

    [Theory]
    [InlineData]
    [InlineData("--strategy", "random")]
    [InlineData("--strategy")]
    [InlineData("--strategy", "basic", "--rounds", "0")]
    [InlineData("--strategy", "basic", "--rounds", "abc")]
    [InlineData("--strategy", "basic", "--workers", "0")]
    [InlineData("--strategy", "basic", "--bet", "-5")]
    [InlineData("--strategy", "basic", "--decks", "9")]
    [InlineData("--strategy", "basic", "--payout", "0")]
    [InlineData("--strategy", "basic", "--penetration", "1.5")]
    [InlineData("--strategy", "basic", "--seed", "x")]
    [InlineData("--strategy", "basic", "--unknown")]
    public void InvalidArguments(params string[] args)
    {
        Assert.False(SimulationOptions.TryParse(args, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }
}
