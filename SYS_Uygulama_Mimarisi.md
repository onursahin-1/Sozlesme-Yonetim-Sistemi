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

**Renkler paletten gelir.** Ekranlar renk yazmaz, `Themes/Palette.axaml`
içindeki ROL adlarına başvurur (`{DynamicResource TextPrimary}`). Sözlük tema
başına ayrı değer taşır; açık/koyu geçişi tek yerden yönetilir. ViewModel'lerin
ürettiği renkler de palet anahtarı döndürür ve `ThemeBrushConverter` üzerinden
çözülür.

> Renkler eskiden 20 dosyada 919 sabit hex olarak duruyordu ve
> `RequestedThemeVariant` "Light"e sabitlenmişti. Taşıma sırasında aynı hex'in
> farklı rollerde kullanıldığı beş yer çıktı (üst çubuk zemini metin rengiyle
> aynı hex'ti, dolu buton zemini yazı rengiyle aynıydı…). Bu yüzden isimler ton
> değil **rol** anlatır.

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

**Stil mi, yerel değer mi.** Avalonia'da öğenin üzerine doğrudan yazılan değer,
stil setter'ını **ezer**. Bir özellik duruma göre değişecekse (örn. Şifre
Değiştir ekranının zorunlu modda ortalanması) her iki hâli de stilde tanımlanır;
biri yerel biri stil olursa stil hiç uygulanmaz. Sınıf, `Classes.ad="{Binding …}"`
ile koşullu bağlanır.

**Sınırsız sorgu yazılmaz.** Sözleşme tablosunun tamamını çeken bir sorgu bilerek
bırakılmadı; bu tür sorgular az veriyle test edilirken doğru çalışıyor görünür ve
yalnızca kayıt sayısı arttıkça ısırır. Sayfalı listeler, durum bazlı sorgular,
`COUNT` sorguları ve sonuç sınırlı seçici sorguları kullanılır. Aynı sebeple
`GetAllAsync` / `GetByCreatedUserAsync` gibi yardımcılar, son çağıranları
kaldırıldıktan sonra **silindi** — dururlarsa er ya da geç yeniden kullanılırlar.

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
| Sözleşme yenile | Personel, SYB |
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

Sözleşme listesindeki **"Yürürlükte"** filtresi ve gösterge panelinin ilk kutusu
da aynı kümeye bağlandı. Liste eskiden yalnızca `Aktif` durumundakileri
getiriyordu: bitişi yaklaşan bir sözleşme (`Uyarı`) listeden düşüyordu, oysa
yükümlülükleri sürüyor ve üzerinde ihlal bildirilebiliyor. Aynı soruya sistemde
iki cevap vardı — biri düğmenin adından, diğeri iş kuralından geliyordu.
"Bitiş Uyarısı" ve "Açık İhlal" kutuları bu kümenin alt kırılımı olarak duruyor;
dördü toplanacak bir bölüm değil, bir başlık ve iki uyarı.

`FinalizeContractAsync` yalnızca `Talep` durumundaki kaydı kabul eder — kapatılmış
bir talep ya da yürürlükteki bir sözleşme yeniden onay zincirinin başına
gönderilemez.

### Son Kontrol'ün atlanması

Sözleşmeyi her zaman SYB oluşturur. Talebi de **aynı SYB** açtıysa, Son Kontrol
o kişinin kendi girdiği veriyi kendisinin onaylaması demek olur — denetim değeri
üretmeyen bir tekrar. Bu durumda sözleşme doğrudan yönetim onayına (Stage 2)
gider.

| Talebi açan | Sözleşmeyi oluşturan | Zincir |
|---|---|---|
| Personel | SYB | Talep → **Son Kontrol** → Yönetim → Yürürlükte |
| SYB (başkası) | SYB | Talep → **Son Kontrol** → Yönetim → Yürürlükte |
| SYB (aynı kişi) | Aynı SYB | Talep → Yönetim → Yürürlükte |

