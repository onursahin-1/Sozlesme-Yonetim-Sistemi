using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Sys.UI.Localization;

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
        CancelButton.Content = Strings.T("Confirm.Cancel");
        AllowResubmitCheck.IsCheckedChanged += (_, _) => UpdateModeTexts();
        UpdateModeTexts();
    }

    public RejectRequestDialog(string contractTitle, bool isOwnRequest) : this()
    {
        _isOwnRequest = isOwnRequest;

        // Pencere başlığı da değişmeli: içerideki her yazı "geri çekme" derken
        // başlık çubuğunun "Talebi Reddet" demesi, kullanıcıya yanlış pencereyi
        // açtığını düşündürüyordu.
        Title = Strings.T(isOwnRequest ? "Card.WithdrawRequest" : "Card.RejectRequest");

        HeaderText.Text = Strings.T(isOwnRequest ? "Rjd.HeaderWithdraw" : "Rjd.HeaderReject", contractTitle);

        NoteLabel.Text = Strings.T(isOwnRequest ? "Rjd.Reason" : "Rjd.RejectReason");
        NoteBox.PlaceholderText = Strings.T(isOwnRequest ? "Rjd.WithdrawHint" : "Rjd.RejectHint");

        AllowResubmitCheck.Content = Strings.T(isOwnRequest ? "Rjd.ResubmitOwn" : "Rjd.ResubmitOther");

        UpdateModeTexts();
    }

    // Onay butonunun yazısı ve alttaki açıklama, seçilen redde göre değişir; kullanıcı
    // "Reddet"e basmadan önce hangi sonucun doğacağını görmeli.
    private void UpdateModeTexts()
    {
        var allowResubmit = AllowResubmitCheck.IsChecked == true;

        if (_isOwnRequest)
        {
            ConfirmText.Text = Strings.T(allowResubmit ? "Rjd.Withdraw" : "Rjd.Close");
            HintText.Text = Strings.T(allowResubmit ? "Rjd.HintStaysEditable" : "Rjd.HintFinal");
            return;
        }

        ConfirmText.Text = Strings.T(allowResubmit ? "Rjd.Return" : "Rjd.RejectAndClose");
        HintText.Text = Strings.T(allowResubmit ? "Rjd.HintReturns" : "Rjd.HintFinal");
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var note = NoteBox.Text?.Trim();

        // Gerekçe servis katmanında da zorunlu; burada engellemek kullanıcıyı bir
        // istisna mesajıyla karşılaştırmadan aynı kuralı uygulamak için.
        if (string.IsNullOrWhiteSpace(note))
        {
            ErrorText.Text = Strings.T("Rjd.ReasonRequired");
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
