using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Blackjack.Gui.ViewModels;
using Blackjack.Gui.Views;

namespace Blackjack.Gui;

/// <summary>The Avalonia application: creates the table and the main window.</summary>
public sealed partial class App : Application
{
    /// <summary>Pause after each dealt card, long enough for the deal animation.</summary>
    public static readonly TimeSpan DealDelay = TimeSpan.FromMilliseconds(350);

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var rules = Rules.Default;
            var table = new TableViewModel(rules, () => new Shoe(rules), startingBankroll: 1000m, DealDelay);
            var window = new MainWindow { DataContext = table };
            window.Closing += (_, _) => table.Shutdown();
            desktop.MainWindow = window;
            table.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
