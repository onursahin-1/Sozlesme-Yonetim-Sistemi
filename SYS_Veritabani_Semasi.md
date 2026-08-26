# SYS — Veritabanı Şeması

Veritabanı: `SysDb` (SQL Server Express). Şema EF Core migration'larıyla yönetilir;
elle DDL çalıştırılmaz.

Tüm enum alanları veritabanına **int** olarak yazılır. Yeni bir enum değeri
**mutlaka listenin sonuna** eklenir — araya sokulursa mevcut kayıtların anlamı kayar.

---

## Users

| Alan | Tip | Not |
|---|---|---|
| `Id` | int, PK | |
| `Username` | nvarchar | Giriş adı, benzersiz |
| `PasswordHash` | nvarchar | BCrypt |
| `FullName` | nvarchar | |
| `Role` | int | `UserRole` |
| `Department` | nvarchar, null | |
| `FailedLoginCount` | int | Ardışık başarısız giriş sayısı |
| `LockedUntil` | datetime2, null | Geçici kilit bitiş zamanı |
| `IsDisabled` | bit | Hesap devre dışı |
| `MustChangePassword` | bit | Şifreyi yönetici belirledi; kullanıcı ilk girişinde değiştirmeli |
| `PasswordChangedAt` | datetime2, null | Şifrenin son değiştiği an. Mevcut kayıtlarda null — geçmişte ne zaman değiştiği bilinmiyor |

**UserRole:** `0 Personel · 1 SYB · 2 Mudur · 3 Admin`

**Şifre kuralları** (`PasswordPolicy`): en az 8 karakter, en az bir harf, en az
bir rakam. Kural üç yolda da (yeni kullanıcı, şifre sıfırlama, kendi şifresini
değiştirme) aynı kaynaktan uygulanır.

Kural yalnızca şifre **belirlenirken** çalışır, girişte değil: eski ve artık
kurala uymayan şifrelerle giriş yapılmaya devam edilir. Aksi halde politika
değişikliği mevcut kullanıcıları sistemden kilitlerdi.

`MustChangePassword` neden var: yöneticinin belirlediği şifreyi iki kişi bilir.
Kullanıcı onu değiştirmezse, denetim kaydındaki "X — Son Kontrol onaylandı"
satırının gerçekten X'i mi yoksa yöneticiyi mi gösterdiği ayırt edilemez.
Bayraklı kullanıcı kabuğa hiç girmez; giriş ekranıyla uygulama arasında
tutulur.

---

## Contracts

Talep ve sözleşme **aynı** tabloda tutulur; ayrım `Status` ve `Stage` ile yapılır.
Bir kayıt `Talep` olarak doğar, SYB sözleşmeye dönüştürünce `ContractNo` alır.

| Alan | Tip | Not |
|---|---|---|
| `Id` | int, PK | |
| `RequestRefNo` | nvarchar | Talep referansı (SAS / YK) |
| `ContractNo` | nvarchar, null | `SYBSA<AAYYYY><0000>`; yalnızca sözleşmeye dönüşünce atanır. **Benzersiz index** |
| `Title`, `Description`, `Type` | nvarchar | Konu, kapsam, sözleşme türü |
| `CompanyName`, `TaxNo` | nvarchar | Karşı taraf |
| `SapCariKodu`, `CompanyType` | nvarchar, null | |
| `PaymentPeriod` | nvarchar, null | Ödeme periyodu |
| `Status` | int | `ContractStatus` |
| `Stage` | int | 0 talep · 1 SYB son kontrol · 2 yönetim onayı · 3 zincir bitti |
| `StartDate`, `EndDate` | datetime2, null | |
| `TotalAmount` | decimal | Kalemlerin toplamı (sözleşmeye dönüştükten sonra) |
| `Currency` | nvarchar | ISO kodu (TRY/EUR/USD). **Kur dönüşümü yapılmaz**, toplamlar para birimi başına ayrı hesaplanır |
| `WasRejected` | bit | Son karar red mi? Onayla ilerlendiğinde temizlenir |
| `LastRejectionNote`, `LastRejectedAt` | nvarchar/datetime2, null | |
| `PendingEdit`, `PendingTermination` | bit | Onay bekleyen düzenleme/fesih talebi var mı |
| `PreviousStatusBeforeEdit` | int, null | Düzenleme reddedilirse dönülecek durum |
| `PreviousStatusBeforeTermination` | int, null | Fesih reddedilirse dönülecek durum |
| `CreatedByUserId` | int, FK → Users | |
| `CreatedAt` | datetime2 | |
| `RenewedFromContractId` | int, null | Bu kayıt hangi sözleşmenin yenilenmesiyle doğdu. null = sıfırdan açılmış talep |
| `RowVersion` | rowversion | Eşzamanlılık denetimi (`[Timestamp]`) |

