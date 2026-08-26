# SYS — Sözleşme Yönetim Sistemi

Şirket içi, çok kullanıcılı masaüstü uygulaması. Sözleşme taleplerinin
oluşturulması, onay zincirinden geçirilmesi, yürürlükteki sözleşmelerin
izlenmesi (revizyon, ihlal, fesih) ve arşivlenmesi için.

**Teknolojiler:** .NET 10 · Avalonia 12 · CommunityToolkit.Mvvm · EF Core ·
SQL Server Express · PDFsharp (PDF) · ClosedXML (Excel) · BCrypt.Net

## Başlıca İşlevler

- Talep → SYB son kontrolü → yönetim onayı şeklinde iki aşamalı onay zinciri
- Yürürlükteki sözleşmelerde düzenleme, fesih ve ihlal yönetimi (ihlaller
  giderilebilir; sözleşme durumu kendiliğinden geri döner)
- **Sözleşme yenileme** — süresi dolan sözleşmeden yeni dönem talebi
- Arşiv (tamamlanan, feshedilen, reddedilen kayıtlar), arama ve sayfalama
- Excel'e aktarma, yazdırma ve PDF çıktısı — üçü de denetim kaydına yazılır
- Bildirimler: yaklaşan bitiş (30/15/7 gün), onay bekleyen iş, karar sonucu
- Şifre politikası ve yönetici tarafından belirlenen şifreler için zorunlu ilk
  değişim
- Denetim kaydı (kim, ne zaman, ne yaptı) ve filtrelenebilir işlem geçmişi

## Katman Yapısı

| Proje | İçerik | Bağımlılık |
|---|---|---|
| `Sys.Domain` | Entity sınıfları ve enum'lar | yok |
| `Sys.Services` | İş kuralları, yetki kontrolleri, repository arayüzleri | Domain |
| `Sys.Infrastructure` | EF Core repository'leri, migration'lar, dosya işlemleri | Domain, Services |
| `Sys.UI` | Avalonia arayüzü (View + ViewModel), PDF/yazdırma | tümü |
| `Sys.Services.Tests` | xUnit birim testleri (sahte repository ile) | Domain, Services |

İş kuralları **yalnızca** `Sys.Services` içinde. ViewModel'ler kural işletmez;
doğrulama yaparlar ama yetki ve durum geçişi kararı servistedir.

## Kurulum

1. **SQL Server Express** (`SQLEXPRESS` instance), TCP/IP etkin, Mixed Mode
   Authentication. Veritabanı adı: `SysDb`.
2. `Sys.UI/appsettings.Local.json.example` dosyasını `appsettings.Local.json`
   olarak kopyalayıp bağlantı dizesini girin. Bu dosya `.gitignore`'da.
3. Migration'ları uygulayın — Package Manager Console, **Default project:
   `Sys.Infrastructure`**:
   ```
   Update-Database
   ```
4. Örnek hesapları isterseniz `appsettings.Local.json` içine
   `"EnableDevSeed": true` ekleyin. Bu satır olmadan **hiçbir hesap
   oluşturulmaz** — paylaşılan bir kurulumda bilinen şifreli hesaplar
   açılmasın diye varsayılan `false`.

   Açıksa ve `Users` tablosu boşsa üç hesap oluşur: `personel`, `syb`, `mudur`.

> **Admin hesabı hiçbir durumda seed edilmez.** Kullanıcı yönetimi ekranına
> erişmek için veritabanında elle bir Admin kaydı oluşturulması gerekir
> (`Role = 3`, `PasswordHash` bir BCrypt özeti olmalı).

Migration'lar uygulama her açılışta otomatik olarak da çalışır
(`Database.MigrateAsync`); 3. adım yalnızca ilk kurulumu hızlandırmak için.

## Roller

| Rol | Yetki |
|---|---|
| **Personel** | Kendi taleplerini oluşturur, düzenler ve görüntüler; ihlal bildirir; sözleşme yeniler |
| **SYB** | Talepleri sözleşmeye dönüştürür, son kontrolü yapar, düzenleme/fesih talebi açar, ihlal bildirir ve giderir, sözleşme yeniler |
| **Müdür** | Onay zincirinin ikinci aşaması; işlem geçmişini görür |
| **Admin** | Kullanıcı yönetimi ve şifre sıfırlama; sözleşme akışına katılmaz |

## Onay Zinciri

```
Talep (Stage 0)
   ↓ SYB sözleşmeyi oluşturur
SYB Son Kontrol (Stage 1)
   ↓ onay                    ↘ red → talep sahibine döner
Yönetim (YK) Onayı (Stage 2)
   ↓ onay                    ↘ red → SYB son kontrolüne döner
Yürürlükte (Stage 3)
```

Düzenleme ve fesih talepleri de aynı zincirden geçer; sözleşme onay sürecinde
`PendingEdit` / `PendingTermination` bayrağıyla işaretlenir ve karar verilene
kadar önceki durumunu `PreviousStatusBefore…` alanlarında saklar.

## Belgeler

- [Uygulama Mimarisi](SYS_Uygulama_Mimarisi.md) — katmanlar, desenler, iş kuralları
- [Veritabanı Şeması](SYS_Veritabani_Semasi.md) — tablolar, alanlar, ilişkiler

## İsimlendirme Kuralları

- Sınıf/metot adları: PascalCase
- Private alanlar: `_camelCase`
- Async metotlar: `Async` son eki (örn. `GetContractAsync`)
- Enum değerleri veritabanına **int** olarak yazılır — yeni değer **mutlaka
  listenin sonuna** eklenir, araya sokulursa mevcut kayıtların anlamı kayar.
