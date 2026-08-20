using Avalonia.Controls;
using Avalonia.Input;
using Sys.UI.ViewModels;

namespace Sys.UI.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();

        // Giriş ekranı açılır açılmaz imleç kullanıcı adı kutusunda olsun; kullanıcı
        // fareye hiç dokunmadan yazmaya başlayıp Enter'a basabilsin.
        Loaded += (_, _) => UsernameBox.Focus();

        // Şifre kutusundan çıkıldığında uyarı da kaybolsun.
        PasswordBox.LostFocus += (_, _) => CapsLockWarning.IsVisible = false;
    }

    // Enter: kullanıcı adı veya şifre kutusundayken girişi tetikler.
    private void OnCredentialKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is not LoginViewModel vm) return;

        e.Handled = true;
        if (vm.LoginCommand.CanExecute(null))
            vm.LoginCommand.Execute(null);
    }

    // Caps Lock durumu tuş bırakıldıktan SONRA okunuyor: KeyDown anında işletim
    // sistemi henüz yeni durumu yazmamış olabiliyor ve uyarı bir tuş geriden gelirdi.
    private void OnPasswordKeyUp(object? sender, KeyEventArgs e) => RefreshCapsLockWarning();

    private void OnPasswordFocusChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => RefreshCapsLockWarning();

    private void RefreshCapsLockWarning()
        => CapsLockWarning.IsVisible = KeyboardState.IsCapsLockOn();

    // Enter: "şifremi unuttum" panelindeki kullanıcı adı kutusunda talebi gönderir.
    private void OnForgotKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is not LoginViewModel vm) return;

        e.Handled = true;
        if (vm.SubmitPasswordResetRequestCommand.CanExecute(null))
            vm.SubmitPasswordResetRequestCommand.Execute(null);
    }
}
