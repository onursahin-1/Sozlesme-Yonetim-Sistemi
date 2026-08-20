namespace Sys.UI.ViewModels;

// Esc tuşuna basıldığında "geri dön / iptal et" davranışı olan ekranlar bunu uygular.
//
// Tuş olayı pencere seviyesinde yakalanıyor (MainWindow), çünkü UserControl'ün olayı
// alabilmesi için içinde bir şeyin odakta olması gerekir — kullanıcı hiçbir yere
// tıklamadan Esc'e bastığında bu sağlanmaz. Pencere seviyesinde yakalayıp o anki
// sayfaya yönlendirmek her durumda çalışır.
public interface IEscapeHandler
{
    // Şu an geri dönülecek bir yer var mı? (Örn. menüden açılmış bir ekranda "Geri"
    // butonu görünmüyorsa Esc'in de bir şey yapmaması gerekir.)
    bool CanHandleEscape { get; }

    void HandleEscape();
}
