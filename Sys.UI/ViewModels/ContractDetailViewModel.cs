using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class ContractDetailViewModel : ViewModelBase, IEscapeHandler
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;

    [ObservableProperty]
    public partial ObservableCollection<Contract> AvailableContracts { get; set; } = new();

    [ObservableProperty]
    public partial Contract? SelectedContract { get; set; }

    [ObservableProperty]
    public partial Contract? Detail { get; set; }

    // Künye alanları artık "Firma: X" gibi birleşik metin değil, ham değer olarak
    // tutuluyor. Ekranda etiket (küçük, gri, büyük harf) ve değer ayrı gösterildiği
    // için iki sütunlu düzen kurulabiliyor ve bilgi taranması kolaylaşıyor.

    [ObservableProperty]
    public partial string DetailTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailNo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailCompany { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailSapCariKodu { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailTaxNo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailType { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailTotal { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailRemainingDays { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailStart { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailEnd { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailRequester { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailDepartment { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailPeriod { get; set; } = string.Empty;

    // Durum, metin yerine renkli bir rozet olarak gösteriliyor — listedeki kartlarla
    // aynı renk paleti kullanılıyor ki kullanıcı aynı durumu her yerde aynı renkte görsün.
    [ObservableProperty]
    public partial string DetailStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailStatusColorHex { get; set; } = "#555555";

    [ObservableProperty]
    public partial string DetailStatusBgHex { get; set; } = "#EAECF0";

    // Revizyon geçmişindeki tutarlar ContractRevision üzerinden geliyor; o kayıtta
    // para birimi yok, sözleşmeninki geçerli. Ekranda ek olarak yazılabilsin diye.
    [ObservableProperty]
    public partial string CurrencySuffix { get; set; } = " TL";

    [ObservableProperty]
    public partial ObservableCollection<ContractItem> Items { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<Attachment> Attachments { get; set; } = new();

    // Sözleşmenin süreç zaman çizelgesi: talep, her onay/red kararı ve bugünkü durum.
    // Ayrı bir "Aşama Geçmişi" listesi tutulmuyor — bu koleksiyon zaten aynı
    // ApprovalLog kayıtlarından besleniyor, ikisi birlikte çelişkili görünüyordu.
    [ObservableProperty]
    public partial ObservableCollection<ContractStepViewModel> Steps { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<RevisionRowViewModel> Revisions { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<TerminationRowViewModel> Terminations { get; set; } = new();

    // İhlal kayıtları yazılıyor ama hiçbir ekranda gösterilmiyordu.
    [ObservableProperty]
    public partial ObservableCollection<ViolationRowViewModel> Violations { get; set; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    // Seçili sözleşmeden yeni dönem talebi açılabilir mi?
    [ObservableProperty]
    public partial bool CanRenew { get; set; }

    // Bu sözleşme bir başkasının yenilemesiyse, hangi sözleşmeden geldiği burada yazar.
    // Sözleşme geçmişini takip ederken "bu ilk dönem mi, kaçıncı dönem mi" sorusunun
    // cevabı aksi halde yalnızca kişilerin hafızasında kalıyordu.
    [ObservableProperty]
    public partial bool IsRenewal { get; set; }

    [ObservableProperty]
    public partial string RenewalSourceText { get; set; } = string.Empty;

    // "Sözleşmeler" ekranından belirli bir sözleşme için buraya yönlendirildiysek true olur.
    public bool ShowBackButton { get; }
    public event Action? BackRequested;
    public event Action<Contract>? RenewRequested;

    [RelayCommand]
    private void Back() => BackRequested?.Invoke();

    [RelayCommand]
    private void Renew()
    {
        if (Detail is not null) RenewRequested?.Invoke(Detail);
    }

    // Esc: yalnızca bir yerden yönlendirilerek gelindiyse (Geri butonu görünürken) çalışır.
    public bool CanHandleEscape => ShowBackButton;
    public void HandleEscape() => BackRequested?.Invoke();

    public ContractDetailViewModel() : this(null!, new User()) { } // yalnızca tasarımcı önizlemesi için

    public ContractDetailViewModel(ContractService contractService, User currentUser, Contract? initialContract = null)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        ShowBackButton = initialContract is not null;
        _ = InitializeAsync(initialContract);
    }

    private async Task InitializeAsync(Contract? initialContract)
    {
        await LoadListAsync();

        // "Sözleşmeler" ekranından "Detay" butonuyla buraya yönlendirildiysek,
        // ilgili sözleşmeyi listeden bulup otomatik olarak seçili hale getiriyoruz.
        if (initialContract is not null)
        {
            var match = AvailableContracts.FirstOrDefault(c => c.Id == initialContract.Id);
            SelectedContract = match ?? initialContract;
        }
    }

    private async Task LoadListAsync()
    {
        ErrorMessage = string.Empty;
        try
        {
            var contracts = await _contractService.GetContractsAsync(_currentUser);
            AvailableContracts = new ObservableCollection<Contract>(contracts);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Sözleşmeler yüklenirken bir hata oluştu: " + ex.Message;
        }
    }

    // Kullanıcı listede hızlıca birden fazla sözleşmeye art arda tıklarsa, eski bir
    // seçimin sorgusu yeni seçimden SONRA tamamlanabilir. Bu sayaç, her seçimde
    // artırılıp yakalanıyor; sonuç geldiğinde hâlâ "en son" istek mi diye kontrol
    // edilip değilse göz ardı ediliyor — böylece ekranda yanlış sözleşmenin
    // detayı görünmüyor.
    private int _loadRequestId;

    partial void OnSelectedContractChanged(Contract? value)
    {
        ErrorMessage = string.Empty;
        Steps = new ObservableCollection<ContractStepViewModel>();
        Items = new ObservableCollection<ContractItem>();
        Attachments = new ObservableCollection<Attachment>();
        Revisions = new ObservableCollection<RevisionRowViewModel>();
        Terminations = new ObservableCollection<TerminationRowViewModel>();
        Violations = new ObservableCollection<ViolationRowViewModel>();
        Detail = null;

        var requestId = ++_loadRequestId;
        _ = LoadDetailAsync(value, requestId);
    }

    private async Task LoadDetailAsync(Contract? summary, int requestId)
    {
        if (summary is null) return;

        IsLoading = true;
        try
        {
            var full = await _contractService.GetContractDetailAsync(summary.Id, _currentUser);
            if (requestId != _loadRequestId) return; // daha yeni bir seçim yapılmış, bu sonuç artık geçersiz

            if (full is null)
            {
                ErrorMessage = "Bu sözleşmeyi görüntüleme yetkiniz yok.";
                return;
            }

            Detail = full;

            var tr = CultureInfo.GetCultureInfo("tr-TR");
            var card = new ContractCardViewModel(full, currentUser: _currentUser);

            DetailTitle = full.Title;
            DetailNo = string.IsNullOrEmpty(full.ContractNo) ? full.RequestRefNo : full.ContractNo!;
            DetailCompany = full.CompanyName;
            DetailSapCariKodu = string.IsNullOrWhiteSpace(full.SapCariKodu) ? "-" : full.SapCariKodu!;
            DetailTaxNo = string.IsNullOrWhiteSpace(full.TaxNo) ? "-" : full.TaxNo;
            DetailType = string.IsNullOrWhiteSpace(full.Type) ? "-" : full.Type;
            DetailTotal = CurrencyHelper.Format(full.TotalAmount, full.Currency);
            CurrencySuffix = " " + CurrencyHelper.Symbol(full.Currency);
            DetailRemainingDays = card.GunKalanText;
            DetailStart = full.StartDate?.ToString("dd.MM.yyyy", tr) ?? "-";
            DetailEnd = full.EndDate?.ToString("dd.MM.yyyy", tr) ?? "-";
            DetailRequester = full.CreatedByUser?.FullName ?? "-";
            DetailDepartment = string.IsNullOrWhiteSpace(full.CreatedByUser?.Department) ? "-" : full.CreatedByUser!.Department!;
            DetailPeriod = string.IsNullOrWhiteSpace(full.PaymentPeriod) ? "-" : full.PaymentPeriod!;
            CanRenew = card.CanRenew;

            IsRenewal = false;
            RenewalSourceText = string.Empty;
            if (full.RenewedFromContractId is { } sourceId)
            {
                var source = await _contractService.GetRenewalSourceAsync(sourceId);
                if (requestId != _loadRequestId) return;
                if (source is { } s)
                {
                    IsRenewal = true;
                    RenewalSourceText = s.EndDate is { } end
                        ? $"{s.RefNo} numaralı sözleşmenin yenilemesidir (önceki dönem {end.ToString("dd.MM.yyyy", tr)} tarihinde sona erdi)."
                        : $"{s.RefNo} numaralı sözleşmenin yenilemesidir.";
                }
            }

            // Durum rozeti için liste kartlarıyla aynı etiket ve renkler.
            DetailStatus = card.StatusLabel;
            DetailStatusColorHex = card.StatusColorHex;
            DetailStatusBgHex = card.StatusBgHex;

            Items = new ObservableCollection<ContractItem>(full.Items);
            Attachments = new ObservableCollection<Attachment>(full.Attachments);
            Steps = new ObservableCollection<ContractStepViewModel>(ContractStepViewModel.Build(full));
            // En yeni kayıt üstte: geçmiş listelerinde son durum önce okunmalı.
            Revisions = new ObservableCollection<RevisionRowViewModel>(
                full.Revisions
                    .OrderByDescending(r => r.ChangedAt).ThenByDescending(r => r.Id)
                    .Select(r => new RevisionRowViewModel(r, CurrencySuffix)));

            Terminations = new ObservableCollection<TerminationRowViewModel>(
                full.Terminations
                    .OrderByDescending(t => t.RequestedAt).ThenByDescending(t => t.Id)
                    .Select(t => new TerminationRowViewModel(t)));

            // Açık ihlalleri yalnızca SYB kapatabilir.
            var canResolve = _currentUser.Role == UserRole.SYB;
            Violations = new ObservableCollection<ViolationRowViewModel>(
                full.Violations
                    .OrderByDescending(v => v.ViolationDate).ThenByDescending(v => v.Id)
                    .Select(v => new ViolationRowViewModel(v, canResolve)));
        }
        catch (Exception ex)
        {
            if (requestId == _loadRequestId)
                ErrorMessage = "Sözleşme detayı yüklenirken bir hata oluştu: " + ex.Message;
        }
        finally
        {
            if (requestId == _loadRequestId)
                IsLoading = false;
        }
    }

    // Ekler listesinde "Sil" butonu yalnızca SYB için görünür — sözleşme belgelerinin
    // yönetimi (EditContractAsync/RequestTerminationAsync gibi) zaten SYB'e ait.
    public bool CanDeleteAttachments => _currentUser.Role == UserRole.SYB;

    // Yazdır butonu yalnızca bir sözleşme seçiliyken anlamlı.
    public bool CanPrint => Detail is not null;

    partial void OnDetailChanged(Contract? value) => OnPropertyChanged(nameof(CanPrint));

    // Varsayılan dosya adı: sözleşme no varsa onu, yoksa talep referans numarasını kullanır.
    // Windows'ta geçersiz olan karakterler temizlenir.
    public string SuggestedPdfFileName
    {
        get
        {
            var baseName = Detail is null
                ? "sozlesme"
                : (string.IsNullOrWhiteSpace(Detail.ContractNo) ? Detail.RequestRefNo : Detail.ContractNo!);

            foreach (var invalid in Path.GetInvalidFileNameChars())
                baseName = baseName.Replace(invalid, '_');

            return $"{baseName}.pdf";
        }
    }

    // Dosya seçim penceresi yalnızca code-behind'dan açılabildiği için, "PDF Kaydet"
    // butonunun handler'ı kullanıcının seçtiği yolu buraya iletir. PDF üretimi,
    // denetim kaydı ve dosyanın açılması burada tek yerde yapılır.
    public async Task ExportPdfAsync(string destinationPath)
    {
        if (Detail is null) return;

        ErrorMessage = string.Empty;
        try
        {
            Printing.ContractPdfExporter.Export(Detail, destinationPath);
            await _contractService.LogContractPrintedAsync(Detail, _currentUser);
            Printing.DocumentPrinter.Open(destinationPath);
        }
        catch (Exception ex)
        {
            ErrorMessage = "PDF oluşturulamadı: " + ex.Message;
        }
    }

    // --- İhlalin giderilmesi ---
    //
    // Gerekçe penceresi kod-arkasından açıldığı için akış iki parçalı: pencereden önce
    // bağlam (bu son açık ihlal mi, sözleşme hangi duruma dönecek), sonra kayıt.

    public bool IsLastOpenViolation(ViolationRowViewModel row)
        => Violations.Count(v => !v.IsResolved) <= 1
        && !row.IsResolved;

    // İhlal kapandığında sözleşmenin alacağı durumun ekranda yazılacak karşılığı.
    public string ResolvedStatusText
    {
        get
        {
            if (Detail?.EndDate is not { } end) return "Aktif";

            var today = DateTime.Today;
            if (end.Date < today) return "Tamamlandı";
            return end.Date <= today.AddDays(30) ? "Bitiş Yaklaşıyor" : "Aktif";
        }
    }

    public async Task ResolveViolationAsync(ViolationRowViewModel row, string note)
    {
        if (Detail is null) return;

        ErrorMessage = string.Empty;
        try
        {
            await _contractService.ResolveViolationAsync(Detail, row.RawViolation, _currentUser, note);

            // Sözleşmenin durumu ve rozetleri değişmiş olabilir; detay yeniden yüklenir.
            await LoadDetailAsync(Detail, ++_loadRequestId);
        }
        catch (Exception ex)
        {
            ErrorMessage = "İhlal giderildi olarak işaretlenemedi: " + ex.Message;
        }
    }

    // "Yazdır": belgeyi geçici bir dosyaya üretip doğrudan yazıcıya gönderir.
    // Eskiden bu buton yalnızca PDF'i kaydedip görüntüleyicide açıyordu — kullanıcı
    // yazdırma işlemini oradan elle yapmak zorundaydı, yani buton adını karşılamıyordu.
    [RelayCommand]
    private async Task Print()
    {
        if (Detail is null) return;

        ErrorMessage = string.Empty;
        try
        {
            Printing.DocumentPrinter.PrintContract(Detail, PrintFileNameBase);

            // Yazdırma da sözleşme verisinin uygulama dışına çıkması demek; kaydediliyor.
            await _contractService.LogContractPrintedAsync(Detail, _currentUser);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Yazdırma başlatılamadı: " + ex.Message;
        }
    }

    // Geçici yazdırma dosyasının adı: sözleşme no varsa o, yoksa talep referansı.
    private string PrintFileNameBase => Detail is null
        ? "sozlesme"
        : (string.IsNullOrWhiteSpace(Detail.ContractNo) ? Detail.RequestRefNo : Detail.ContractNo!);

    [RelayCommand]
    private async Task OpenAttachment(Attachment attachment)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(attachment.FilePath) { UseShellExecute = true };
            System.Diagnostics.Process.Start(psi);
            await _contractService.LogAttachmentOpenedAsync(attachment, _currentUser);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Dosya açılamadı: " + ex.Message;
        }
    }

    // Dosya seçim penceresi Avalonia'da yalnızca code-behind'dan (TopLevel üzerinden)
    // açılabildiği için, "İndir" butonunun Click handler'ı kullanıcının seçtiği hedef
    // yolu buraya iletir; kopyalama ve erişim kaydı burada, tek yerde yapılır.
    public async Task DownloadAttachmentAsync(Attachment attachment, string destinationPath)
    {
        try
        {
            File.Copy(attachment.FilePath, destinationPath, overwrite: true);
            await _contractService.LogAttachmentDownloadedAsync(attachment, _currentUser);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Dosya indirilemedi: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteAttachment(Attachment attachment)
    {
        try
        {
            await _contractService.DeleteAttachmentAsync(attachment, _currentUser);
            Attachments.Remove(attachment);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}