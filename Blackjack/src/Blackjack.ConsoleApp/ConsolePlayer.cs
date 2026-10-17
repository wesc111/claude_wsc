namespace Blackjack.ConsoleApp;

/// <summary>A human player who enters bets and actions on the console.</summary>
/// <param name="input">Where bets and actions are read from.</param>
/// <param name="output">Where prompts are written to.</param>
/// <param name="shoe">If given, the cards left in it are shown before each bet prompt.</param>
internal sealed class ConsolePlayer(TextReader input, TextWriter output, ICardSource? shoe = null) : IPlayerStrategy
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

        // Shown here because the engine reshuffles before asking for the bet.
        if (shoe is not null)
        {
            output.WriteLine($"Shoe: {shoe.CardsRemaining} of {shoe.TotalCards} cards left.");
        }

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

    // Commands in prompt order: key, prompt text, action.
    private static readonly (string Key, string Label, PlayerAction Action)[] Commands =
    [
        ("h", "(h)it", PlayerAction.Hit),
        ("s", "(s)tand", PlayerAction.Stand),
        ("d", "(d)ouble", PlayerAction.Double),
        ("p", "s(p)lit", PlayerAction.Split),
    ];

    /// <inheritdoc />
    public async Task<PlayerAction> DecideAsync(DecisionContext context, CancellationToken ct = default)
    {
        var allowed = Commands.Where(c => context.AllowedActions.Contains(c.Action)).ToArray();
        var prefix = context.HandCount > 1 ? $"Hand {context.HandIndex + 1} – " : "";
        var prompt = $"{prefix}{string.Join(", ", allowed.Select(c => c.Label))}, (q)uit after this round: ";
        var keys = string.Join(", ", allowed.Select(c => c.Key)) + " or q";

        while (true)
        {
            output.Write(prompt);
            var line = await ReadLineAsync(ct);
            if (line is null)
            {
                // Input has ended: finish the hand safely and leave afterwards.
                return context.AllowedActions.Contains(PlayerAction.Stand) ? PlayerAction.Stand : allowed[0].Action;
            }

            var key = line.ToLowerInvariant();
            if (key == "q")
            {
                QuitRequested = true;
                output.WriteLine("You will leave the table after this round.");
                continue;
            }

            var command = Array.Find(Commands, c => c.Key == key);
            if (command.Key is null)
            {
                output.WriteLine($"Please enter {keys}.");
            }
            else if (!context.AllowedActions.Contains(command.Action))
            {
                output.WriteLine($"{command.Action} is not possible now. Please enter {keys}.");
            }
            else
            {
                return command.Action;
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
