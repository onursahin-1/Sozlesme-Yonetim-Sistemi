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
    private void OnPasswordKeyUp(object? sender, KeyEventArgs e) => RefreshCapsLockWarning(sender);

    private void OnPasswordFocusChanged(object? sender, RoutedEventArgs e) => RefreshCapsLockWarning(sender);

    private void OnPasswordLostFocus(object? sender, RoutedEventArgs e) => HideAllWarnings();

    // Ekranda üç şifre alanı var; uyarı hepsinin üstünde tek bir yerde değil, o an
    // yazılan alanın kendi başlığının yanında gösteriliyor.
    private void RefreshCapsLockWarning(object? sender)
    {
        HideAllWarnings();

        if (!KeyboardState.IsCapsLockOn()) return;
        if (sender is not Control control) return;

        var target = control.Name switch
        {
            "CurrentPasswordBox" => CapsWarnCurrent,
            "NewPasswordBox" => CapsWarnNew,
            "RepeatPasswordBox" => CapsWarnRepeat,
            _ => null
        };

        if (target is not null) target.IsVisible = true;
    }

    private void HideAllWarnings()
    {
        CapsWarnCurrent.IsVisible = false;
        CapsWarnNew.IsVisible = false;
        CapsWarnRepeat.IsVisible = false;
    }
}
