using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Sys.UI.Localization;

namespace Sys.UI.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();

        // Pencere başlığı ve Vazgeç düğmesi XAML'de sabit metindi. Diyaloğun içi
        // çevrilirken başlık çubuğu Türkçe kalıyordu.
        Title = Strings.T("Confirm.Title");
        CancelButton.Content = Strings.T("Confirm.Cancel");
    }

    public ConfirmDialog(string message, string? confirmText = null) : this()
    {
        MessageText.Text = message;
        // Butonun içeriği artık Border+TextBlock olduğu için yazı doğrudan TextBlock'a
        // yazılır; Content'e atamak butonun renkli gövdesini ezerdi.
        ConfirmButtonText.Text = confirmText ?? Strings.T("Confirm.Default");
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(true);

    public static async Task<bool> ShowAsync(Window owner, string message, string? confirmText = null)
    {
        var dialog = new ConfirmDialog(message, confirmText);
        var result = await dialog.ShowDialog<bool?>(owner);
        return result == true;
    }
}