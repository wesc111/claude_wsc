namespace Blackjack.Gui.ViewModels;

/// <summary>What the table is waiting for.</summary>
public enum TablePhase
{
    /// <summary>Waiting for the bet (chips, Deal, Leave table).</summary>
    Betting,

    /// <summary>Cards are being dealt; no input.</summary>
    Dealing,

    /// <summary>The dealer shows an Ace: waiting for the answer to the insurance question (spec E12).</summary>
    Insurance,

    /// <summary>Waiting for the player's action on the active hand.</summary>
    Playing,

    /// <summary>The bankroll is below the minimum bet; waiting for New game.</summary>
    GameOver,

    /// <summary>The player left the table; waiting for New game.</summary>
    Left,
}