Kontrol listesi kaybolmuyor, **yer değiştiriyor**: atlanan durumda maddeler
Sözleşme Yarat sihirbazının son adımında soruluyor ve hepsi işaretlenmeden
sözleşme kaydedilemiyor. Maddeler zaten veri girişini doğruluyor (SAP cari kodu,
bedel kalemleri, tarihler), kararı değil — asıl yerleri veri girişinin sonu.
İki ekran da `WizardChecklist.Labels`'tan besleniyor ki zamanla ayrışmasınlar.

Atlama denetim kaydına **açıkça** yazılıyor; aksi halde geçmişe bakan biri bir
adımın sessizce eksik kaldığını sanardı.

Atlama kararı `Contract.FinalCheckSkipped` alanında **saklanıyor**. Karar
oluşturma anında veriliyor ama sonucu red anında da gerekiyor ve o an işlemi
yapan kişi Müdür olduğu için yeniden hesaplanamıyor.

**Stage 1 silinmedi.** Yeni sözleşme yolundan çıkarıldı ama iki işi duruyor:
düzenleme talebinin başlangıcı ve fesih talebinin başlangıcı. Müdür reddinde de
hâlâ dönüş noktası — ama yalnızca Son Kontrol'ü **başkası** yapacaksa.

### Reddedilen sözleşme nereye döner

Son Kontrol ekranında düzeltme yapılamaz; orada yalnızca onay ve red vardır.
Bu yüzden Müdür reddinin Stage 1'e dönmesi ancak Son Kontrol'ü **başka biri**
yapacaksa anlamlı: o kişi Müdür'ün itirazını değerlendirir ve gerekiyorsa
talebi sahibine iade eder.

Son Kontrol atlanmışsa böyle bir ara mercii yok. Stage 1'e dönmek SYB'yi kendi
sözleşmesini yeniden onaylayan bir ekrana düşürüyordu; ekranda tek anlamlı
eylem "Reddet" olduğu için SYB, düzeltebilmek için **kendi talebini reddetmek**
zorunda kalıyordu. Artık doğrudan `Talep` durumuna dönüyor: sözleşme, verileri
dolu olarak Sözleşme Yarat ekranında açılıyor. Sözleşme numarası korunuyor,
kalemler ekleme değil değiştirme ile yazılıyor.

| Son Kontrol | Müdür reddettiğinde |
|---|---|
| Yapıldı (Personel/başka SYB talebi) | Stage 1 — Son Kontrol'e döner |
| Atlandı (kendi talebi) | Stage 0, `Talep` — Sözleşme Yarat'a döner |

### Red izinde "kim geri gönderdi"

`WasRejected` yalnızca "reddedildi mi" der. Ama `Talep` + reddedilmiş görünümü
**üç** ayrı olaydan doğuyor: SYB'nin talebi sahibine iade etmesi, Müdür'ün
Son Kontrol'ü atlanmış bir sözleşmeyi geri göndermesi, ve kişinin kendi talebini
geri çekmesi. Üçünde de kullanıcıya söylenecek cümle farklı.

Bunu `FinalCheckSkipped`'tan çıkarmaya çalışmak yanlıştı: o alan "Son Kontrol
atlandı mı" sorusuna ait. Kendi talebini geri çeken SYB'ye "Yönetim onayından
döndü" yazıyordu. Ayrı soru, ayrı alan: `LastRejectedStage` (0 / 1 / 2), red
yazılırken **aşama değişmeden önce** doldurulur ve zincirde ileri gidildiğinde
red izinin geri kalanıyla birlikte temizlenir.

### Dil desteği (TR / EN)

Arayüz iki dilli. Tercih `%AppData%\SYS\language.json` içinde — tema gibi, o
bilgisayardaki görüntü ayarı; veritabanına yazılsaydı aynı hesapla farklı
makinelerden girildiğinde biri diğerini ezerdi.

**Tek sözlük, çift değer.** `Strings.Map` her anahtarın Türkçe ve İngilizce
karşılığını YAN YANA tutar:

```csharp
["Nav.Dashboard"] = ("Gösterge Paneli", "Dashboard"),
```

