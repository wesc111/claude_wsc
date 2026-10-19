using Avalonia.Controls;
using Avalonia.Interactivity;
using Blackjack.Gui.ViewModels;

namespace Blackjack.Gui.Views;

/// <summary>The settings dialog (spec E9); closes with a <see cref="SettingsChange"/>, or <c>null</c> on Cancel.</summary>
public sealed partial class SettingsWindow : Window
{
    /// <summary>Creates the dialog.</summary>
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void OnOk(object? sender, RoutedEventArgs e) => CloseWith(resetBankroll: false);

    private void OnResetBankroll(object? sender, RoutedEventArgs e) => CloseWith(resetBankroll: true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    private void CloseWith(bool resetBankroll)
    {
        if (DataContext is SettingsViewModel settings)
        {
            Close(new SettingsChange(settings.ToSettings(), resetBankroll));
        }
    }
}
