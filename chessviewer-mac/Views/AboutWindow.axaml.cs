using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ChessViewer.Views;

public partial class AboutWindow : Window
{
    public AboutWindow() => InitializeComponent();

    private void OkButton_Click(object? sender, RoutedEventArgs e) => Close();
}
