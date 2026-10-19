using System.Windows.Input;
using Avalonia.Input;
using Blackjack.Gui.ViewModels;

namespace Blackjack.Gui.Views;

/// <summary>
/// The keyboard shortcuts of the table (spec E10). A key runs the same command as its button, under the same
/// conditions; Leave table has no shortcut, so the shoe is never lost by accident.
/// </summary>
public static class KeyboardShortcuts
{
    /// <summary>The command and its parameter for a key, or <c>(null, null)</c> if the key is no shortcut.</summary>
    public static (ICommand? Command, object? Parameter) Find(TableViewModel table, Key key, KeyModifiers modifiers)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (ChipIndex(key) is { } index)
        {
            var value = ChipViewModel.Values[index];
            return modifiers switch
            {
                KeyModifiers.None => (table.AddChipCommand, value),
                KeyModifiers.Shift => (table.RemoveChipCommand, value),
                _ => (null, null),
            };
        }

        if (modifiers != KeyModifiers.None)
        {
            return (null, null);
        }

        ICommand? command = key switch
        {
            Key.H => table.HitCommand,
            Key.S => table.StandCommand,
            Key.D => table.DoubleCommand,
            Key.P => table.SplitCommand,
            Key.Enter or Key.Space => table.DealCommand,
            Key.C => table.ClearBetCommand,
            Key.N => table.NewGameCommand,
            Key.M => table.ToggleSoundCommand,
            _ => null,
        };
        return (command, null);
    }

    /// <summary>Runs the command of a shortcut if it is enabled.</summary>
    /// <returns>Whether the key is a shortcut (also when its command is disabled now).</returns>
    public static bool Handle(TableViewModel table, Key key, KeyModifiers modifiers)
    {
        var (command, parameter) = Find(table, key, modifiers);
        if (command is null)
        {
            return false;
        }

        if (command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }

        return true;
    }

    // Keys 1–4 (also on the number pad) stand for the chips of the tray, ascending.
    private static int? ChipIndex(Key key) => key switch
    {
        >= Key.D1 and <= Key.D4 => key - Key.D1,
        >= Key.NumPad1 and <= Key.NumPad4 => key - Key.NumPad1,
        _ => null,
    };
}