**RenewedFromContractId için gezinme özelliği tanımlanmadı.** Kendine referans
veren bir `Contract` navigation'ı, zaten yedi tabloyu birden çeken detay
sorgusunda istemeden zincirleme yükleme riski taşırdı. Bağlantı gerektiğinde
kimlik üzerinden ayrıca sorgulanır (`GetRenewalSourceSummaryAsync`, yalnızca
`ContractNo`/`RequestRefNo`/`EndDate` çeker). Bu yüzden veritabanı seviyesinde
foreign key kısıtı da yoktur; yalnızca filtreli bir index vardır.

**Yenilenebilir durumlar:** Aktif, Uyari, Ihlal, Tamamlandi. Feshedilen
sözleşme yenilenemez — fesih, tarafların ilişkiyi sürdürmeme kararıdır;
yeniden çalışılacaksa bu, eski sözleşmenin devamı değil sıfırdan verilecek
yeni bir karardır.

**ContractStatus:**
`0 Talep · 1 OnayBekliyor · 2 Aktif · 3 Uyari · 4 Ihlal · 5 Tamamlandi · 6 Feshedildi · 7 Reddedildi`

- **Uyari** — bitişine 30 günden az kalmış, yürürlükteki sözleşme
- **Reddedildi** — sözleşmeye hiç dönüşmeden kapatılmış talep (terminal)
- **Arşiv kümesi:** Tamamlandi, Feshedildi, Reddedildi

---

## ContractItems

| Alan | Tip | Not |
|---|---|---|
| `Id` | int, PK | |
| `ContractId` | int, FK → Contracts | |
| `Description` | nvarchar | |
| `Quantity` | int | |
| `Unit` | nvarchar | Listeden seçilir (adet, kg, saat…) |
| `UnitPrice` | decimal | |

`LineTotal` hesaplanmış özelliktir, tabloda tutulmaz.

> Sözleşme yeniden oluşturulduğunda (Son Kontrol reddi sonrası) kalemler
> **eklenmez, değiştirilir** — aksi halde eski kalemler kalır ve `TotalAmount`
> ile kalem toplamı birbirini tutmaz.

---

## Attachments

| Alan | Tip | Not |
|---|---|---|
| `Id` | int, PK | |
| `ContractId` | int, FK → Contracts | |
| `Category` | int | `AttachmentCategory` |
| `FileName` | nvarchar | Kullanıcıya gösterilen ad |
| `FilePath` | nvarchar | Diskteki yol; dosya veritabanında tutulmaz |
| `UploadedAt` | datetime2 | |
| `UploadedByUserId` | int | |

**AttachmentCategory:** `0 Talep · 1 Sozlesme · 2 Ek · 3 Teminat · 4 Ihlal · 5 Fesih`

---

## ApprovalLogs

Onay zincirindeki her karar. Sözleşme detayındaki zaman çizelgesi bu kayıtlardan
üretilir.

| Alan | Tip | Not |
|---|---|---|
| `Id` | int, PK | |
| `ContractId` | int, FK → Contracts | |
| `StepNumber` | int | 0 talep incelemesi · 1 SYB son kontrol · 2 yönetim onayı |
| `StepName` | nvarchar | Fesih/düzenleme kararlarında ek taşır: `"SYB Son Kontrol (Fesih)"` |
| `ActingUserId` | int | |
| `Decision` | int | `0 Onay · 1 Red` |
| `Note` | nvarchar, null | Redde zorunlu |
| `ActionDate` | datetime2 | |

---

## ContractRevisions

Düzenleme **talebi** ve sonucu. Kaydedilen değerler değişiklikten **önceki**
hâldir; yeni değerler sözleşmenin kendisine yazılır. Talep reddedilirse sözleşme
bu kayıttan geri alınır.

