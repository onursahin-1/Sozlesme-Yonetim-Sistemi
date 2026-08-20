using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Sys.UI.ViewModels;

namespace Sys.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Esc kısayolu pencere seviyesinde yakalanıyor. UserControl üzerinde
        // dinlemek yeterli değildi: olayın oraya ulaşması için içindeki bir öğenin
        // odakta olması gerekir, kullanıcı hiçbir yere tıklamadan Esc'e bastığında
        // bu sağlanmaz. Tunnel (önizleme) aşamasında dinliyoruz ki metin kutuları
        // olayı yutmadan önce bize gelsin.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        if (DataContext is not MainViewModel main) return;
        if (main.CurrentViewModel is not ShellViewModel shell) return;

        // Bildirim paneli açıksa Esc önce onu kapatır — en yakın "geri alınabilir"
        // durum odur.
        if (shell.IsNotificationPanelOpen)
        {
            shell.IsNotificationPanelOpen = false;
            e.Handled = true;
            return;
        }

        if (shell.CurrentPageContent is IEscapeHandler handler && handler.CanHandleEscape)
        {
            handler.HandleEscape();
            e.Handled = true;
        }
    }
}
