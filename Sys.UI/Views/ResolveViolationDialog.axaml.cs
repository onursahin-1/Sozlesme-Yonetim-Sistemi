using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Sys.UI.Localization;

namespace Sys.UI.Views;

// İhlalin nasıl giderildiğini soran pencere. Gerekçe zorunlu olduğu için sonuç
// hiçbir zaman boş dönmez; kullanıcı vazgeçtiyse null döner.
public partial class ResolveViolationDialog : Window
{
    public ResolveViolationDialog()
    {
        InitializeComponent();

        // Pencerenin ÇERÇEVESİ de çevrilmeli: başlık, soru, ipucu ve iki düğme
        // XAML'de sabit metindi. İçerideki cümleler çevrilirken bunlar Türkçe
        // kalıyordu — aynı hatayı ConfirmDialog ve RejectRequestDialog'da da
        // yapmıştım; kod-arkasından atanan metinler görülüyor, XAML'dekiler değil.
        Title = Strings.T("Vio.ResolveTitle");
        QuestionText.Text = Strings.T("Vio.ResolveQuestion");
        NoteBox.PlaceholderText = Strings.T("Vio.ResolvePlaceholder");
        CancelButton.Content = Strings.T("Confirm.Cancel");
        ConfirmText.Text = Strings.T("Vio.ResolveConfirm");
    }

    public ResolveViolationDialog(string violationType, bool isLastOpen, string newStatusText) : this()
    {
        HeaderText.Text = Strings.T("Vio.ResolveAsk", violationType);

        // Kullanıcı sözleşmenin durumunun değişip değişmeyeceğini önceden bilmeli.
        HintText.Text = isLastOpen
            ? Strings.T("Vio.ResolveLastOpen", newStatusText)
            : Strings.T("Vio.ResolveMoreOpen");
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var note = NoteBox.Text?.Trim();

        // Gerekçe servis katmanında da zorunlu; burada engellemek kullanıcıyı bir
        // istisna mesajıyla karşılaştırmadan aynı kuralı uygulamak için.
        if (string.IsNullOrWhiteSpace(note))
        {
            ErrorText.Text = Strings.T("Vio.ResolutionRequired");
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
