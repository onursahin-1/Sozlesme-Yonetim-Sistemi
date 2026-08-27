using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Sys.UI.Views;

// Talep reddi diyaloğunun sonucu. Gerekçe zorunlu olduğu için Note hiçbir zaman boş dönmez;
// kullanıcı vazgeçtiyse ShowAsync null döndürür.
public record RejectRequestResult(string Note, bool AllowResubmit);

public partial class RejectRequestDialog : Window
{
    // Talep, işlemi yapan kişinin kendisine mi ait?
    //
    // Aynı servis işlemi iki farklı insan davranışına karşılık geliyor: başkasının
    // talebini REDDETMEK ve kendi talebini GERİ ÇEKMEK. Ayrım yapılmadığında SYB
    // kendi talebini "reddediyor", kendine gerekçe yazıyor ve gerekçe kendisine
    // bildirim olarak dönüyordu. Kural aynı, anlatım farklı.
    private bool _isOwnRequest;

    public RejectRequestDialog()
    {
        InitializeComponent();
        AllowResubmitCheck.IsCheckedChanged += (_, _) => UpdateModeTexts();
        UpdateModeTexts();
    }

    public RejectRequestDialog(string contractTitle, bool isOwnRequest) : this()
    {
        _isOwnRequest = isOwnRequest;

        // Pencere başlığı da değişmeli: içerideki her yazı "geri çekme" derken
        // başlık çubuğunun "Talebi Reddet" demesi, kullanıcıya yanlış pencereyi
        // açtığını düşündürüyordu.
        Title = isOwnRequest ? "Talebi Geri Çek" : "Talebi Reddet";

        HeaderText.Text = isOwnRequest
            ? $"\"{contractTitle}\" talebiniz geri çekilecek."
            : $"\"{contractTitle}\" talebi reddedilecek.";

        NoteLabel.Text = isOwnRequest ? "Gerekçe" : "Red gerekçesi";
        NoteBox.Watermark = isOwnRequest
            ? "Talebi neden geri çektiğinizi yazın. İşlem geçmişine kaydedilir."
            : "Talebin neden reddedildiğini yazın. Bu metin talep sahibine bildirim olarak iletilir.";

        AllowResubmitCheck.Content = isOwnRequest
            ? "Düzeltip yeniden göndereceğim"
            : "Düzeltilip yeniden gönderilebilir";

        UpdateModeTexts();
    }

    // Onay butonunun yazısı ve alttaki açıklama, seçilen redde göre değişir; kullanıcı
    // "Reddet"e basmadan önce hangi sonucun doğacağını görmeli.
    private void UpdateModeTexts()
    {
        var allowResubmit = AllowResubmitCheck.IsChecked == true;

        if (_isOwnRequest)
        {
            ConfirmText.Text = allowResubmit ? "Geri Çek" : "Kapat";
            HintText.Text = allowResubmit
                ? "Talep düzenlenebilir durumda kalır; düzeltip yeniden gönderebilirsiniz."
                : "Talep nihai olarak kapanır ve yeniden gönderilemez.";
            return;
        }

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

    public static async Task<RejectRequestResult?> ShowAsync(Window owner, string contractTitle, bool isOwnRequest = false)
    {
        var dialog = new RejectRequestDialog(contractTitle, isOwnRequest);
        return await dialog.ShowDialog<RejectRequestResult?>(owner);
    }
}
