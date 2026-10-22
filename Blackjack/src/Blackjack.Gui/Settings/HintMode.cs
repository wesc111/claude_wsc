namespace Blackjack.Gui.Settings;

/// <summary>How the table helps with basic strategy (spec E11).</summary>
public enum HintMode
{
    /// <summary>No hints.</summary>
    Off,

    /// <summary>The basic strategy action is shown before each decision.</summary>
    Show,

    /// <summary>A decision that differs from basic strategy is reported afterwards.</summary>
    Feedback,
}
