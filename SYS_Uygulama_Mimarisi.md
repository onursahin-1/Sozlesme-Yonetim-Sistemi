# SYS — Uygulama Mimarisi

## Katmanlar

```
Sys.UI  ──▶  Sys.Services  ──▶  Sys.Domain
   │              ▲
   └──────────────┤
          Sys.Infrastructure
```

`Sys.Services` repository **arayüzlerini** tanımlar, `Sys.Infrastructure` bunları
EF Core ile gerçekler. Servis katmanı veritabanını hiç bilmez; testlerde sahte
repository ile çalışır.

| Proje | Sorumluluk |
|---|---|
| `Sys.Domain` | Entity ve enum'lar. Davranış yok, hesaplanmış birkaç özellik dışında (`LineTotal`, `IsResolved`). |
| `Sys.Services` | İş kuralları, yetki kontrolleri, durum geçişleri, bildirim üretimi. |
| `Sys.Infrastructure` | EF Core repository'leri, migration'lar, dosya kopyalama, bağlantı yönetimi. |
| `Sys.UI` | Avalonia View + ViewModel, PDF üretimi, yazdırma. |

## MVVM

CommunityToolkit.Mvvm kaynak üreteçleri kullanılır:

```csharp
[ObservableProperty]
public partial string Title { get; set; } = string.Empty;

[RelayCommand]
private async Task Submit() { ... }
```

**ViewModel kural işletmez.** Alan doğrulaması yapar (zorunlu alan, tarih aralığı,
sayı biçimi) ama *"bu kullanıcı bunu yapabilir mi"* ve *"sözleşme hangi duruma
geçer"* kararı servistedir. Aynı kural iki yerde ise, servistekiler bağlayıcıdır;
arayüzdeki kopya yalnızca kullanıcıyı istisna mesajıyla karşılaştırmamak içindir.

Pencere açan işlemler (dosya seçici, onay penceresi) kod-arkasından yürütülür;
`TopLevel.GetTopLevel(this) as Window` ile sahip pencere alınır.

## Tekrar Eden Desenler

**İstek sayacı (`_loadRequestId`).** Kullanıcı listede hızlıca birden fazla kayda
tıklarsa, eski bir sorgu yenisinden sonra dönüp ekranı ezebilir. Her seçimde
sayaç artırılır; sonuç geldiğinde hâlâ en son istek mi diye bakılır.

**Gecikmeli arama (debounce).** Arama kutusuna her harfte sorgu atılmaz; 350 ms
beklenir, bu sürede yeni harf gelirse önceki bekleme iptal edilir.

**`Classes="flat"` butonlar.** Fluent teması `:pointerover` durumunda butonun
`Background`/`Foreground` değerlerini eziyor; renkli butonlar fare üzerine gelince
kayboluyordu. Çözüm: butonun gövdesi tamamen şeffaf, renk ve yazı **içindeki**
`Border`/`TextBlock` üzerinde (yerel değer, tema ezemez), geri bildirim `Opacity`
ile. Tanım `App.axaml` içinde.

**`RequestedThemeVariant="Light"`.** Uygulamanın tüm renkleri açık tema
varsayımıyla yazıldı. "Default" bırakılınca sistem koyu moddayken tema
varsayılanı olan açık yazılar beyaz kartların üzerine düşüp okunamaz hale
geliyordu.

**Emoji kullanılmaz.** Emoji her Windows sürümünde farklı çiziliyor, hizası kayıyor
ve satır yüksekliğini bozuyor. İkonlar `Path` (vektör) veya renkli nokta olarak
çizilir.

**Sayfalama.** Sayfa boyutu `PagingDefaults.PageSize` (10) — sözleşme listesi,
arşiv, işlem geçmişi ve onay kuyruğu aynı değeri kullanır. Filtreleme, arama ve
sayfalama **veritabanı tarafında** yapılır; kullanıcı yönetimi bunun istisnasıdır
(liste küçük ve tamamı bellekte).

**Ekran düzeni.** Form ekranlarında solda form, sağda kaydırmayla kaybolmayan
sabit panel (özet + gönder butonu) durur. Liste ekranlarında `DockPanel`
kullanılır — `StackPanel` çocuklarına sınırsız yükseklik verdiği için içindeki
`ListBox` kendi kaydırma çubuğunu hiç açmaz.

## İş Kuralları

### Yetki

| İşlem | Rol |
|---|---|
| Talep oluştur / güncelle | Personel, SYB |
| Talebi reddet (iade / kapatma) | SYB |
| Sözleşme oluştur (talepten) | SYB |
| Son kontrol (Stage 1) | SYB |
| Yönetim onayı (Stage 2) | Müdür |
| Düzenleme / fesih talebi | SYB |
| İhlal bildir | Personel, SYB |
| İhlal gider | SYB |
| Ek sil | SYB |
| İşlem geçmişi | Müdür |
| Kullanıcı yönetimi | Admin |

Personel yalnızca **kendi** taleplerini görür; `GetContractDetailAsync` başkasının
kaydında `null` döner ve sahiplik kontrolü buradan gelir.

### Durum geçişleri

