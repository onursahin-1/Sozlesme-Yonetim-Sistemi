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

    // Zorunlu değişim tamamlandığında tetiklenir; kullanıcı bu noktada uygulamaya
    // alınır. BackRequested'dan ayrı bir olay çünkü zorunlu modda "geri" diye bir
    // seçenek yok — tek çıkış şifreyi değiştirmek.
    public event Action? ForcedChangeCompleted;

    // Zorunlu modda kullanıcının tek çıkışı şifreyi değiştirmek olmamalı: yanlış
    // hesaba girmiş olabilir ya da geçici şifreyi hatırlamıyor olabilir. Uygulamayı
    // kapatmak zorunda bırakmak yerine giriş ekranına dönüş bırakılıyor.
    public event Action? ForcedLogoutRequested;

    [RelayCommand]
    private void Back() => BackRequested?.Invoke();

    [RelayCommand]
    private void ForcedLogout() => ForcedLogoutRequested?.Invoke();

    // Yönetici şifreyi belirlediyse kullanıcı bu ekrandan çıkamaz: geri butonu ve
    // Esc kapalı, mevcut şifre alanı "yöneticinin verdiği geçici şifre" olarak
    // etiketleniyor.
    public bool IsForced { get; }
    public bool ShowBackButton => !IsForced;

    public bool CanHandleEscape => !IsForced;
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

    partial void OnNewPasswordChanged(string value)
    {
        NewPasswordError = string.Empty;
        RaiseRuleFlags();
    }

    partial void OnNewPasswordRepeatChanged(string value)
    {
        RepeatError = string.Empty;
        OnPropertyChanged(nameof(RuleRepeatOk));
    }

    private void RaiseRuleFlags()
    {
        OnPropertyChanged(nameof(RuleLengthOk));
        OnPropertyChanged(nameof(RuleLetterOk));
        OnPropertyChanged(nameof(RuleDigitOk));
        OnPropertyChanged(nameof(RuleRepeatOk));
    }

    public ChangePasswordViewModel() : this(null!, new User()) { } // yalnızca tasarımcı önizlemesi için

    public ChangePasswordViewModel(AuthService authService, User currentUser, bool isForced = false)
    {
        _authService = authService;
        _currentUser = currentUser;
        IsForced = isForced;
    }

    // Ekrandaki canlı kural listesi. Kurallar PasswordPolicy'den geliyor; sabit metin
    // yazılsaydı kural değiştiğinde ekran sessizce yanlış bilgi vermeye başlardı.
    public string MinLengthRule => $"En az {PasswordPolicy.MinLength} karakter";

    public bool RuleLengthOk => PasswordPolicy.HasMinLength(NewPassword);
    public bool RuleLetterOk => PasswordPolicy.HasLetter(NewPassword);
    public bool RuleDigitOk => PasswordPolicy.HasDigit(NewPassword);
    public bool RuleRepeatOk => NewPassword.Length > 0 && NewPassword == NewPasswordRepeat;

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

            // Kural metni PasswordPolicy'den; servisle aynı kaynağı kullanıyor ki
            // ekranda geçen bir şifre serviste reddedilmesin.
            if (PasswordPolicy.Validate(NewPassword) is { } policyError)
                NewPasswordError = policyError;

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

                CurrentPassword = string.Empty;
                NewPassword = string.Empty;
                NewPasswordRepeat = string.Empty;

                if (IsForced)
                {
                    // Zorunlu modda ekran kapanıp uygulama açılıyor; "başarılı" mesajını
                    // kullanıcının okuyacağı bir an yok, doğrudan içeri alınıyor.
                    ForcedChangeCompleted?.Invoke();
                    return;
                }

                SuccessMessage = "Şifreniz güncellendi. Bir sonraki girişinizde yeni şifrenizi kullanın.";
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
