namespace Blackjack.Gui.ViewModels;

/// <summary>
/// The human player of the GUI: the engine's requests wait on a <see cref="TaskCompletionSource{TResult}"/>
/// until a button completes them.
/// </summary>
public sealed class GuiPlayer : IPlayerStrategy
{
    private TaskCompletionSource<decimal?>? _bet;
    private TaskCompletionSource<PlayerAction>? _decision;
    private TaskCompletionSource<bool>? _insurance;

    /// <summary>Raised when the engine asks for a bet.</summary>
    public event EventHandler<BetContext>? BetRequested;

    /// <summary>Raised when the engine asks for an action.</summary>
    public event EventHandler<DecisionContext>? DecisionRequested;

    /// <summary>Raised when the engine offers insurance (spec E12).</summary>
    public event EventHandler<InsuranceContext>? InsuranceRequested;

    /// <summary>The pending insurance offer, or <c>null</c>.</summary>
    public InsuranceContext? PendingInsurance { get; private set; }

    /// <summary>The pending bet request, or <c>null</c>.</summary>
    public BetContext? PendingBet { get; private set; }

    /// <summary>The pending decision request, or <c>null</c>.</summary>
    public DecisionContext? PendingDecision { get; private set; }

    /// <inheritdoc />
    public Task<decimal?> GetBetAsync(BetContext context, CancellationToken ct = default)
    {
        // Continuations run synchronously: a button press drives the engine to its next request immediately.
        var tcs = new TaskCompletionSource<decimal?>();
        _bet = tcs;
        PendingBet = context;
        BetRequested?.Invoke(this, context);
        return WaitAsync(tcs, ct);
    }

    /// <inheritdoc />
    public Task<PlayerAction> DecideAsync(DecisionContext context, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<PlayerAction>();
        _decision = tcs;
        PendingDecision = context;
        DecisionRequested?.Invoke(this, context);
        return WaitAsync(tcs, ct);
    }

    /// <inheritdoc />
    public Task<bool> DecideInsuranceAsync(InsuranceContext context, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<bool>();
        _insurance = tcs;
        PendingInsurance = context;
        InsuranceRequested?.Invoke(this, context);
        return WaitAsync(tcs, ct);
    }

    /// <summary>Answers the insurance offer; returns <c>false</c> if no offer is pending.</summary>
    public bool AnswerInsurance(bool take)
    {
        if (_insurance is not { } tcs)
        {
            return false;
        }

        _insurance = null;
        PendingInsurance = null;
        tcs.TrySetResult(take);
        return true;
    }

    /// <summary>Places <paramref name="bet"/>; returns <c>false</c> if no bet is pending or the bet is invalid.</summary>
    public bool PlaceBet(decimal bet)
    {
        if (_bet is not { } tcs || PendingBet is not { } context || !context.IsValidBet(bet))
        {
            return false;
        }

        ClearBet();
        tcs.TrySetResult(bet);
        return true;
    }

    /// <summary>Leaves the table instead of betting; returns <c>false</c> if no bet is pending.</summary>
    public bool LeaveTable()
    {
        if (_bet is not { } tcs)
        {
            return false;
        }

        ClearBet();
        tcs.TrySetResult(null);
        return true;
    }

    /// <summary>Chooses <paramref name="action"/>; returns <c>false</c> if no decision is pending or the action is not allowed.</summary>
    public bool Choose(PlayerAction action)
    {
        if (_decision is not { } tcs || PendingDecision is not { } context || !context.AllowedActions.Contains(action))
        {
            return false;
        }

        _decision = null;
        PendingDecision = null;
        tcs.TrySetResult(action);
        return true;
    }

    /// <summary>Whether <paramref name="action"/> can be chosen now.</summary>
    public bool CanChoose(PlayerAction action) => PendingDecision?.AllowedActions.Contains(action) == true;

    private void ClearBet()
    {
        _bet = null;
        PendingBet = null;
    }

    private static async Task<T> WaitAsync<T>(TaskCompletionSource<T> tcs, CancellationToken ct)
    {
        using (ct.Register(() => tcs.TrySetCanceled(ct)))
        {
            return await tcs.Task;
        }
    }
}
