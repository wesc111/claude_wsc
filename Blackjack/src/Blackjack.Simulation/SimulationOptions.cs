using System.Globalization;
using Blackjack.Simulation.Players;

namespace Blackjack.Simulation;

/// <summary>Command-line options of the simulator.</summary>
internal sealed record SimulationOptions
{
    /// <summary>The usage text shown for <c>--help</c> and after an invalid argument.</summary>
    public const string Usage =
        """
        Usage: blackjack-sim --strategy <basic|basic-hs|mimic|neverbust> [options]

          --strategy <name>       basic, basic-hs, mimic or neverbust (required)
          --rounds <number>       Number of rounds (default 1,000,000)
          --seed <number>         Seed for reproducible results (default: random, printed)
          --bet <amount>          Flat bet per round (default 10)
          --workers <number>      Parallel workers (default: number of CPU cores)
          --csv <file>            Also write the statistics as CSV
          --decks <1-8>           Number of decks (default 6)
          --h17                   Dealer hits soft 17 (default: stands, S17)
          --payout <ratio>        Blackjack payout, e.g. 1.5 = 3:2, 1.2 = 6:5 (default 1.5)
          --penetration <0-1>     Fraction dealt before reshuffling (default 0.75)
          --help                  Show this help
        """;

    /// <summary>Strategy name, see <see cref="PlayerFactory.Names"/>.</summary>
    public string Strategy { get; init; } = "";

    /// <summary>Number of rounds.</summary>
    public long Rounds { get; init; } = 1_000_000;

    /// <summary>Seed, or <c>null</c> for a random one.</summary>
    public int? Seed { get; init; }

    /// <summary>Flat bet.</summary>
    public decimal Bet { get; init; } = 10m;

    /// <summary>Number of parallel workers.</summary>
    public int Workers { get; init; } = Environment.ProcessorCount;

    /// <summary>CSV output file, or <c>null</c>.</summary>
    public string? CsvPath { get; init; }

    /// <summary>Number of decks.</summary>
    public int Decks { get; init; } = Rules.Default.Decks;

    /// <summary>Dealer hits soft 17.</summary>
    public bool DealerHitsSoft17 { get; init; }

    /// <summary>Blackjack payout.</summary>
    public decimal Payout { get; init; } = Rules.Default.BlackjackPayout;

    /// <summary>Penetration.</summary>
    public decimal Penetration { get; init; } = Rules.Default.Penetration;

    /// <summary>True if <c>--help</c> was given.</summary>
    public bool ShowHelp { get; init; }

    /// <summary>The table rules for these options; the bet limits equal the flat bet.</summary>
    public Rules CreateRules() => new()
    {
        Decks = Decks,
        Penetration = Penetration,
        DealerHitsSoft17 = DealerHitsSoft17,
        BlackjackPayout = Payout,
        MinBet = Bet,
        MaxBet = Bet,
    };

    /// <summary>Parses the arguments. Accepts <c>--name value</c> and <c>--name=value</c>.</summary>
    /// <returns><c>true</c> on success; otherwise <paramref name="error"/> describes the problem.</returns>
    public static bool TryParse(IReadOnlyList<string> args, out SimulationOptions options, out string? error)
    {
        options = new SimulationOptions();
        error = null;

        for (var i = 0; i < args.Count; i++)
        {
            var (name, inlineValue) = Split(args[i]);
            if (name is "--help" or "--h17")
            {
                options = name == "--help" ? options with { ShowHelp = true } : options with { DealerHitsSoft17 = true };
                continue;
            }

            if (name is not ("--strategy" or "--rounds" or "--seed" or "--bet" or "--workers" or "--csv" or "--decks" or "--payout" or "--penetration"))
            {
                error = $"Unknown argument '{args[i]}'.";
                return false;
            }

            var value = inlineValue;
            if (value is null)
            {
                if (i + 1 >= args.Count)
                {
                    error = $"Missing value for {name}.";
                    return false;
                }

                value = args[++i];
            }

            var parsed = name switch
            {
                "--strategy" => SetIf(PlayerFactory.Names.Contains(value), ref options, options with { Strategy = value }),
                "--csv" => SetIf(value.Length > 0, ref options, options with { CsvPath = value }),
                "--rounds" => TryLong(value, out var rounds) && rounds > 0 && SetIf(true, ref options, options with { Rounds = rounds }),
                "--seed" => TryInt(value, out var seed) && SetIf(true, ref options, options with { Seed = seed }),
                "--workers" => TryInt(value, out var workers) && workers > 0 && SetIf(true, ref options, options with { Workers = workers }),
                "--decks" => TryInt(value, out var decks) && decks is >= Rules.MinDecks and <= Rules.MaxDecks
                             && SetIf(true, ref options, options with { Decks = decks }),
                "--bet" => TryDecimal(value, out var bet) && bet > 0 && SetIf(true, ref options, options with { Bet = bet }),
                "--payout" => TryDecimal(value, out var payout) && payout > 0 && SetIf(true, ref options, options with { Payout = payout }),
                _ => TryDecimal(value, out var penetration) && penetration > 0 && penetration <= 1
                     && SetIf(true, ref options, options with { Penetration = penetration }),
            };

            if (!parsed)
            {
                error = $"Invalid value for {name}: '{value}'.";
                return false;
            }
        }

        if (!options.ShowHelp && options.Strategy.Length == 0)
        {
            error = $"--strategy is required ({string.Join(", ", PlayerFactory.Names)}).";
            return false;
        }

        return true;
    }

    private static bool SetIf(bool condition, ref SimulationOptions options, SimulationOptions updated)
    {
        if (condition)
        {
            options = updated;
        }

        return condition;
    }

    private static bool TryInt(string text, out int value) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

    private static bool TryLong(string text, out long value) =>
        long.TryParse(text.Replace("_", "", StringComparison.Ordinal), NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);

    private static bool TryDecimal(string text, out decimal value) =>
        decimal.TryParse(text.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);

    private static (string Name, string? Value) Split(string arg)
    {
        var equals = arg.IndexOf('=', StringComparison.Ordinal);
        return equals < 0 ? (arg, null) : (arg[..equals], arg[(equals + 1)..]);
    }
}
