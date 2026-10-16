namespace Blackjack.Core;

/// <summary>
/// Table rules and bet limits. Each property is validated when it is set;
/// <c>MaxBet ≥ MinBet</c> is checked by the <see cref="BlackjackGame"/> constructor.
/// </summary>
public sealed record Rules
{
    /// <summary>The smallest supported number of decks.</summary>
    public const int MinDecks = 1;

    /// <summary>The largest supported number of decks.</summary>
    public const int MaxDecks = 8;

    private readonly int _decks = 6;
    private readonly decimal _penetration = 0.75m;
    private readonly decimal _blackjackPayout = 1.5m;
    private readonly decimal _minBet = 10m;
    private readonly decimal _maxBet = 500m;

    /// <summary>The default rules: 6 decks, 75 % penetration, S17, 3:2, peek, bets 10–500.</summary>
    public static Rules Default { get; } = new();

    /// <summary>Number of decks in the shoe (1–8). Default 6.</summary>
    public int Decks
    {
        get => _decks;
        init
        {
            ValidateDecks(value, nameof(Decks));
            _decks = value;
        }
    }

    /// <summary>Fraction of cards dealt before the shoe is reshuffled (greater than 0, at most 1). Default 0.75.</summary>
    public decimal Penetration
    {
        get => _penetration;
        init
        {
            ValidatePenetration(value, nameof(Penetration));
            _penetration = value;
        }
    }

    /// <summary><c>false</c> = dealer stands on soft 17 (S17, default); <c>true</c> = dealer hits soft 17 (H17).</summary>
    public bool DealerHitsSoft17 { get; init; }

    /// <summary>Net win per unit bet for a Blackjack: 1.5 = 3:2 (default), 1.2 = 6:5. Must be greater than 0.</summary>
    public decimal BlackjackPayout
    {
        get => _blackjackPayout;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(BlackjackPayout));
            _blackjackPayout = value;
        }
    }

    /// <summary>Whether the dealer peeks for Blackjack. Default <c>true</c>; <c>false</c> (no-peek) is not supported yet.</summary>
    public bool DealerPeeks { get; init; } = true;

    /// <summary>Minimum bet; must be greater than 0. Default 10.</summary>
    public decimal MinBet
    {
        get => _minBet;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(MinBet));
            _minBet = value;
        }
    }

    /// <summary>Maximum bet; must be at least <see cref="MinBet"/>. Default 500.</summary>
    public decimal MaxBet
    {
        get => _maxBet;
        init => _maxBet = value;
    }

    internal static void ValidateDecks(int decks, string paramName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(decks, MinDecks, paramName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(decks, MaxDecks, paramName);
    }

    internal static void ValidatePenetration(decimal penetration, string paramName)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(penetration, paramName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(penetration, 1m, paramName);
    }
}
