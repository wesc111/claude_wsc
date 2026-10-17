namespace Blackjack.ConsoleApp.Tests;

public sealed class ConsoleOptionsTests
{
    [Fact]
    public void Defaults()
    {
        Assert.True(ConsoleOptions.TryParse([], out var options, out var error));

        Assert.Null(error);
        Assert.Equal(1000m, options.Bankroll);
        Assert.Equal(10m, options.MinBet);
        Assert.Equal(500m, options.MaxBet);
        Assert.Null(options.Seed);
        Assert.Equal(6, options.Decks);
        Assert.False(options.ShowHelp);
    }

    [Fact]
    public void AllArguments()
    {
        Assert.True(ConsoleOptions.TryParse(
            ["--bankroll", "250", "--min-bet=5", "--max-bet", "100,5", "--seed", "42", "--decks", "2"], out var options, out _));

        Assert.Equal(250m, options.Bankroll);
        Assert.Equal(5m, options.MinBet);
        Assert.Equal(100.5m, options.MaxBet);
        Assert.Equal(42, options.Seed);
        Assert.Equal(2, options.Decks);
        Assert.Equal(new Rules { Decks = 2, MinBet = 5m, MaxBet = 100.5m }, options.CreateRules());
    }

    [Fact]
    public void Help()
    {
        Assert.True(ConsoleOptions.TryParse(["--help"], out var options, out _));
        Assert.True(options.ShowHelp);
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("--bankroll")]
    [InlineData("--bankroll", "abc")]
    [InlineData("--bankroll", "0")]
    [InlineData("--min-bet", "-5")]
    [InlineData("--seed", "1.5")]
    [InlineData("--max-bet", "5")]
    [InlineData("--bankroll", "5")]
    [InlineData("--decks", "0")]
    [InlineData("--decks", "9")]
    [InlineData("--decks", "two")]
    [InlineData("--decks", "-1")]
    public void InvalidArguments(params string[] args)
    {
        Assert.False(ConsoleOptions.TryParse(args, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void MaxBetBelowMinBetNamesBothValues()
    {
        Assert.False(ConsoleOptions.TryParse(["--min-bet", "50", "--max-bet", "20"], out _, out var error));
        Assert.Contains("20", error, StringComparison.Ordinal);
        Assert.Contains("50", error, StringComparison.Ordinal);
    }
}
