using Avalonia;

namespace Blackjack.Gui;

/// <summary>Entry point of the Blackjack GUI.</summary>
public static class Program
{
    /// <summary>Starts the application.</summary>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Configures Avalonia; also used by the visual designer.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
