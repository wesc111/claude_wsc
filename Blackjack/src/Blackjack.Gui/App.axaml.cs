using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Blackjack.Reports;
using Blackjack.Gui.Settings;
using Blackjack.Gui.Sounds;
using Blackjack.Gui.ViewModels;
using Blackjack.Gui.Views;

namespace Blackjack.Gui;

/// <summary>The Avalonia application: creates the table and the main window.</summary>
public sealed partial class App : Application
{
    /// <summary>Pause after a split; dealt cards wait for their flight from the shoe instead (spec E6).</summary>
    public static readonly TimeSpan DealDelay = TimeSpan.FromMilliseconds(350);

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var store = new SettingsStore(SettingsStore.DefaultPath);
            var settings = store.Load();
            var table = new TableViewModel(
                settings.ToRules(Rules.Default), r => new Shoe(r), settings.StartingBankroll, DealDelay,
                saveLog: log => SessionLogFiles.DisplayPath(SessionLogFiles.Save(log, SessionLogFiles.DefaultDirectory)),
                bankroll: settings.Bankroll, saveSettings: s => store.Save(s))
            {
                IsSoundOn = settings.IsSoundOn,
            };
            var window = new MainWindow { DataContext = table };
            table.CardAnimator = window;
            table.SoundPlayer = OperatingSystem.IsMacOS() ? new AfplaySoundPlayer() : null;
            window.Closing += (_, _) => table.Shutdown();
            desktop.MainWindow = window;

            // "Settings… ⌘," in the application menu (macOS); elsewhere the header button only.
            var settingsItem = new NativeMenuItem("Settings…") { Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Meta) };
            settingsItem.Click += async (_, _) => await window.OpenSettingsAsync();
            var menu = new NativeMenu();
            menu.Items.Add(settingsItem);
            NativeMenu.SetMenu(this, menu);

            table.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
