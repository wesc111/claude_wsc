using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Blackjack.Gui.Views;

/// <summary>The counting help (spec E14): how to keep the Hi-Lo count, bet and play with it; not modal.</summary>
public sealed partial class CountingHelpWindow : Window
{
    /// <summary>Creates the window.</summary>
    public CountingHelpWindow()
    {
        InitializeComponent();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
