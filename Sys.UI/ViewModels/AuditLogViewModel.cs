using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Services;

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
    public string UserText => _log.ActingUser?.FullName ?? ("Kullanıcı #" + _log.ActingUserId);

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
    public partial ObservableCollection<string> UserOptions { get; set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    public partial string SelectedUser { get; set; } = "Tümü";

    [ObservableProperty]
    public partial DateTimeOffset? StartDate { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset? EndDate { get; set; }

    // İşlem türü filtresi. "Tüm şifre sıfırlamaları" ya da "tüm ek silmeleri" gibi
    // denetim soruları kullanıcı ve tarihle cevaplanamıyordu.
    // Görünen etiketler kataloğdan; seçim ham işlem adına çevriliyor.
    public ObservableCollection<string> ActionOptions { get; } = new(
        new[] { TumIslemler }.Concat(AuditActionCatalog.Actions.Select(a => a.Label)));

    private const string TumIslemler = "Tüm işlemler";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    public partial string SelectedAction { get; set; } = TumIslemler;

    partial void OnSelectedActionChanged(string value) => ReloadFromFirstPage();

    public bool HasActiveFilters =>
        (SelectedUser is not null && SelectedUser != "Tümü")
        || SelectedAction != TumIslemler
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
    public string PageInfoText => $"Sayfa {CurrentPage} / {TotalPages} ({TotalCount} kayıt)";
    public bool CanGoPrevious => CurrentPage > 1;
    public bool CanGoNext => CurrentPage < TotalPages;

    partial void OnSelectedUserChanged(string value) => ReloadFromFirstPage();
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
        SelectedUser = "Tümü";
        SelectedAction = TumIslemler;
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
            var users = new List<string> { "Tümü" };
            users.AddRange(await _contractService.GetAuditLogUserOptionsAsync(_currentUser));
            UserOptions = new ObservableCollection<string>(users);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Kullanıcı listesi yüklenirken bir hata oluştu: " + ex.Message;
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

    private async Task LoadPageAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            string? userFilter = string.IsNullOrEmpty(SelectedUser) || SelectedUser == "Tümü" ? null : SelectedUser;
            DateTime? start = StartDate?.Date;
            DateTime? end = EndDate?.Date;

            // Açılır listede okunabilir etiket görünüyor; sorguya ham işlem adı gider.
            string? actionFilter = SelectedAction == TumIslemler
                ? null
                : AuditActionCatalog.Actions.FirstOrDefault(a => a.Label == SelectedAction)?.Key;

            var (items, totalCount) = await _contractService.GetAuditLogsAsync(
                _currentUser, CurrentPage, PageSize, userFilter, start, end, actionFilter);

            FilteredLogs = new ObservableCollection<AuditLogRowViewModel>(
                items.Select(l => new AuditLogRowViewModel(l)));

            TotalCount = totalCount;
        }
        catch (Exception ex)
        {
            ErrorMessage = "İşlem geçmişi yüklenirken bir hata oluştu: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}