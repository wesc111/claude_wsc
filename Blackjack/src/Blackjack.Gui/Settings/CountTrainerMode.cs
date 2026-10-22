namespace Blackjack.Gui.Settings;

/// <summary>How the table trains the Hi-Lo count (spec E13).</summary>
public enum CountTrainerMode
{
    /// <summary>No count.</summary>
    Off,

    /// <summary>The running count, true count, decks left and the count bet are shown; hints use the index plays.</summary>
    ShowCount,

    /// <summary>The count is hidden; every 5th round and at the end of the shoe the player is asked for the running count.</summary>
    Quiz,
}
