namespace Blackjack.Core.Tests.Helpers;

/// <summary>Bets a fixed amount and plays a predefined list of actions. Fails the test on an unexpected decision request.</summary>
public sealed class ScriptedPlayer : IPlayerStrategy
{
    private readonly decimal? _bet;
    private readonly Queue<PlayerAction> _actions;

    public ScriptedPlayer(decimal? bet, params PlayerAction[] actions)
    {
        _bet = bet;
        _actions = new Queue<PlayerAction>(actions);
    }

    public int BetRequests { get; private set; }

    public List<DecisionContext> Decisions { get; } = [];

    /// <summary>The answer to every insurance offer (SPEC E12).</summary>
    public bool TakesInsurance { get; init; }

    public List<InsuranceContext> InsuranceOffers { get; } = [];

    public Task<decimal?> GetBetAsync(BetContext context, CancellationToken ct = default)
    {
        BetRequests++;
        return Task.FromResult(_bet);
    }

    public Task<PlayerAction> DecideAsync(DecisionContext context, CancellationToken ct = default)
    {
        Decisions.Add(context);
        if (!_actions.TryDequeue(out var action))
        {
            Assert.Fail($"Unexpected decision request on {string.Join(" ", context.PlayerCards)}.");
        }

        return Task.FromResult(action);
    }

    public Task<bool> DecideInsuranceAsync(InsuranceContext context, CancellationToken ct = default)
    {
        InsuranceOffers.Add(context);
        return Task.FromResult(TakesInsurance);
    }
}