Ayrı iki sözlük daha düzenli görünürdü ama bu projede aynı bilgiyi iki yerde
tutmak defalarca hata üretti. Böyle yazıldığında bir anahtarın bir dilde eksik
kalması **mümkün değil**. Sözlükte olmayan anahtar ekranda `[Nav.Dashboard]`
olarak görünür — boşluk bırakmak yerine göze batar.

XAML'den `{Binding [Anahtar], Source={x:Static loc:Strings.Current}}`, C#'tan
`Strings.T("Anahtar")`. Dil değişince indeksleyicinin tamamı "değişti" olarak
duyurulur ve kabuk açık sayfayı yeniden kurar.

**Servis katmanı metin üretmez.** İş kuralı `AppError` kodu döndürür, metni
arayüz seçer (`ErrorText`). Eskiden arayüz servis mesajının İÇİNDE kelime
arıyordu — `ErrorMessage.Contains("Mevcut şifreniz")` — ve çeviri o koşulu
sessizce bozardı. Aynı sınır panelin "Sizi bekleyen işler" başlıklarında da
geçerli: servis anahtar döndürür.

**Ne çevrilir, ne çevrilmez.**

| Çevrilir | Çevrilmez |
|---|---|
| Etiket, düğme, ipucu, doğrulama mesajı | Denetim kaydının `Detail` metni |
| Durum rozetleri, süreç adımı açıklamaları | `ApprovalLog.StepName` |
| Hata kodlarının karşılığı | Açılır liste değerleri (fesih türü, ödeme periyodu, firma türü, tazminat yönü, ihlal türü) |

Ayrım şu: **sistemin ürettiği** her şey iki dilde, **kaydedilmiş** olan her şey
yazıldığı dilde. Bir denetim kaydını dile göre farklı göstermek, olay anında ne
yazıldığından başka bir şey göstermek olurdu. Açılır liste değerleri de seçilince
veritabanına yazılıyor; çevrilseydi aynı sözleşme iki dilde iki farklı değer
taşırdı.

**Bildirimler de anahtar olarak saklanıyor.** `Notification.TitleKey` /
`MessageKey` / `MessageArgs`; metin okunduğu anda kuruluyor. Bildirim geçici bir
mesaj, denetim kaydı gibi kurumsal kayıt değil — bu yüzden "kaydedilmiş olan
yazıldığı dilde kalır" kuralı burada uygulanmıyor. Parametrelerin bazıları
kendisi de anahtar olabiliyor (konu adı, durum adı); `Strings.Has` ile ayırt
ediliyor. Anahtarı olmayan eski kayıtlar saklanmış metne düşüyor.

**Tarih ve tutar biçimi dile bağlı değil.** Her iki dilde de `tr-TR`
(31.12.2026 · 15.678,00 TL). Şirket içi bir sistem; sözleşme tutarı ve tarihi
Türk mevzuatına göre yazılıyor ve aynı sözleşmenin PDF'i iki dilde farklı
okunmamalı.

### Kendi işleminin bildirimi gönderilmez

`NotifyAsync` alıcı listesinden işlemi yapan kişiyi çıkarır. Bildirimler "senin
adına bir şey oldu" demek için var; işlemi yapan zaten haberdar. SYB kendi
talebini işlediğinde sistem kendi kararını kendisine "Talebiniz reddedildi"
diye bildiriyordu — rozet şişiyor, gerçek işler görünmez oluyordu.

Aynı ayrım arayüzde de var: başkasının talebini **reddetmek** bir karar, kendi
talebini **geri çekmek** bir vazgeçme. Buton ve diyalog metinleri talebin
sahibine göre değişiyor (`ContractCardViewModel.IsOwnRequest`); işletilen kural
aynı.

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

### Sözleşme yenileme

Süresi dolan bir sözleşmeden yeni dönem talebi açılır. Yenileme, kaynak
sözleşmenin verisiyle **dolu bir talep formu** açar ama kaynağa hiç dokunmaz:
sonunda yeni bir kayıt doğar, eski sözleşme kendi durumunda ve kendi döneminde
kalır. Bağlantı `Contract.RenewedFromContractId` ile kurulur.

