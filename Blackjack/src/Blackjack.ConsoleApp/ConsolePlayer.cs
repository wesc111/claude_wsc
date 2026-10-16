namespace Blackjack.ConsoleApp;

/// <summary>A human player who enters bets and actions on the console.</summary>
internal sealed class ConsolePlayer(TextReader input, TextWriter output) : IPlayerStrategy
{
    private decimal? _lastBet;

    /// <summary>True once the player has asked to leave (<c>q</c>) or the input has ended.</summary>
    public bool QuitRequested { get; private set; }

    /// <inheritdoc />
    public async Task<decimal?> GetBetAsync(BetContext context, CancellationToken ct = default)
    {
        if (QuitRequested)
        {
            return null;
        }

        var min = context.Rules.MinBet;
        var max = context.MaxAllowedBet;
        var defaultBet = Math.Clamp(_lastBet ?? min, min, max);
        var bankroll = context.Bankroll is { } b ? Money.Format(b) : "unlimited";

        while (true)
        {
            output.Write($"Bankroll: {bankroll}   Your bet ({Money.Format(min)}–{Money.Format(max)}, Enter = {Money.Format(defaultBet)}, q = quit): ");
            var line = await ReadLineAsync(ct);
            if (line is null)
            {
                return null;
            }

            if (line.Length == 0)
            {
                return Accept(defaultBet);
            }

            if (line.Equals("q", StringComparison.OrdinalIgnoreCase))
            {
                QuitRequested = true;
                return null;
            }

            if (!Money.TryParse(line, out var bet))
            {
                output.WriteLine("Please enter an amount, or q to quit.");
            }
            else if (!context.IsValidBet(bet))
            {
                output.WriteLine($"The bet must be between {Money.Format(min)} and {Money.Format(max)}.");
            }
            else
            {
                return Accept(bet);
            }
        }
    }

    /// <inheritdoc />
    public async Task<PlayerAction> DecideAsync(DecisionContext context, CancellationToken ct = default)
    {
        while (true)
        {
            output.Write("(h)it, (s)tand, (q)uit after this round: ");
            var line = await ReadLineAsync(ct);
            if (line is null)
            {
                // Input has ended: finish the hand safely and leave afterwards.
                return PlayerAction.Stand;
            }

            switch (line.ToLowerInvariant())
            {
                case "h" when context.AllowedActions.Contains(PlayerAction.Hit):
                    return PlayerAction.Hit;
                case "s" when context.AllowedActions.Contains(PlayerAction.Stand):
                    return PlayerAction.Stand;
                case "q":
                    QuitRequested = true;
                    output.WriteLine("You will leave the table after this round.");
                    break;
                default:
                    output.WriteLine("Please enter h, s or q.");
                    break;
            }
        }
    }

    private decimal Accept(decimal bet)
    {
        _lastBet = bet;
        output.WriteLine();
        return bet;
    }

    private async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        // Console.In.ReadLineAsync blocks the calling thread and cannot be interrupted. Reading on a
        // thread-pool thread lets cancellation (Ctrl+C) stop the wait; the abandoned read ends with the process.
        var line = await Task.Run(() => input.ReadLineAsync(CancellationToken.None).AsTask(), CancellationToken.None).WaitAsync(ct);
        if (line is null)
        {
            QuitRequested = true;
            output.WriteLine();
            return null;
        }

        return line.Trim();
    }
}
