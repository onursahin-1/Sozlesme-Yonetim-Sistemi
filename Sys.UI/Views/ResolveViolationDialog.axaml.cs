using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Sys.UI.Views;

// İhlalin nasıl giderildiğini soran pencere. Gerekçe zorunlu olduğu için sonuç
// hiçbir zaman boş dönmez; kullanıcı vazgeçtiyse null döner.
public partial class ResolveViolationDialog : Window
{
    public ResolveViolationDialog()
    {
        InitializeComponent();
    }

    public ResolveViolationDialog(string violationType, bool isLastOpen, string newStatusText) : this()
    {
        HeaderText.Text = $"\"{violationType}\" ihlali giderildi olarak işaretlenecek.";

        // Kullanıcı sözleşmenin durumunun değişip değişmeyeceğini önceden bilmeli.
        HintText.Text = isLastOpen
            ? $"Bu sözleşmenin son açık ihlali. İşaretlediğinizde sözleşme \"{newStatusText}\" durumuna dönecek."
            : "Sözleşmede başka açık ihlaller var; durumu \"İhlal Mevcut\" olarak kalmaya devam edecek.";
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var note = NoteBox.Text?.Trim();

        // Gerekçe servis katmanında da zorunlu; burada engellemek kullanıcıyı bir
        // istisna mesajıyla karşılaştırmadan aynı kuralı uygulamak için.
        if (string.IsNullOrWhiteSpace(note))
        {
            ErrorText.Text = "İhlalin nasıl giderildiği yazılmalıdır.";
            ErrorText.IsVisible = true;
            NoteBox.Focus();
            return;
        }

        Close(note);
    }

    public static async Task<string?> ShowAsync(Window owner, string violationType, bool isLastOpen, string newStatusText)
    {
        var dialog = new ResolveViolationDialog(violationType, isLastOpen, newStatusText);
        return await dialog.ShowDialog<string?>(owner);
    }
}
