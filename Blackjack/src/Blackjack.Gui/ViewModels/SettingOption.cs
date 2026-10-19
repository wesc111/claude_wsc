namespace Blackjack.Gui.ViewModels;

/// <summary>One choice in a list of the settings dialog, shown by its text.</summary>
/// <param name="Value">The value of the setting.</param>
/// <param name="Text">The text shown, e.g. <c>3:2</c>.</param>
public sealed record SettingOption<T>(T Value, string Text)
{
    /// <inheritdoc />
    public override string ToString() => Text;
}
