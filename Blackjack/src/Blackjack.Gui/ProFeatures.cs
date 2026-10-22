namespace Blackjack.Gui;

/// <summary>
/// The central switch of the Pro features (strategy hints since E11; later count trainer and statistics).
/// Until the license check exists, the Pro features are always unlocked.
/// </summary>
public static class ProFeatures
{
    /// <summary>Whether the Pro features are available.</summary>
    public static bool IsUnlocked => true;
}
