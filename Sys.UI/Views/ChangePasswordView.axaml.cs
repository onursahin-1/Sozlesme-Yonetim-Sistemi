using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Sys.UI.Views;

public partial class ChangePasswordView : UserControl
{
    public ChangePasswordView()
    {
        InitializeComponent();
    }

    // Caps Lock durumu tuş bırakıldıktan SONRA okunuyor: KeyDown anında işletim
    // sistemi henüz yeni durumu yazmamış olabiliyor ve uyarı bir tuş geriden gelirdi.
    private void OnPasswordKeyUp(object? sender, KeyEventArgs e) => RefreshCapsLockWarning();

    private void OnPasswordFocusChanged(object? sender, RoutedEventArgs e) => RefreshCapsLockWarning();

    // Odak şifre kutularından çıkınca uyarı gizlenir.
    private void OnPasswordLostFocus(object? sender, RoutedEventArgs e)
        => CapsLockWarning.IsVisible = false;

    private void RefreshCapsLockWarning()
        => CapsLockWarning.IsVisible = KeyboardState.IsCapsLockOn();
}
