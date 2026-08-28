namespace Sys.UI.ViewModels;

// Kullanıcının doldurduğu ama henüz göndermediği veri var mı?
//
// NEDEN GEREKLİ?
// Tema veya dil değiştiğinde kabuk açık sayfayı yeniden kuruyor — bu, o ekrandaki
// ViewModel'in ürettiği metinlerin ve renklerin tazelenmesinin tek yolu.
// (Bir kez bunu yapmadan çözmeyi denedim: ekranı yerinde bırakıp XAML bağlamalarının
// kendiliğinden güncelleneceğini varsaydım. Güncellenmiyorlar — o ekranlar dil
// değiştiğinde hiç değişmedi. Yanlış varsayımdı.)
//
// Yeniden kurmanın bedeli: yarısı doldurulmuş bir formdaysanız girdikleriniz gider.
// Bu arayüz, o kaybın SESSİZ olmasını engelliyor — kullanıcıya önce soruluyor.
//
// Boş bir formda soru sorulmuyor: her dil değişiminde onay istemek, uyarıyı
// anlamsızlaştırır ve insanlar okumadan onaylamayı öğrenir.
public interface IHasUnsavedInput
{
    bool HasUnsavedInput { get; }
}
