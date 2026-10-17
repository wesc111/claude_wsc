namespace Blackjack.Reports.Tests;

/// <summary>Builds session logs from real rounds with a controllable clock.</summary>
public static class Sessions
{
    public static readonly DateTimeOffset Start = new(2026, 10, 17, 15, 30, 0, TimeSpan.FromHours(2));

    /// <summary>Round 1: push 17 vs 17 at 15:31:05. Round 2: 8 8 split, hand 1 doubles to 21, hand 2 loses, at 15:33:10. Ends 15:35:00.</summary>
    public static async Task<SessionLog> TwoRoundsAsync()
    {
        var time = Start;
        var log = new SessionLog(Rules.Default, 1000m, () => time);
        var game = new BlackjackGame(Rules.Default, new FixedCardSource("10 9 7 8  8 10 8 7 3 10 2"), 1000m);
        time = time.AddSeconds(65);
        log.Record((await game.PlayRoundAsync(new Player(10m, PlayerAction.Stand), TestContext.Current.CancellationToken))!);
        time = time.AddSeconds(125);
        log.Record((await game.PlayRoundAsync(new Player(10m, PlayerAction.Split, PlayerAction.Double, PlayerAction.Stand), TestContext.Current.CancellationToken))!);
        time = time.AddSeconds(110);
        return log;
    }

    public static DateTimeOffset End => Start.AddMinutes(5);

    private sealed class Player(decimal bet, params PlayerAction[] actions) : IPlayerStrategy
    {
        private readonly Queue<PlayerAction> _actions = new(actions);

        public Task<decimal?> GetBetAsync(BetContext context, CancellationToken ct = default) => Task.FromResult<decimal?>(bet);

        public Task<PlayerAction> DecideAsync(DecisionContext context, CancellationToken ct = default) => Task.FromResult(_actions.Dequeue());
    }
}