| Alan | Tip | Not |
|---|---|---|
| `Id` | int, PK | |
| `ContractId` | int, FK → Contracts | |
| `ChangeType`, `Reason` | nvarchar | |
| `PreviousTotalAmount` | decimal | |
| `PreviousEndDate` | datetime2, null | |
| `PreviousDescription`, `PreviousCompanyName`, `PreviousTaxNo` | nvarchar | |
| `PreviousPaymentPeriod` | nvarchar, null | |
| `ChangedByUserId` | int | |
| `ChangedAt` | datetime2 | |
| `IsApproved` | bit, null | **null** karar bekliyor · **1** onaylandı · **0** reddedildi |
| `ResolvedAt` | datetime2, null | |

---

## ContractTerminations

Fesih **talebi** ve sonucu.

| Alan | Tip | Not |
|---|---|---|
| `Id` | int, PK | |
| `ContractId` | int, FK → Contracts | |
| `TerminationType`, `Reason` | nvarchar | |
| `TerminationDate` | datetime2 | Sözleşme başlangıcı ile bitişi arasında olmalı |
| `CompensationAmount` | decimal, null | |
| `CompensationDirection` | nvarchar | "Tazminat yok" ise tutar yazılmaz |
| `RequestedByUserId` | int | |
| `RequestedAt` | datetime2 | |
| `IsApproved` | bit, null | **null** karar bekliyor · **1** onaylandı · **0** reddedildi |
| `ResolvedAt` | datetime2, null | |

---

## Violations

İhlal bildirimi. Onay zinciri **yoktur** — kaydedildiği anda sözleşme `Ihlal`
durumuna geçer.

| Alan | Tip | Not |
|---|---|---|
| `Id` | int, PK | |
| `ContractId` | int, FK → Contracts | |
| `ViolationType`, `Description` | nvarchar | |
| `ViolationDate` | datetime2 | Gelecekte veya sözleşme başlangıcından önce olamaz |
| `ReportedByUserId` | int | |
| `ReportedAt` | datetime2 | |
| `ResolvedAt` | datetime2, null | **null ise ihlal açıktır** |
| `ResolvedByUserId` | int, null | |
| `ResolutionNote` | nvarchar, null | |

> Sözleşmenin **tüm** açık ihlalleri kapandığında durumu bitiş tarihine göre
> Aktif / Uyari / Tamamlandi'ya döner.

---

## AuditLog

Denetim kaydı. İşlem adları `AuditActionCatalog` içinde etiketlenir.

| Alan | Tip | Not |
|---|---|---|
| `Id` | int, PK | |
| `EntityName` | nvarchar | `"Contract"` / `"User"` |
| `EntityId` | int | |
| `Action` | nvarchar | `TalepOluşturuldu`, `İhlalGiderildi`, `EkSilindi`… |
| `ActingUserId` | int, FK → Users | |
| `Detail` | nvarchar, null | |
| `ActionDate` | datetime2 | |

---

## Notifications

| Alan | Tip | Not |
|---|---|---|
| `Id` | int, PK | |
| `UserId` | int, FK → Users | Alıcı |
| `ContractId` | int, null | Tıklandığında açılacak sözleşme |
| `Type` | int | `NotificationType` |
| `Title`, `Message` | nvarchar | |
| `IsRead` | bit | |
| `CreatedAt` | datetime2 | |
| `DedupeKey` | nvarchar, null | Tekrarlayan taramaların aynı olay için mükerrer bildirim üretmesini engeller. Olay anında üretilen bildirimlerde null |

`(UserId, DedupeKey)` üzerinde **filtreli benzersiz index** (`DedupeKey IS NOT NULL`) —
birden fazla null serbest.

**NotificationType:**
`0 YaklasanBitis · 1 OnayBekliyor · 2 TalepSonucu · 3 SozlesmeOlayi · 4 SifreSifirlamaTalebi`

**Yaklaşan bitiş eşikleri:** 30 · 15 · 7 gün (`NotificationService.EndingThresholds`).
Geçilen en küçük eşik seçilir: bitişe 10 gün kalmışsa 15'lik uyarı üretilir, 30'luk
zaten daha önce üretilmiştir. Bildirim, sözleşmeyi oluşturan kişiye ve tüm aktif SYB
kullanıcılarına gider.

> Bu eşikleri, gösterge panelindeki **bitiş takvimiyle** (30/60/90 gün dilimleri)
> karıştırmamak gerekir. Takvim bir görüntüleme aracı, eşikler ise bildirim üretiyor.

