using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Services;
using Sys.UI.Localization;

namespace Sys.UI.ViewModels;

public class AuditLogRowViewModel
{
    private readonly AuditLog _log;

    public AuditLogRowViewModel(AuditLog log)
    {
        _log = log;
    }

    public string DateText => _log.ActionDate.ToString("dd.MM.yyyy");
    public string TimeText => _log.ActionDate.ToString("HH:mm");
    public string UserText => _log.ActingUser?.FullName ?? Strings.T("Audit.UnknownUser", _log.ActingUserId);

    // Etiket ve renkler tek katalogdan geliyor; buradaki switch son eklenen
    // işlemleri (TalepİadeEdildi, TalepReddedildi, İhlalGiderildi) kaçırıyordu.
    public string ActionText => AuditActionCatalog.Label(_log.Action);
    public string ActionColorHex => AuditActionCatalog.ColorHexFor(_log.Action);
    public string ActionBgHex => AuditActionCatalog.BgHexFor(_log.Action);
    public string ActionStripHex => AuditActionCatalog.StripHexFor(_log.Action);

    public string DetailText => _log.Detail ?? string.Empty;
    public bool HasDetail => !string.IsNullOrWhiteSpace(_log.Detail);

    // Kullanıcı adının baş harfleri; satırın solundaki yuvarlak rozet.
    public string UserInitials
    {
        get
        {
            var name = UserText.Trim();
            if (name.Length == 0) return "?";

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0][..1].ToUpperInvariant();
            return (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
        }
    }

    public DateTime ActionDate => _log.ActionDate;
}

public partial class AuditLogViewModel : ViewModelBase
{
    // Sayfa başına kayıt sayısı tek merkezden (PagingDefaults) gelir; böylece
    // sözleşme listesi vb. diğer sayfalanan ekranlarla her zaman tutarlı kalır.
    private const int PageSize = PagingDefaults.PageSize;

    private readonly ContractService _contractService;
    private readonly User _currentUser;
    private bool _isInitializing = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial ObservableCollection<AuditLogRowViewModel> FilteredLogs { get; set; } = new();


    [ObservableProperty]
    public partial ObservableCollection<FilterOption> UserOptions { get; set; } = new() { AllUsersOption() };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    public partial FilterOption SelectedUser { get; set; } = AllUsersOption();

    private static FilterOption AllUsersOption() => new(null, Strings.T("Common.All"));

