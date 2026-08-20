using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Services;

namespace Sys.UI.ViewModels;

// Kullanıcının kendi şifresini değiştirdiği ekran. Admin'in "şifre sıfırlama"
// işlevinden farkı: mevcut şifre doğrulanır ve yeni şifreyi kullanıcıdan başka
// kimse bilmez.
public partial class ChangePasswordViewModel : ViewModelBase, IEscapeHandler
{
    private readonly AuthService _authService;
    private readonly User _currentUser;

    // Bu ekran sol menüde bir bölüm değil, kullanıcı menüsünden açılan bir hesap
    // işlemi. Bu yüzden kendi "Geri" aksiyonu var — kullanıcıyı geldiği ana bölüme
    // döndürmesi için ShellViewModel bu olaya abone oluyor.
    public event Action? BackRequested;

    [RelayCommand]
    private void Back() => BackRequested?.Invoke();

    public bool CanHandleEscape => true;
    public void HandleEscape() => BackRequested?.Invoke();

    [ObservableProperty]
    public partial string CurrentPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPasswordRepeat { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SuccessMessage { get; set; } = string.Empty;

    // Alan bazlı doğrulama mesajları — diğer formlarla aynı desen.
    [ObservableProperty]
    public partial string CurrentPasswordError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPasswordError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RepeatError { get; set; } = string.Empty;

    // Çift gönderim koruması — uygulamadaki diğer formlarla aynı desen.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubmitButtonText))]
    public partial bool IsBusy { get; set; }

    public string SubmitButtonText => IsBusy ? "Kaydediliyor..." : "Şifreyi Değiştir";

    partial void OnCurrentPasswordChanged(string value) => CurrentPasswordError = string.Empty;
    partial void OnNewPasswordChanged(string value) => NewPasswordError = string.Empty;
    partial void OnNewPasswordRepeatChanged(string value) => RepeatError = string.Empty;

    public ChangePasswordViewModel() : this(null!, new User()) { } // yalnızca tasarımcı önizlemesi için

    public ChangePasswordViewModel(AuthService authService, User currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    [RelayCommand]
    private async Task Submit()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;
            CurrentPasswordError = string.Empty;
            NewPasswordError = string.Empty;
            RepeatError = string.Empty;

            if (string.IsNullOrWhiteSpace(CurrentPassword))
                CurrentPasswordError = "Mevcut şifrenizi girin.";

            if (string.IsNullOrWhiteSpace(NewPassword))
                NewPasswordError = "Yeni şifre zorunludur.";
            else if (NewPassword.Length < 6)
                NewPasswordError = "Yeni şifre en az 6 karakter olmalıdır.";

            if (string.IsNullOrWhiteSpace(NewPasswordRepeat))
                RepeatError = "Yeni şifreyi tekrar girin.";
            else if (NewPassword != NewPasswordRepeat)
                RepeatError = "Şifreler eşleşmiyor.";

            if (CurrentPasswordError.Length > 0 || NewPasswordError.Length > 0 || RepeatError.Length > 0)
            {
                ErrorMessage = "Lütfen işaretli alanları düzeltin.";
                return;
            }

            try
            {
                var result = await _authService.ChangeOwnPasswordAsync(_currentUser, CurrentPassword, NewPassword);

                if (!result.Success)
                {
                    // "Mevcut şifreniz hatalı" mesajı ilgili alanın altında gösterilir;
                    // diğer hatalar (kural ihlalleri) genel mesaj alanına düşer.
                    if (result.ErrorMessage is not null && result.ErrorMessage.Contains("Mevcut şifreniz"))
                        CurrentPasswordError = result.ErrorMessage;
                    else
                        ErrorMessage = result.ErrorMessage ?? "Şifre değiştirilemedi.";
                    return;
                }

                SuccessMessage = "Şifreniz güncellendi. Bir sonraki girişinizde yeni şifrenizi kullanın.";
                CurrentPassword = string.Empty;
                NewPassword = string.Empty;
                NewPasswordRepeat = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Şifre değiştirilirken bir hata oluştu: " + ex.Message;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
