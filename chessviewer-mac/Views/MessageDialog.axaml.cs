using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ChessViewer.Views;

public partial class MessageDialog : Window
{
    public MessageDialog() => InitializeComponent();

    // Avalonia has no built-in MessageBox (unlike WPF), so this stands in for
    // MessageBox.Show — a small modal window for load/parse error reporting.
    public static Task Show(Window owner, string title, string message)
    {
        var dialog = new MessageDialog { Title = title };
        dialog.MessageText.Text = message;
        return dialog.ShowDialog(owner);
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e) => Close();
}
