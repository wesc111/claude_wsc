namespace Blackjack.ConsoleApp;

/// <summary>Command-line options of the console game.</summary>
internal sealed record ConsoleOptions
{
    /// <summary>The usage text shown for <c>--help</c> and after an invalid argument.</summary>
    public const string Usage =
        """
        Usage: blackjack [options]

          --bankroll <amount>   Starting bankroll (default 1000)
          --min-bet <amount>    Minimum bet (default 10)
          --max-bet <amount>    Maximum bet (default 500)
          --seed <number>       Seed for a reproducible shoe (default: random)
          --help                Show this help
        """;

    /// <summary>Starting bankroll.</summary>
    public decimal Bankroll { get; init; } = 1000m;

    /// <summary>Minimum bet.</summary>
    public decimal MinBet { get; init; } = 10m;

    /// <summary>Maximum bet.</summary>
    public decimal MaxBet { get; init; } = 500m;

    /// <summary>Seed for the shoe, or <c>null</c> for a random one.</summary>
    public int? Seed { get; init; }

    /// <summary>True if <c>--help</c> was given.</summary>
    public bool ShowHelp { get; init; }

    /// <summary>Parses the arguments. Accepts <c>--name value</c> and <c>--name=value</c>.</summary>
    /// <returns><c>true</c> on success; otherwise <paramref name="error"/> describes the problem.</returns>
    public static bool TryParse(IReadOnlyList<string> args, out ConsoleOptions options, out string? error)
    {
        options = new ConsoleOptions();
        error = null;

        for (var i = 0; i < args.Count; i++)
        {
            var (name, value) = Split(args[i]);
            if (name == "--help")
            {
                options = options with { ShowHelp = true };
                continue;
            }

            if (name is not ("--bankroll" or "--min-bet" or "--max-bet" or "--seed"))
            {
                error = $"Unknown argument '{args[i]}'.";
                return false;
            }

            if (value is null)
            {
                if (i + 1 >= args.Count)
                {
                    error = $"Missing value for {name}.";
                    return false;
                }

                value = args[++i];
            }

            if (name == "--seed")
            {
                if (!int.TryParse(value, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var seed))
                {
                    error = $"Invalid value for --seed: '{value}'.";
                    return false;
                }

                options = options with { Seed = seed };
                continue;
            }

            if (!Money.TryParse(value, out var amount) || amount <= 0)
            {
                error = $"Invalid value for {name}: '{value}' (a positive amount is required).";
                return false;
            }

            options = name switch
            {
                "--bankroll" => options with { Bankroll = amount },
                "--min-bet" => options with { MinBet = amount },
                _ => options with { MaxBet = amount },
            };
        }

        if (options.MaxBet < options.MinBet)
        {
            error = $"The maximum bet ({Money.Format(options.MaxBet)}) must not be less than the minimum bet ({Money.Format(options.MinBet)}).";
            return false;
        }

        if (options.Bankroll < options.MinBet)
        {
            error = $"The bankroll ({Money.Format(options.Bankroll)}) must be at least the minimum bet ({Money.Format(options.MinBet)}).";
            return false;
        }

        return true;
    }

    private static (string Name, string? Value) Split(string arg)
    {
        var equals = arg.IndexOf('=', StringComparison.Ordinal);
        return equals < 0 ? (arg, null) : (arg[..equals], arg[(equals + 1)..]);
    }
}