---

## PasswordResetRequests

Giriş ekranındaki "Şifremi unuttum" akışı. Kullanıcı adı sistemde yoksa da kayıt
oluşur — giriş ekranı, hesabın var olup olmadığını sızdırmamak için her durumda
aynı yanıtı verir.

| Alan | Tip | Not |
|---|---|---|
| `Id` | int, PK | |
| `Username` | nvarchar(150) | Kullanıcının yazdığı ad |
| `UserId` | int, null | Eşleşen kullanıcı; yoksa null |
| `RequestedAt` | datetime2 | |
| `IsHandled` | bit | Talep karşılandı ya da kapatıldı |
| `HandledAt` | datetime2, null | |
| `HandledByUserId` | int, null | |

---

## ScheduledJobRuns

Saatlik bakım işinin çalıştırma kaydı ve kilidi. Kullanıcının sunucuya erişimi
olmadığı için iş, uygulama açıkken veritabanı üzerinden **tek düğüm** garantisiyle
yürütülür: kilit tek bir koşullu `UPDATE` ile alınır (oku-sonra-yaz yarışı yok) ve
belirli bir süre sonra kendiliğinden düşer.

| Alan | Tip | Not |
|---|---|---|
| `Id` | int, PK | |
| `JobName` | nvarchar(100) | **Benzersiz** — her iş için tabloda tek satır olmalı, kilit mantığı buna dayanıyor |
| `LastRunAt` | datetime2, null | |
| `LockedUntil` | datetime2, null | Kilit bitiş zamanı |
| `LockedBy` | nvarchar, null | |
| `LastResult` | nvarchar, null | Son çalıştırmanın sonucu |

Bakım işi, bitiş tarihi geçen Aktif/Uyarı/**İhlal** sözleşmeleri `Tamamlandi`'ya,
bitişi 30 günden yakın olanları `Uyari`'ya çeker.

---

## İndeksler

**Benzersizlik kısıtları**

| İndeks | Not |
|---|---|
| `Users.Username` | |
| `Contracts.ContractNo` | Filtreli (`IS NOT NULL`) — talep aşamasındaki kayıtlarda numara yok. Aynı anda iki sözleşme yaratılırsa numara çakışması burada yakalanır |
| `Notifications (UserId, DedupeKey)` | Filtreli (`IS NOT NULL`) — mükerrer bildirimi engeller |
| `ScheduledJobRuns.JobName` | Her iş için tek satır |

**Performans indeksleri**

`Contracts.Status` · `Contracts.Stage` · `Contracts.EndDate` ·
`Contracts (CreatedByUserId, Status)` · `AuditLog.ActionDate` ·
`Notifications (UserId, IsRead)` · `Notifications.CreatedAt` ·
`PasswordResetRequests (IsHandled, RequestedAt)` · `PasswordResetRequests.Username`

`Contracts.RenewedFromContractId` — filtreli (`IS NOT NULL`); sözleşmelerin çoğu
yenileme değil, bu yüzden tamamı indekslenmiyor.

---

## Sorgu sınırları

Sözleşme tablosunu **sınırsız** çeken sorgu bilerek bırakılmadı. Bu tür sorgular az
veriyle test edilirken doğru çalışıyor görünür; sorun yalnızca kayıt sayısı arttıkça
ortaya çıkar.

| Amaç | Yöntem |
|---|---|
| Liste ekranları | `GetContractsPagedAsync` — filtre, arama ve sayfalama veritabanında |
| Durum bazlı listeler (düzenleme/fesih/ihlal, bekleyen talepler) | `GetByStatusesAsync` |
| Sözleşme seçici (Görüntüle ekranı) | `GetContractsForPickerAsync` — arama veritabanında, en fazla 30 sonuç |
| Sayaçlar ve rozetler | `CountByStageAsync`, `CountByStatusesAsync` — kayıt çekilmez |
| Excel'e aktarma | Sayfalanmaz (analiz için tüm eşleşenler gerekli) ama **10.000 satır** üst sınırı var |

**Ondalık alanlar** `decimal(18,2)` olarak tanımlıdır:
`Contracts.TotalAmount`, `ContractItems.UnitPrice`,
`ContractRevisions.PreviousTotalAmount`, `ContractTerminations.CompensationAmount`.
