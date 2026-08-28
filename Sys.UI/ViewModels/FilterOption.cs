namespace Sys.UI.ViewModels;

// Açılır liste filtrelerinin tek bir seçeneği.
//
// NEDEN AYRI BİR TİP?
// Filtre listeleri düz `string` idi ve "filtre yok" seçeneği ekranda görünen
// metnin ta kendisiydi:
//
//     private const string AllTypes = "Tüm türler";
//     ...
//     TypeFilter => SelectedType == AllTypes ? null : SelectedType;
//
// Yani aynı dizge iki iş görüyordu: KULLANICIYA GÖSTERİLEN etiket ve
// "daraltma yok" İŞARETİ. Metin çevrilebilir olduğu an bu ikisi ayrılmak
// zorunda — aksi halde dil değiştiğinde işaret de değişir ve karşılaştırma
// sessizce tutmaz olur.
//
// Şu an bu, kabuk dil değişiminde açık sayfayı baştan kurduğu için patlamıyordu:
// liste ve seçili değer aynı anda yeni dilde doğuyordu. Ama bu, görünmez bir
// bağımlılıktı — sayfa bir gün önbelleğe alınsa filtre çalışmaz hale gelirdi ve
// sebebini bulmak zor olurdu. Değer artık etiketten bağımsız.
//
// Value null ise "hepsi" demektir; sorguya hiçbir daraltma gitmez.
// SINIF DEĞİL RECORD: değer eşitliği ŞART.
//
// ComboBox, SelectedItem'ı listedeki öğelerle karşılaştırarak hangisinin seçili
// görüneceğine karar veriyor. Sınıf olduğunda karşılaştırma REFERANS üzerinden
// yapılıyordu; "hepsi" seçeneğini iki ayrı yerde new'leyen kod (bir kez listeyi
// kurarken, bir kez varsayılan seçimi atarken) birbirine eşit olmayan iki nesne
// üretiyor ve kutu BOŞ görünüyordu.
//
// Bu, filtre değerlerini etiketten ayırırken düzelttiğimiz hatanın aynısı: iki
// yerde üretilen "aynı" şeyin aslında aynı olmaması. Değer eşitliği, nerede
// üretildiğinden bağımsız olarak eşleşmeyi garanti ediyor.
public sealed record FilterOption(string? Value, string Label)
{
    public bool IsAll => Value is null;

    // ComboBox şablonsuz öğelerde ToString() kullanıyor; ayrıca bir
    // DisplayMemberBinding tanımlamaya gerek kalmıyor.
    public override string ToString() => Label;
}
