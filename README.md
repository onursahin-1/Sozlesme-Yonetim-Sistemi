# SYS — Sözleşme Yönetim Sistemi

Şirket içi, çok kullanıcılı masaüstü uygulaması (WPF, .NET 10, SQL Server Express).

## Katman Yapısı
- **Sys.Domain** — Entity sınıfları (Contract, User, vb.). Hiçbir projeye bağımlı değil.
- **Sys.Services** — İş kuralları ve servisler (ContractService, ApprovalWorkflowService, AuthService). Sadece Sys.Domain'e bağımlı.
- **Sys.Infrastructure** — Veritabanı erişimi (EF Core), dosya işlemleri, loglama.
- **Sys.UI** — WPF arayüzü (View + ViewModel).

## Kurulum
- SQL Server Express (SQLEXPRESS instance), TCP/IP etkin, Mixed Mode Authentication.
- Bağlantı bilgisi kodda düz metin tutulmaz (DPAPI ile şifrelenecek — Gün 2).

## İsimlendirme Kuralları
- Sınıf/metot adları: PascalCase
- Private alanlar: _camelCase
- Async metotlar: Async son eki (örn. GetContractAsync)