using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Sys.UI.ViewModels;

namespace Sys.UI.Views;

public partial class UserManagementView : UserControl
{
    public UserManagementView()
    {
        InitializeComponent();
    }

    // Caps Lock durumu tuş bırakıldıktan SONRA okunuyor: KeyDown anında işletim
    // sistemi henüz yeni durumu yazmamış olabiliyor ve uyarı bir tuş geriden gelirdi.
    // (Şifre Değiştir ekranıyla aynı desen.)

    // --- Yeni kullanıcı formundaki geçici şifre alanı ---

    private void OnNewUserPasswordKeyUp(object? sender, KeyEventArgs e) => RefreshNewUserWarning();
    private void OnNewUserPasswordFocus(object? sender, RoutedEventArgs e) => RefreshNewUserWarning();

    private void OnNewUserPasswordLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is UserManagementViewModel vm) vm.ShowNewUserCapsWarning = false;
    }

    private void RefreshNewUserWarning()
    {
        if (DataContext is UserManagementViewModel vm)
            vm.ShowNewUserCapsWarning = KeyboardState.IsCapsLockOn();
    }

    // --- Satır içi şifre sıfırlama alanı ---
    //
    // Kutu DataTemplate içinde tekrar ettiği için x:Name ile tek bir uyarı öğesine
    // erişilemiyor; uyarı, kutunun bağlı olduğu satırın kendi durumundan besleniyor.

    private void OnResetPasswordKeyUp(object? sender, KeyEventArgs e) => RefreshRowWarning(sender, KeyboardState.IsCapsLockOn());
    private void OnResetPasswordFocus(object? sender, RoutedEventArgs e) => RefreshRowWarning(sender, KeyboardState.IsCapsLockOn());
    private void OnResetPasswordLostFocus(object? sender, RoutedEventArgs e) => RefreshRowWarning(sender, false);

    private static void RefreshRowWarning(object? sender, bool show)
    {
        if (sender is Control { DataContext: UserRowViewModel row })
            row.ShowCapsWarning = show;
    }
}
