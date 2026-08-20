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

    public string DateText => _log.ActionDate.ToString("dd.MM.yyyy HH:mm");
    public string UserText => _log.ActingUser?.FullName ?? ("Kullanıcı #" + _log.ActingUserId);
    public string ActionText => _log.Action switch
    {
        "TalepOluşturuldu" => "Talep Oluşturuldu",
        "TalepGüncellendi" => "Talep Güncellendi",
        "SözleşmeOluşturuldu" => "Sözleşme Oluşturuldu",
        "SözleşmeDüzenlendi" => "Sözleşme Düzenlendi",
        "İhlalBildirildi" => "İhlal Bildirildi",
        "FesihTalebiOluşturuldu" => "Fesih Talebi Oluşturuldu",
        "Onaylandı" => "Onaylandı",
        "Reddedildi" => "Reddedildi",
        "EkGörüntülendi" => "Ek Görüntülendi",
        "Ekİndirildi" => "Ek İndirildi",
        "EkSilindi" => "Ek Silindi",
        _ => _log.Action
    };
    public string DetailText => _log.Detail ?? string.Empty;
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
    public partial ObservableCollection<AuditLogRowViewModel> FilteredLogs { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<string> UserOptions { get; set; } = new();

    [ObservableProperty]
    public partial string SelectedUser { get; set; } = "Tümü";

    [ObservableProperty]
    public partial DateTimeOffset? StartDate { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset? EndDate { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages), nameof(PageInfoText), nameof(CanGoPrevious), nameof(CanGoNext))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand), nameof(PreviousPageCommand))]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages), nameof(PageInfoText), nameof(CanGoPrevious), nameof(CanGoNext))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand), nameof(PreviousPageCommand))]
    public partial int TotalCount { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public string PageInfoText => $"Sayfa {CurrentPage} / {TotalPages} ({TotalCount} kayıt)";
    public bool CanGoPrevious => CurrentPage > 1;
    public bool CanGoNext => CurrentPage < TotalPages;

    partial void OnSelectedUserChanged(string value) => ReloadFromFirstPage();
    partial void OnStartDateChanged(DateTimeOffset? value) => ReloadFromFirstPage();
    partial void OnEndDateChanged(DateTimeOffset? value) => ReloadFromFirstPage();

    [RelayCommand]
    private void ClearDateFilter()
    {
        StartDate = null;
        EndDate = null;
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

            var (items, totalCount) = await _contractService.GetAuditLogsAsync(_currentUser, CurrentPage, PageSize, userFilter, start, end);

            FilteredLogs = new ObservableCollection<AuditLogRowViewModel>(items.Select(l => new AuditLogRowViewModel(l)));
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