Yenilenebilir durumlar: **Aktif, Uyarı, İhlal, Tamamlandı**. Feshedilen sözleşme
yenilenemez — fesih, tarafların ilişkiyi sürdürmeme kararıdır; yeniden
çalışılacaksa bu sıfırdan verilecek yeni bir karardır. Talep ve onay
aşamasındaki kayıtlar da dışarıdadır: ortada yenilenecek bir dönem yoktur.

Taşınan ve taşınmayanlar bilinçli seçildi:

| | Taşınır mı? | Neden |
|---|---|---|
| Firma, vergi no, SAP cari, tür, para birimi | **Evet** | Yenilemede neredeyse hep aynı |
| Bedel kalemleri (sihirbazda) | **Evet** | Asıl yazma yükü burada; genelde yalnızca birim fiyatlar değişir |
| Referans numarası | Hayır | Her dönemin kendi referansı olur |
| Ekler | Hayır | Yeni dönemin kendi belgeleri yüklenir |
| Başlangıç / bitiş tarihi | **Hayır** | Eski tarihlerin forma gelmesi yanlışlıkla geçmişe dönük bir sözleşme kaydedilmesine yol açardı |

Yenileme yalnızca **detay ekranlarından** başlatılır (sözleşme detayı ve arşiv
detayı). Liste kartlarına konmadı: yenileme, önceki dönemin kalemlerine ve
koşullarına bakılarak verilen bir karardır; listedeki tek satır bilgiyle
başlatılması doğru olmaz.

### Şifre politikası

Şifre üç yoldan belirlenebilir: yönetici yeni kullanıcı oluştururken, yönetici
şifre sıfırlarken ve kullanıcı kendi şifresini değiştirirken. Kural üçünde de
tek kaynaktan (`PasswordPolicy`) gelir — eskiden yalnızca üçüncü yolda vardı,
yönetici yolları hiçbir doğrulama yapmadan doğrudan hash'liyordu.

Kural: en az 8 karakter, en az bir harf, en az bir rakam. Özel karakter
zorunluluğu yok — insanları tahmin edilebilir kalıplara itiyor.

Politika yalnızca şifre **belirlenirken** çalışır, girişte değil. Eski ve artık
kurala uymayan şifrelerle giriş yapılmaya devam edilir; aksi halde politika
değişikliği mevcut kullanıcıları sistemden kilitlerdi.

**Zorunlu ilk değişim:** yönetici şifre belirlediğinde `MustChangePassword`
kalkar. Bayraklı kullanıcı kabuğa hiç girmez — `MainViewModel` onu giriş
ekranıyla uygulama arasında tutar. Kabuk içinde kilitlenseydi bildirimler,
kısayollar ve arka plan yüklemeleri zaten çalışmaya başlamış olurdu. O ekranda
"giriş ekranına dön" bırakıldı: yanlış hesaba girmiş ya da geçici şifreyi
hatırlamayan biri uygulamayı kapatmak zorunda kalmasın.

Gerekçe denetimle ilgili: yöneticinin belirlediği şifreyi iki kişi bilir.
Kullanıcı onu değiştirmezse denetim kaydındaki "X — Son Kontrol onaylandı"
satırının gerçekten X'i mi gösterdiği ayırt edilemez.

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

`Sys.Services.Tests`, xUnit. Servis katmanı veritabanısız sınanır; her depo
arayüzünün bir sahtesi var (`FakeContractRepository`, `FakeAttachmentRepository`,
`FakeUserRepository`, `FakeAuthRepositories.cs` içindekiler).