    [ObservableProperty]
    public partial DateTimeOffset? StartDate { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset? EndDate { get; set; }

    // İşlem türü filtresi. "Tüm şifre sıfırlamaları" ya da "tüm ek silmeleri" gibi
    // denetim soruları kullanıcı ve tarihle cevaplanamıyordu.
    // Seçenek DEĞERİ ham işlem adı (veritabanındaki kod), ETİKETİ kataloğun
    // çevrilmiş metni. Eskiden etiketten koda geri arama yapılıyordu; etiket
    // çevrilebilir olduğu an bu arama kırılgan hale gelir.
    public ObservableCollection<FilterOption> ActionOptions { get; } = new(
        new[] { AllActionsOption() }
            .Concat(AuditActionCatalog.Actions.Select(a => new FilterOption(a.Key, a.Label))));

    private static FilterOption AllActionsOption() => new(null, Strings.T("Audit.AllActions"));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    public partial FilterOption SelectedAction { get; set; } = AllActionsOption();

    partial void OnSelectedActionChanged(FilterOption value) => ReloadFromFirstPage();

    public bool HasActiveFilters =>
        SelectedUser is { IsAll: false }
        || SelectedAction is { IsAll: false }
        || StartDate is not null
        || EndDate is not null;

    public bool IsEmpty => !IsLoading && FilteredLogs.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages), nameof(PageInfoText), nameof(CanGoPrevious), nameof(CanGoNext))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand), nameof(PreviousPageCommand))]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages), nameof(PageInfoText), nameof(CanGoPrevious), nameof(CanGoNext))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand), nameof(PreviousPageCommand))]
    public partial int TotalCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public string PageInfoText => Strings.T("Audit.PageInfo", CurrentPage, TotalPages, TotalCount);
    public bool CanGoPrevious => CurrentPage > 1;
    public bool CanGoNext => CurrentPage < TotalPages;

    partial void OnSelectedUserChanged(FilterOption value) => ReloadFromFirstPage();
    partial void OnStartDateChanged(DateTimeOffset? value)
    {
        OnPropertyChanged(nameof(HasActiveFilters));
        ReloadFromFirstPage();
    }

    partial void OnEndDateChanged(DateTimeOffset? value)
    {
        OnPropertyChanged(nameof(HasActiveFilters));
        ReloadFromFirstPage();
    }

    // Tek tek temizlemek yerine hepsini birden sıfırlar. Her atama ayrı bir yükleme
    // tetiklemesin diye bayrakla susturulup sonunda tek sorgu atılıyor.
    [RelayCommand]
    private async Task ClearFilters()
    {
        _isInitializing = true;
        SelectedUser = UserOptions.FirstOrDefault(o => o.IsAll) ?? AllUsersOption();
        SelectedAction = ActionOptions.First(o => o.IsAll);
        StartDate = null;
        EndDate = null;
        _isInitializing = false;

        OnPropertyChanged(nameof(HasActiveFilters));
        CurrentPage = 1;
        await LoadPageAsync();
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextPage()
    {
        CurrentPage++;
        await LoadPageAsync();
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private async Task PreviousPage()
    {
        CurrentPage--;
        await LoadPageAsync();
    }

    public AuditLogViewModel() : this(null!, new User()) { } // tasarımcı önizlemesi için

    public AuditLogViewModel(ContractService contractService, User currentUser)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            // Burada "Tümü" SABİT METİN olarak yazılıydı; seçili değer ise
            // sözlükten geliyordu. İngilizce modda liste "Tümü" ile başlıyor ama
            // seçili değer "All" oluyordu — hiçbiriyle eşleşmiyor, kutu boş
            // görünüyordu. Aynı bilginin iki yerde ayrı yazılmasının sonucu.
            var options = new List<FilterOption> { AllUsersOption() };
            options.AddRange((await _contractService.GetAuditLogUserOptionsAsync(_currentUser))
                .Select(u => new FilterOption(u, u)));
            UserOptions = new ObservableCollection<FilterOption>(options);
        }
        catch (Exception ex)
        {
            ErrorMessage = Strings.T("Audit.UserListFailed", ex.Message);
        }
        finally
        {
            _isInitializing = false;
        }

        await LoadPageAsync();
    }

    private void ReloadFromFirstPage()
    {
        if (_isInitializing) return;
        CurrentPage = 1;
        _ = LoadPageAsync();
    }

    // --- Excel'e aktarma ---
    //
    // Ekrandaki filtreler aynen uygulanır ama sayfa değil, eşleşen tüm kayıtlar
    // aktarılır. Denetim çıktısı çoğunlukla dışarıya (iç denetim, mali müşavir)
    // verildiği için aktarmanın kendisi de denetim kaydına yazılıyor.

    public string SuggestedExportFileName => Exporting.ExcelExporter.SuggestFileName("Islem_Gecmisi");

    public async Task ExportToExcelAsync(string destinationPath)
    {
        ErrorMessage = string.Empty;
        try
        {
            var (userFilter, start, end, actionFilter) = CurrentFilters();

            var rows = await _contractService.GetAuditLogsForExportAsync(
                _currentUser, userFilter, start, end, actionFilter);

            Exporting.ExcelExporter.ExportAuditLogs(rows, destinationPath);
            await _contractService.LogExportAsync(_currentUser, Strings.T("Audit.ExportName"), rows.Count);

            if (rows.Count >= ContractService.MaxExportRows)
                ErrorMessage = Strings.T("Audit.ExportCapped", ContractService.MaxExportRows);

            Printing.DocumentPrinter.Open(destinationPath);
        }
        catch (Exception ex)
        {
            ErrorMessage = Strings.T("List.ExportFailed", ex.Message);
        }
    }

    // Sayfalama ve dışa aktarma aynı filtreleri kullanıyor; tek yerden üretiliyor.
    private (string? User, DateTime? Start, DateTime? End, string? Action) CurrentFilters()
    {
        // Sorguya giden değer etiketten değil seçeneğin KENDİ değerinden geliyor;
        // etiketten koda geri arama yapmaya gerek kalmıyor.
        var userFilter = SelectedUser?.Value;
        var actionFilter = SelectedAction?.Value;

        return (userFilter, StartDate?.Date, EndDate?.Date, actionFilter);
    }

    private async Task LoadPageAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var (userFilter, start, end, actionFilter) = CurrentFilters();

            var (items, totalCount) = await _contractService.GetAuditLogsAsync(
                _currentUser, CurrentPage, PageSize, userFilter, start, end, actionFilter);

            FilteredLogs = new ObservableCollection<AuditLogRowViewModel>(
                items.Select(l => new AuditLogRowViewModel(l)));

            TotalCount = totalCount;
        }
        catch (Exception ex)
        {
            ErrorMessage = Strings.T("Audit.LoadFailed", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }
}