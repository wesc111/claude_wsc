using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Blackjack.Reports;
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
            var table = new TableViewModel(
                rules, r => new Shoe(r), startingBankroll: 1000m, DealDelay,
                saveLog: log => SessionLogFiles.DisplayPath(SessionLogFiles.Save(log, SessionLogFiles.DefaultDirectory)));
            var window = new MainWindow { DataContext = table };
            window.Closing += (_, _) => table.Shutdown();
            desktop.MainWindow = window;
            table.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