| Dosya | Kapsam |
|---|---|
| `ContractServiceAuthorizationTests` | Yetki ve doğrulama kuralları |
| `ContractServiceApprovalTests` | Onay zinciri ve durum geçişleri |
| `ContractQueryScopeTests` | Sorguların ne kadar veri çektiği |
| `PasswordPolicyTests` | Şifre kuralı ve zorunlu ilk değişim |
| `AuthServiceTests` | Giriş, hesap kilitleme |
| `PasswordResetRequestTests` | "Şifremi unuttum" akışı |
| `NotificationServiceTests` | Yaklaşan bitiş eşikleri ve tekrar engeli |
| `AttachmentFileNameTests` | Ek dosyası adının diske yazılmadan temizlenmesi |
| `MaintenanceServiceTests` | Bakım işinin kilit ve hata davranışı |
| `DashboardTests` | Panel göstergelerinin neyi saydığı |

Üç test grubu, "çıktıyı doğrula" kalıbının dışında bir şey ölçüyor:

- **Sorgu kapsamı** — sonuca değil, servisin depoya **hangi daraltmayla
  gittiğine** bakar. Sınırsız sorgu hatasında sonuç doğru görünüyordu; sorun
  yalnızca çekilen veri miktarındaydı, o yüzden çıktıya bakan bir test bunu
  yakalayamazdı.
- **Şifre politikası** — asıl kritik test `LoginAsync_LegacyWeakPassword_StillWorks`:
  politika değişikliğinin mevcut kullanıcıları sistemden kilitlememesi gerekiyor.
- **Gizlilik kuralları** — var olmayan bir kullanıcı adıyla yanlış şifrenin aynı
  mesajı vermesi ve bulunamayan kullanıcı adının da kaydedilmesi. Bunlar
  görünmeyen davranışlar; test edilmezse ileride biri "daha yardımcı" bir hata
  mesajı yazarak sessizce bozar.
- **Bakım işi** — en kritik testi `Run_OnError_StillReleasesLock`: hata olsa bile
  kilidin bırakılması gerekiyor. Bırakılmazsa iş, kilit süresi dolana kadar
  (10 dk) askıda kalır ve bu da ekranda hiçbir belirti vermez.

Test projesi `Sys.Infrastructure`'a da referans veriyor ama **depo sınıfları
test edilmiyor** — onlar veritabanı gerektirir. Yalnızca veritabanına dokunmayan
saf yardımcılar (dosya adı temizleme) sınanıyor.

Kapsam dışı: depo sınıfları ve `Sys.UI` katmanı.

## Gösterge Paneli

Panel tek bir servis çağrısıyla doldurulur (`GetDashboardSummaryAsync`); kutu
başına ayrı çağrı hem yavaş olur hem de ekran parça parça dolardı. Sorguların
hepsi veritabanı tarafında sayı/özet döner, sözleşme kayıtları belleğe çekilmez.

Panelin ölçüsü **aksiyona dönüp dönmediği**. Bu ölçüyle iki kutu değişti:

- **Bitiş takvimi (30/60/90) kaldırıldı.** "Bitiş Uyarısı" kartı ve "Yaklaşan
  Bitişler" listesi zaten aynı bilgiyi veriyordu; takvim üçüncü tekrardı. 60 ve
  90 günlük dilimler de bugün yapılacak bir işe karşılık gelmiyordu.
- **Tür dağılımı tıklanabilir oldu.** Eskiden yalnızca sayı gösteriyordu ve
  hiçbir yere gitmiyordu. Artık bir türe basınca sözleşme listesi o türe
  filtrelenmiş açılır. Bunun için tür filtresi uçtan uca eklendi (sorgu, servis,
  liste ekranındaki açılır seçici). Müdür'de genel sözleşme listesi ekranı
  olmadığı için satırlar orada tıklanamaz kalır — ama kutu gösterilmeye devam
  eder: portföyün türe göre dağılımı bir yönetici için başlı başına anlamlı.

  Tıklama `IsEnabled` ile değil **`IsHitTestVisible`** ile kapatılır. `IsEnabled`
  satırın tamamını soluklaştırıp bozuk gösteriyordu; oysa burada devre dışı bir
  denetim yok, yalnızca gidilecek bir yer yok. Aynı sebeple "tıklayın" diyen
  ipucu da o rolde gizlenir.

