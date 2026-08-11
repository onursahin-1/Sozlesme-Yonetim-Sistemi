using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Sys.UI.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public ConfirmDialog(string message, string confirmText = "Evet, Devam Et") : this()
    {
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(true);

    public static async Task<bool> ShowAsync(Window owner, string message, string confirmText = "Evet, Devam Et")
    {
        var dialog = new ConfirmDialog(message, confirmText);
        var result = await dialog.ShowDialog<bool?>(owner);
        return result == true;
    }
}