`EnsureContractIsLive` — düzenleme, fesih ve ihlal işlemleri yalnızca
**yürürlükteki** sözleşmede (Aktif / Uyarı / İhlal) yapılabilir. Ayrıca onay
bekleyen bir düzenleme/fesih varken ikincisi başlatılamaz: geri dönüş noktasını
tutan `PreviousStatusBefore…` alanları tek değer tuttuğu için ikinci istek
birincinin kaydını ezerdi.

Bu üç ekranın açılır listeleri de aynı kümeden (`LiveStatuses`) beslenir; ayrı
yazıldıklarında kümeler birbirinden sapmıştı.

`FinalizeContractAsync` yalnızca `Talep` durumundaki kaydı kabul eder — kapatılmış
bir talep ya da yürürlükteki bir sözleşme yeniden onay zincirinin başına
gönderilemez.

### Red izi

Hem Stage 1 (talebe dönüş) hem Stage 2 (SYB'ye geri gönderme) reddi sözleşmeye
`WasRejected` / `LastRejectionNote` yazar; zincirde ileri gidildiğinde temizlenir.
Böylece onay kuyruğu bir kaydın **tekrar** inceleme olduğunu ve sebebini
gösterebiliyor.

### Talep / sonuç ayrımı

`ContractRevision` ve `ContractTermination` birer **talep** kaydıdır; sonuçları
`IsApproved` alanında tutulur (`null` = karar bekliyor). Sonuç yalnızca zincir
bittiğinde işaretlenir — Stage 1 onayı talebi bir sonraki aşamaya taşır,
sonuçlandırmaz. Karar, sözleşme durumu ve onay kaydıyla **aynı transaction'da**
yazılır.

### Eşzamanlılık

`Contract.RowVersion` (`[Timestamp]`). Karar/düzenleme uygulanırken kullanıcının
ekranda gördüğü sürüm veritabanındakiyle karşılaştırılır; araya başka bir
güncelleme girdiyse `DbUpdateConcurrencyException` yerine anlaşılır bir
`InvalidOperationException` fırlatılır.

### Para birimi

Sözleşme bazında ISO kodu tutulur (TRY/EUR/USD). **Kur dönüşümü yapılmaz**;
gösterge panelindeki toplamlar para birimi başına ayrı hesaplanır.

Tutar biçimlendirme ve ayrıştırma **tr-TR** kültürüne sabitlenmiştir. İkisinin
farklı kültür kullandığı bir yerde ("12.348,00" metnini geçerli kültürle
ayrıştırmak) tutar sessizce yanlış okunuyordu.

## Bildirimler

`NotificationService` üzerinden üretilir. Bildirim yazılamazsa (bağlantı hatası
vb.) asıl işlem **başarılı sayılmaya devam eder** — bildirim kritik olmayan bir
yan etkidir.

Tetikleyiciler: yeni talep (SYB'ye), aşama ilerlemesi (o aşamanın rolüne), karar
sonucu (talep sahibine), ihlal bildirimi ve giderilmesi, yaklaşan bitiş, şifre
sıfırlama talebi (Admin'e).

Kabuk (`ShellView`) okunmamış sayısını periyodik olarak tazeler.

## Yazdırma ve PDF

İki ayrı yol:

- **Yazdır** — künye HTML olarak üretilip varsayılan tarayıcıda açılır, sayfa
  yüklenince `window.print()` çalışır. Windows'ta varsayılan PDF uygulaması
  genellikle Edge ve Edge `.pdf` için kabuk `print` fiilini **kaydetmiyor**;
  PDF üzerinden yazdırma hiç başlamıyordu.
- **PDF Kaydet** — PDFsharp ile A4 belge üretilir (`ContractPdfExporter`).
  Sayfa kırılımı ve metin akışı elle yönetilir; PDFsharp düşük seviyeli bir
  kitaplıktır.

Her iki çıktı da denetim kaydına yazılır: sözleşme verisi uygulama dışına
çıkıyor, kimin ne zaman aldığı izlenebilmeli.

## Bakım İşi

`MaintenanceService`, uygulama açıkken saatlik çalışır. Kullanıcının sunucuya
erişimi olmadığı için zamanlanmış görev/servis kullanılamıyor; tek düğüm garantisi
veritabanı kilidiyle sağlanır (`ScheduledJobRuns`). Kilit **tek bir koşullu
`UPDATE`** ile alınır — oku-sonra-yaz yarışı yoktur — ve süresi dolunca
kendiliğinden düşer.

İş, bitiş tarihi geçen Aktif/Uyarı/İhlal sözleşmeleri `Tamamlandi`'ya, bitişi
30 günden yakın olanları `Uyari`'ya çeker.

## Testler

`Sys.Services.Tests`, xUnit. `FakeContractRepository` ve
`FakeAttachmentRepository` ile servis katmanı veritabanısız sınanır. Kapsam
ağırlıklı olarak **yetki** ve **durum geçişi** kurallarıdır: yanlış rolün işlem
yapamaması, zorunlu gerekçe, kapatılmış talebin yeniden gönderilememesi,
reddedilen düzenlemenin geri alınması, ihlal kapandığında sözleşmenin doğru
duruma dönmesi.