Üç gösterge, "neyi saydığı" düzeltilerek eklendi:

| Gösterge | Eskiden | Şimdi |
|---|---|---|
| İhlal kartı | Durumu `Ihlal` olan **sözleşme** sayısı | **Açık ihlal adedi** — bir sözleşmede birden fazla açık ihlal olabilir |
| | | (yalnızca yürürlükteki sözleşmelerde) |
| Yaklaşan bitişler | Yalnızca sözleşme ve kalan gün | Ayrıca **yenilenmiş mi** rozeti |
| Sizi bekleyen işler | Yalnızca adet | Ayrıca **en eskisi kaç gündür bekliyor** |

Yenileme rozeti ayrı bir sorgudan gelir: bağlantı ters yönde tutuluyor (yeni
kayıt eskisini işaret ediyor), bu yüzden "bu sözleşme yenilendi mi" sorusu
sözleşmenin kendisinden okunamıyor. Sorgu yalnızca ekranda görünecek kayıtlar
için çalışır. Reddedilmiş ve feshedilmiş yenilemeler sayılmaz — yenileme
borcunu kapatmazlar.

Bekleme süresi kritik olmayan bir ek bilgidir: sorgusu başarısız olursa panel
yine açılır, yalnızca o satır boş kalır. Bilinmiyorsa hiç gösterilmez;
uydurulmuş bir "0 gün" yanlış bilgi olurdu.

Açık ihlal sayımı **yalnızca durumu `Ihlal` olan sözleşmeleri** kapsar. Kural
şu: kartın sayısı ile kartın götürdüğü liste **aynı kümeyi** göstermeli. Kart
"ihlal" filtresine gidiyor, o filtre de `Status == Ihlal` olanları listeliyor;
sayım daha geniş olsaydı kullanıcı "3 açık ihlal" görüp tıklar, listede iki
sözleşme bulur ve üçüncüyü arardı.

Kapsam dışında kalan iki durum:

- **Kapanmış sözleşme** (Tamamlandı/Feshedildi) — ihlal kaydı açık kalmış
  olabilir ama artık bir aksiyon gerektirmiyor.
- **Onay zincirindeki sözleşme** (OnayBekliyor) — düzenleme ya da fesih talebi
  karara bağlanana kadar durum geçici olarak `OnayBekliyor`'dur, ihlal "donmuş"
  sayılır. Talep reddedilirse sözleşme `Ihlal`'e döner ve ihlal yeniden sayıma
  girer.

Yenileme rozeti yalnızca **yenilenmişlerde** gösterilir. "Yenilenmedi" etiketi de
denendi ama listedeki her satırda kırmızı bir rozet belirdiği için gürültü
yapıyordu; rozetin yokluğu zaten aynı anlama geliyor.

## Ek Dosyaları

Yükleme üç aşamada doğrulanır:

1. **Uzantı** — yalnızca pdf, docx, xlsx, jpg, png
2. **Boyut** — en fazla 10 MB
3. **Dosya imzası** — ilk baytlara bakılıp içeriğin gerçekten o tür olduğu
   doğrulanır. Sadece uzantıya bakmak yetmez; bir dosya kolayca yeniden
   adlandırılabilir (`zararli.exe` → `sozlesme.pdf`)

Diske yazılacak ad ayrıca **temizlenir** (`SanitizeFileName`): geçersiz
karakterler, yol ayırıcıları, Windows'un ayrılmış aygıt adları (`CON`, `PRN`,
`COM1`…), sondaki nokta/boşluk ve aşırı uzun adlar. Ardından bir de hedef yolun
sözleşme klasörünün içinde kaldığı doğrulanır — temizleyici ileride değişirse
sessiz bir açık kalmasın diye ikinci bir savunma hattı.

Kullanıcıya gösterilen ad (`Attachment.FileName`) değişmez; temizlik yalnızca
dosya sistemindeki adı ilgilendirir. Böylece ekranda okunaklı ad korunurken
diske güvenli yazılır.
