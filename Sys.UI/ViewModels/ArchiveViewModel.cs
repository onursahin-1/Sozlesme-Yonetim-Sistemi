using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class ArchiveViewModel : ViewModelBase
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;

    // Sayfa başına kayıt sayısı diğer sayfalanan ekranlarla aynı merkezden gelir.
    private const int PageSize = PagingDefaults.PageSize;

    // Arama kutusuna her harfte sorgu atmamak için kısa bekleme (sözleşme listesiyle
    // aynı desen) ve geç dönen eski sorguların yenisini ezmesini engelleyen sayaç.
    private CancellationTokenSource? _searchDebounceCts;
    private int _loadToken;

    // Liste artık ham Contract değil kart görünüm modeli tutuyor: arşivde hangi kaydın
    // neden orada olduğu (süresi doldu / feshedildi / reddedildi) rozetten okunabilmeli.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial ObservableCollection<ContractCardViewModel> AvailableContracts { get; set; } = new();

    [ObservableProperty]
    public partial ContractCardViewModel? SelectedContract { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    public partial string SelectedFilter { get; set; } = "tumu";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    public partial string SearchText { get; set; } = string.Empty;

    partial void OnSearchTextChanged(string value) => DebouncedReloadFirstPage();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; set; } = true;

    // --- Sayfalama durumu ---

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageInfoText))]
    [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageInfoText))]
    [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(ShowPager))]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    public partial int TotalCount { get; set; }

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool CanGoPrevious => CurrentPage > 1;
    public bool CanGoNext => CurrentPage < TotalPages;
    public bool ShowPager => TotalCount > PageSize;
    public string PageInfoText => $"{CurrentPage} / {TotalPages}  ·  {TotalCount} kayıt";

    public bool IsEmpty => !IsLoading && AvailableContracts.Count == 0;
    public bool HasActiveFilters => SelectedFilter != "tumu" || !string.IsNullOrWhiteSpace(SearchText);

    [ObservableProperty]
    public partial Contract? Detail { get; set; }

    // Künye alanları ham değer olarak tutulur; etiket ("FİRMA", "BEDEL" vb.) ekranda
    // ayrı yazıldığı için ön ek konmuyor. Sözleşme detay ekranıyla aynı desen.

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
    public partial string DetailStart { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailEnd { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailRequester { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailDepartment { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailPeriod { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailStatusColorHex { get; set; } = "#555555";

    [ObservableProperty]
    public partial string DetailStatusBgHex { get; set; } = "#EAECF0";

    [ObservableProperty]
    public partial bool HasTerminationInfo { get; set; }

    [ObservableProperty]
    public partial string TerminationInfo { get; set; } = string.Empty;

    // Feshedilen sözleşmenin fesih bilgisi kutusunun karşılığı: reddedilerek kapatılan
    // talebin neden kapandığı da arşivde okunabilmeli.
    [ObservableProperty]
    public partial bool HasRejectionInfo { get; set; }

    [ObservableProperty]
    public partial string RejectionInfo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<ContractItem> Items { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<Attachment> Attachments { get; set; } = new();

    // Süreç zaman çizelgesi — sözleşme detay ekranıyla aynı bileşen. Ayrı bir
    // "Aşama Geçmişi" listesi tutulmuyor; ikisi aynı ApprovalLog kayıtlarından
    // besleniyor ve yan yana durduklarında çelişkili görünüyorlardı.
    [ObservableProperty]
    public partial ObservableCollection<ContractStepViewModel> Steps { get; set; } = new();

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public ArchiveViewModel() : this(null!, new User()) { } // tasarımcı önizlemesi için

    public ArchiveViewModel(ContractService contractService, User currentUser)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        _ = LoadListAsync();
    }

    private async Task LoadListAsync()
    {
        var token = ++_loadToken;
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var (contracts, totalCount) = await _contractService.GetArchivedContractsPagedAsync(
                _currentUser, SelectedFilter, SearchText, CurrentPage, PageSize);

            if (token != _loadToken) return; // daha yeni bir sorgu başlatıldı

            TotalCount = totalCount;
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;

            AvailableContracts = new ObservableCollection<ContractCardViewModel>(
                contracts.Select(c => new ContractCardViewModel(c)));
        }
        catch (Exception ex)
        {
            if (token != _loadToken) return;
            ErrorMessage = "Arşiv yüklenirken bir hata oluştu: " + ex.Message;
        }
        finally
        {
            if (token == _loadToken) IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SetFilter(string filter)
    {
        SelectedFilter = filter;
        CurrentPage = 1;
        await LoadListAsync();
    }

    [RelayCommand]
    private async Task ClearFilters()
    {
        // SearchText ataması OnSearchTextChanged üzerinden ikinci bir yükleme
        // tetiklemesin diye bekleyen debounce önce iptal edilir.
        _searchDebounceCts?.Cancel();
        SelectedFilter = "tumu";
        SearchText = string.Empty;
        _searchDebounceCts?.Cancel();
        CurrentPage = 1;
        await LoadListAsync();
    }

    [RelayCommand]
    private async Task Refresh() => await LoadListAsync();

    [RelayCommand]
    private async Task NextPage()
    {
        if (!CanGoNext) return;
        CurrentPage++;
        await LoadListAsync();
    }

    [RelayCommand]
    private async Task PreviousPage()
    {
        if (!CanGoPrevious) return;
        CurrentPage--;
        await LoadListAsync();
    }

    private void DebouncedReloadFirstPage()
    {
        _searchDebounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchDebounceCts = cts;
        _ = DelayThenReloadAsync(cts.Token);
    }

    private async Task DelayThenReloadAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token);
        }
        catch (OperationCanceledException)
        {
            return; // kullanıcı yazmaya devam etti
        }

        if (token.IsCancellationRequested) return;
        CurrentPage = 1;
        await LoadListAsync();
    }

    // Kullanıcı listede hızlıca birden fazla sözleşmeye art arda tıklarsa, eski bir
    // seçimin sorgusu yeni seçimden SONRA tamamlanıp ekrandaki veriyi ezebilir.
    // Diğer ekranlarda kullanılan istek sayacı deseni burada da uygulanıyor.
    private int _loadRequestId;

    partial void OnSelectedContractChanged(ContractCardViewModel? value)
    {
        ErrorMessage = string.Empty;
        Items = new ObservableCollection<ContractItem>();
        Attachments = new ObservableCollection<Attachment>();
        Steps = new ObservableCollection<ContractStepViewModel>();
        Detail = null;
        HasTerminationInfo = false;
        TerminationInfo = string.Empty;
        HasRejectionInfo = false;
        RejectionInfo = string.Empty;

        var requestId = ++_loadRequestId;
        _ = LoadDetailAsync(value?.RawContract, requestId);
    }

    private async Task LoadDetailAsync(Contract? summary, int requestId)
    {
        if (summary is null) return;

        try
        {
            var full = await _contractService.GetContractDetailAsync(summary.Id, _currentUser);
            if (requestId != _loadRequestId) return; // daha yeni bir seçim yapıldı

            if (full is null)
            {
                ErrorMessage = "Bu sözleşmeyi görüntüleme yetkiniz yok.";
                return;
            }

            var tr = CultureInfo.GetCultureInfo("tr-TR");
            var card = new ContractCardViewModel(full);

            Detail = full;
            DetailTitle = full.Title;
            DetailNo = string.IsNullOrEmpty(full.ContractNo) ? full.RequestRefNo : full.ContractNo!;
            DetailCompany = full.CompanyName;
            DetailSapCariKodu = string.IsNullOrWhiteSpace(full.SapCariKodu) ? "-" : full.SapCariKodu!;
            DetailTaxNo = string.IsNullOrWhiteSpace(full.TaxNo) ? "-" : full.TaxNo;
            DetailType = string.IsNullOrWhiteSpace(full.Type) ? "-" : full.Type;
            DetailTotal = CurrencyHelper.Format(full.TotalAmount, full.Currency);
            DetailStart = full.StartDate?.ToString("dd.MM.yyyy", tr) ?? "-";
            DetailEnd = full.EndDate?.ToString("dd.MM.yyyy", tr) ?? "-";
            DetailRequester = full.CreatedByUser?.FullName ?? "-";
            DetailDepartment = string.IsNullOrWhiteSpace(full.CreatedByUser?.Department) ? "-" : full.CreatedByUser!.Department!;
            DetailPeriod = string.IsNullOrWhiteSpace(full.PaymentPeriod) ? "-" : full.PaymentPeriod!;

            DetailStatus = card.StatusLabel;
            DetailStatusColorHex = card.StatusColorHex;
            DetailStatusBgHex = card.StatusBgHex;

            Items = new ObservableCollection<ContractItem>(full.Items);
            Attachments = new ObservableCollection<Attachment>(full.Attachments);
            Steps = new ObservableCollection<ContractStepViewModel>(ContractStepViewModel.Build(full));

            if (full.Status == ContractStatus.Feshedildi)
            {
                var term = full.Terminations.OrderByDescending(t => t.RequestedAt).FirstOrDefault();
                if (term is not null)
                {
                    HasTerminationInfo = true;
                    TerminationInfo =
                        $"Fesih Türü: {term.TerminationType}\n" +
                        $"Fesih Tarihi: {term.TerminationDate.ToString("dd.MM.yyyy", tr)}\n" +
                        $"Gerekçe: {term.Reason}";
                }
            }
            else if (full.Status == ContractStatus.Reddedildi)
            {
                HasRejectionInfo = true;
                var tarih = full.LastRejectedAt?.ToString("dd.MM.yyyy HH:mm", tr) ?? "-";
                var gerekce = string.IsNullOrWhiteSpace(full.LastRejectionNote)
                    ? "belirtilmemiş"
                    : full.LastRejectionNote!;

                RejectionInfo =
                    $"Red Tarihi: {tarih}\n" +
                    $"Gerekçe: {gerekce}\n" +
                    "Bu talep sözleşmeye dönüşmeden kapatılmıştır.";
            }
        }
        catch (Exception ex)
        {
            if (requestId == _loadRequestId)
                ErrorMessage = "Sözleşme detayı yüklenirken bir hata oluştu: " + ex.Message;
        }
    }

    // Arşivdeki sözleşmeler salt okunur; yalnızca açma ve PDF çıktısı alma var.
    public bool CanPrint => Detail is not null;

    partial void OnDetailChanged(Contract? value) => OnPropertyChanged(nameof(CanPrint));

    public string SuggestedPdfFileName
    {
        get
        {
            var baseName = Detail is null
                ? "sozlesme"
                : (string.IsNullOrWhiteSpace(Detail.ContractNo) ? Detail.RequestRefNo : Detail.ContractNo!);

            foreach (var invalid in System.IO.Path.GetInvalidFileNameChars())
                baseName = baseName.Replace(invalid, '_');

            return $"{baseName}.pdf";
        }
    }

    public async Task ExportPdfAsync(string destinationPath)
    {
        if (Detail is null) return;

        ErrorMessage = string.Empty;
        try
        {
            Printing.ContractPdfExporter.Export(Detail, destinationPath);
            await _contractService.LogContractPrintedAsync(Detail, _currentUser);

            var psi = new System.Diagnostics.ProcessStartInfo(destinationPath) { UseShellExecute = true };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            ErrorMessage = "PDF oluşturulamadı: " + ex.Message;
        }
    }

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
}
