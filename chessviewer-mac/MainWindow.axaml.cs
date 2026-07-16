using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls;
using ChessViewer.ViewModels;
using ChessViewer.Views;

namespace ChessViewer;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(this);

        // Tunnel phase mirrors WPF's PreviewKeyDown: intercept navigation keys before
        // a focused child control (e.g. the move ListBox) handles them itself.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        var vm = (MainViewModel)DataContext!;
        switch (e.Key)
        {
            case Key.Right:
                vm.NextMoveCommand.Execute(null);
                e.Handled = true;
                return;
            case Key.Left:
                vm.PrevMoveCommand.Execute(null);
                e.Handled = true;
                return;
            case Key.Home:
                vm.GoToStartCommand.Execute(null);
                e.Handled = true;
                return;
            case Key.End:
                vm.GoToEndCommand.Execute(null);
                e.Handled = true;
                return;
        }
    }

    private void AboutButton_Click(object? sender, RoutedEventArgs e) =>
        new AboutWindow().ShowDialog(this);
}
