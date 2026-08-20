namespace Sys.UI.ViewModels;

// Sayfalama yapan tüm ekranlar (sözleşme listesi, denetim kaydı, ...) sayfa boyutunu
// buradan alır. Böylece "sayfa başına kaç kayıt gösterilsin" kararı tek bir yerden
// değiştirilir ve ekranlar arasında tutarsızlık oluşmaz.
public static class PagingDefaults
{
    public const int PageSize = 10;
}