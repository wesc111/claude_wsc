using Blackjack.Gui.Settings;

namespace Blackjack.Gui.ViewModels;

/// <summary>The result of the settings dialog: the chosen settings and whether to reset the bankroll.</summary>
/// <param name="Settings">The chosen settings.</param>
/// <param name="ResetBankroll">Whether "Reset bankroll" was pressed.</param>
public sealed record SettingsChange(GameSettings Settings, bool ResetBankroll);
