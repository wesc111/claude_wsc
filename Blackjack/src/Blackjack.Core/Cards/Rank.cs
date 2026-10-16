namespace Blackjack.Core;

/// <summary>The thirteen ranks of a standard deck. Numeric ranks have their face value as underlying value.</summary>
public enum Rank
{
    /// <summary>Two.</summary>
    Two = 2,

    /// <summary>Three.</summary>
    Three = 3,

    /// <summary>Four.</summary>
    Four = 4,

    /// <summary>Five.</summary>
    Five = 5,

    /// <summary>Six.</summary>
    Six = 6,

    /// <summary>Seven.</summary>
    Seven = 7,

    /// <summary>Eight.</summary>
    Eight = 8,

    /// <summary>Nine.</summary>
    Nine = 9,

    /// <summary>Ten.</summary>
    Ten = 10,

    /// <summary>Jack (counts 10).</summary>
    Jack = 11,

    /// <summary>Queen (counts 10).</summary>
    Queen = 12,

    /// <summary>King (counts 10).</summary>
    King = 13,

    /// <summary>Ace (counts 11 or 1).</summary>
    Ace = 14,
}
