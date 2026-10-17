namespace Blackjack.Reports.Tests;

public sealed class SessionLogTests
{
    [Fact]
    public async Task RecordsRoundsWithNumberAndTime()
    {
        var log = await Sessions.TwoRoundsAsync();

        Assert.Equal(Sessions.Start, log.StartedAt);
        Assert.Equal([1, 2], log.Rounds.Select(r => r.Number));
        Assert.Equal([Sessions.Start.AddSeconds(65), Sessions.Start.AddSeconds(190)], log.Rounds.Select(r => r.Time));
        Assert.Equal(2, log.Statistics.Rounds);
        Assert.Equal(1010m, log.Statistics.FinalBankroll);
        Assert.Equal(Sessions.End, log.Now);
    }

    [Fact]
    public void NewLogIsEmpty()
    {
        var log = new SessionLog(Rules.Default, 1000m);

        Assert.Empty(log.Rounds);
        Assert.Equal(0, log.Statistics.Rounds);
        Assert.Equal(1000m, log.Statistics.StartingBankroll);
    }
}
