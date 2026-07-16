using ChessViewer.ViewModels;
using ChessViewer.Views;
using System.Windows;
using System.Windows.Input;

namespace ChessViewer;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var vm = (MainViewModel)DataContext;
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
        base.OnPreviewKeyDown(e);
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e) =>
        new AboutWindow { Owner = this }.ShowDialog();
}
