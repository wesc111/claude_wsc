using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Blackjack.Gui.ViewModels;

namespace Blackjack.Gui.Views;

/// <summary>The main window showing the Blackjack table; it animates dealt cards from the shoe to the hands.</summary>
public sealed partial class MainWindow : Window, ICardAnimator
{
    /// <summary>Flight of a card from the shoe to its hand.</summary>
    public static readonly TimeSpan FlightDuration = TimeSpan.FromMilliseconds(400);

    /// <summary>Turning over the hole card in place.</summary>
    public static readonly TimeSpan TurnDuration = TimeSpan.FromMilliseconds(300);

    // A card leaves the shoe like the card shown in it (MainWindow.axaml: rotate(-45deg), table size).
    private const double StartScale = 1;
    private const double StartAngle = -45;

    // A card dealt face up turns over during the last part of the flight.
    private const double TurnStart = 0.55;

    private bool _settingsOpen;

    /// <summary>Creates the window.</summary>
    public MainWindow()
    {
        InitializeComponent();

        // Tunnel, so the shortcuts come before any control (spec E10).
        AddHandler(KeyDownEvent, OnShortcutKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnShortcutKeyDown(object? sender, KeyEventArgs e)
    {
        // An open deck list keeps its own keys.
        var comboBox = (e.Source as Visual)?.FindAncestorOfType<ComboBox>(includeSelf: true);
        if (comboBox?.IsDropDownOpen == true || DataContext is not TableViewModel table)
        {
            return;
        }

        e.Handled = KeyboardShortcuts.Handle(table, e.Key, e.KeyModifiers);
    }

    /// <summary>
    /// Shows the settings dialog (spec E9) and applies it on OK; only between rounds (the header button and the app
    /// menu item ⌘, both end here).
    /// </summary>
    public async Task OpenSettingsAsync()
    {
        if (_settingsOpen || DataContext is not TableViewModel table || !table.IsBetweenRounds)
        {
            return;
        }

        _settingsOpen = true;
        try
        {
            var dialog = new SettingsWindow
            {
                DataContext = new SettingsViewModel(table.CurrentSettings, table.Bankroll, table.CanChangeDecks),
            };
            if (await dialog.ShowDialog<SettingsChange?>(this) is { } change)
            {
                table.ApplySettings(change.Settings, change.ResetBankroll);
            }
        }
        finally
        {
            _settingsOpen = false;
        }
    }

    private async void OnSettingsClick(object? sender, RoutedEventArgs e) => await OpenSettingsAsync();

    /// <inheritdoc />
    public async Task DealAsync(CardViewModel card, CancellationToken ct)
    {
        UpdateLayout();
        var target = FindCard(card);
        if (target is null || target.TranslatePoint(default, FlightLayer) is not { } to
            || ShoeMouth.TranslatePoint(default, FlightLayer) is not { } mouth)
        {
            return;
        }

        // Starts face down; the face is shown when the card turns over.
        var flying = new CardViewModel(null);
        var view = new ContentControl
        {
            Content = flying,
            ContentTemplate = (IDataTemplate)Resources["CardTemplate"]!,
            Width = target.Bounds.Width,
            Height = target.Bounds.Height,
        };
        var scale = new ScaleTransform();
        var rotate = new RotateTransform();
        view.RenderTransform = new TransformGroup { Children = { scale, rotate } };

        // The centre of the card starts at the mouth of the shoe.
        var from = new Point(mouth.X - view.Width / 2, mouth.Y - view.Height / 2);
        FlightLayer.Children.Add(view);
        try
        {
            await AnimateAsync(FlightDuration, t =>
            {
                var e = EaseOut(t);
                Canvas.SetLeft(view, from.X + (to.X - from.X) * e);
                Canvas.SetTop(view, from.Y + (to.Y - from.Y) * e);
                var size = StartScale + (1 - StartScale) * e;
                rotate.Angle = StartAngle * (1 - e);
                scale.ScaleY = size;
                scale.ScaleX = size;
                if (card.Card is { } face)
                {
                    var turn = Math.Clamp((t - TurnStart) / (1 - TurnStart), 0, 1);
                    scale.ScaleX = size * Math.Abs(Math.Cos(Math.PI * turn));
                    if (turn >= 0.5)
                    {
                        flying.Card = face;
                    }
                }
            }, ct);
        }
        finally
        {
            FlightLayer.Children.Remove(view);
        }
    }

    /// <inheritdoc />
    public async Task TurnOverAsync(CardViewModel card, Action turn, CancellationToken ct)
    {
        var target = FindCard(card);
        if (target is null)
        {
            turn();
            return;
        }

        var scale = new ScaleTransform();
        target.RenderTransform = scale;
        var turned = false;
        try
        {
            await AnimateAsync(TurnDuration, t =>
            {
                scale.ScaleX = Math.Abs(Math.Cos(Math.PI * t));
                if (t >= 0.5 && !turned)
                {
                    turned = true;
                    turn();
                }
            }, ct);
        }
        finally
        {
            target.RenderTransform = null;
        }
    }

    private Border? FindCard(CardViewModel card) =>
        this.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Classes.Contains("card") && ReferenceEquals(b.DataContext, card) && b.FindAncestorOfType<Canvas>() != FlightLayer);

    private static double EaseOut(double t) => 1 - Math.Pow(1 - t, 3);

    /// <summary>Calls <paramref name="apply"/> once per frame with the progress 0…1, ending with 1.</summary>
    private async Task AnimateAsync(TimeSpan duration, Action<double> apply, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var t = Math.Min(1, watch.Elapsed / duration);
            apply(t);
            if (t >= 1)
            {
                return;
            }

            await NextFrameAsync(ct);
        }
    }

    // The next rendered frame; the timeout keeps the animation going while the window is not rendered (e.g. minimized).
    private async Task NextFrameAsync(CancellationToken ct)
    {
        var frame = new TaskCompletionSource();
        RequestAnimationFrame(_ => frame.TrySetResult());
        await Task.WhenAny(frame.Task, Task.Delay(50, ct));
    }
}
