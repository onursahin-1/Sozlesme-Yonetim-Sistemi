using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Sys.UI.Views;

// Talep reddi diyaloğunun sonucu. Gerekçe zorunlu olduğu için Note hiçbir zaman boş dönmez;
// kullanıcı vazgeçtiyse ShowAsync null döndürür.
public record RejectRequestResult(string Note, bool AllowResubmit);

public partial class RejectRequestDialog : Window
{
    public RejectRequestDialog()
    {
        InitializeComponent();
        AllowResubmitCheck.IsCheckedChanged += (_, _) => UpdateModeTexts();
        UpdateModeTexts();
    }

    public RejectRequestDialog(string contractTitle) : this()
    {
        HeaderText.Text = $"\"{contractTitle}\" talebi reddedilecek.";
    }

    // Onay butonunun yazısı ve alttaki açıklama, seçilen redde göre değişir; kullanıcı
    // "Reddet"e basmadan önce hangi sonucun doğacağını görmeli.
    private void UpdateModeTexts()
    {
        var allowResubmit = AllowResubmitCheck.IsChecked == true;

        ConfirmText.Text = allowResubmit ? "İade Et" : "Reddet ve Kapat";
        HintText.Text = allowResubmit
            ? "Talep sahibine geri döner; düzeltip yeniden gönderebilir."
            : "Talep nihai olarak kapanır ve yeniden gönderilemez.";
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var note = NoteBox.Text?.Trim();

        // Gerekçe servis katmanında da zorunlu; burada engellemek kullanıcıyı bir
        // istisna mesajıyla karşılaştırmadan aynı kuralı uygulamak için.
        if (string.IsNullOrWhiteSpace(note))
        {
            ErrorText.Text = "Red gerekçesi zorunludur.";
            ErrorText.IsVisible = true;
            NoteBox.Focus();
            return;
        }

        Close(new RejectRequestResult(note, AllowResubmitCheck.IsChecked == true));
    }

    public static async Task<RejectRequestResult?> ShowAsync(Window owner, string contractTitle)
    {
        var dialog = new RejectRequestDialog(contractTitle);
        return await dialog.ShowDialog<RejectRequestResult?>(owner);
    }
}
