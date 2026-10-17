namespace Blackjack.Simulation.Players;

/// <summary>A cell of a basic strategy table (SPEC 7.6).</summary>
internal enum StrategyCode
{
    /// <summary><c>H</c>: Hit.</summary>
    Hit,

    /// <summary><c>S</c>: Stand.</summary>
    Stand,

    /// <summary><c>D</c>: Double if allowed, otherwise Hit.</summary>
    DoubleOrHit,

    /// <summary><c>Ds</c>: Double if allowed, otherwise Stand.</summary>
    DoubleOrStand,

    /// <summary><c>P</c>: Split (pair table only).</summary>
    Split,

    /// <summary><c>-</c>: do not split, use the total tables (pair table only).</summary>
    NoSplit,
}
