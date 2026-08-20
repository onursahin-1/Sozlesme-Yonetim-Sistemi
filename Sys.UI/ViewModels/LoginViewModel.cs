using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly AuthService _authService;

    public event Action<User>? LoginSucceeded;

    [ObservableProperty]
    public partial string Username { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    // --- Şifremi unuttum ---
    // E-posta altyapısı olmadığı için otomatik sıfırlama bağlantısı gönderilemiyor.
    // Bunun yerine talep kayda geçiyor, Admin Kullanıcı Yönetimi ekranında görüp
    // şifreyi sıfırlıyor ve kullanıcıya iletiyor.

    [ObservableProperty]
    public partial bool ShowForgotPasswordPanel { get; set; }

    [ObservableProperty]
    public partial string ForgotUsername { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ForgotMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSubmittingReset { get; set; }

    [RelayCommand]
    private void ToggleForgotPassword()
    {
        ShowForgotPasswordPanel = !ShowForgotPasswordPanel;
        ForgotMessage = string.Empty;
        // Kullanıcı adını giriş kutusundan taşıyoruz; büyük ihtimalle aynı hesap.
        ForgotUsername = ShowForgotPasswordPanel ? Username : string.Empty;
    }

    [RelayCommand]
    private async Task SubmitPasswordResetRequest()
    {
        if (IsSubmittingReset) return;
        IsSubmittingReset = true;
        try
        {
            ForgotMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(ForgotUsername))
            {
                ForgotMessage = "Lütfen kullanıcı adınızı girin.";
                return;
            }

            try
            {
                await _authService.RequestPasswordResetAsync(ForgotUsername);
            }
            catch
            {
                // Hata olsa bile aşağıdaki tek tip mesaj gösterilir; kullanıcıya
                // sistemin iç durumu hakkında ipucu verilmez.
            }

            // Kullanıcı adı sistemde olsun ya da olmasın HER ZAMAN aynı mesaj gösterilir.
            // Aksi halde giriş ekranı, hangi kullanıcı adlarının var olduğunu deneyerek
            // öğrenmeye yarayan bir araca dönüşürdü.
            ForgotMessage = "Talebiniz alındı. Sistem yöneticiniz sizinle iletişime geçecek.";
            ForgotUsername = string.Empty;
        }
        finally
        {
            IsSubmittingReset = false;
        }
    }

    public LoginViewModel() : this(null!) { } // yalnızca tasarımcı önizlemesi için

    public LoginViewModel(AuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;
        var result = await _authService.LoginAsync(Username, Password);
        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage ?? "Giriş başarısız.";
            return;
        }

        LoginSucceeded?.Invoke(result.User!);
    }
}