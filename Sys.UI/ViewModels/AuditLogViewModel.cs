using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Sys.Domain;
using Sys.Services;
using CommunityToolkit.Mvvm.Input;

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
        _ => _log.Action
    };
    public string DetailText => _log.Detail ?? string.Empty;
    public DateTime ActionDate => _log.ActionDate;
}

public partial class AuditLogViewModel : ViewModelBase
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;
    private List<AuditLogRowViewModel> _all = new();

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
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    partial void OnSelectedUserChanged(string value) => ApplyFilter();
    partial void OnStartDateChanged(DateTimeOffset? value) => ApplyFilter();
    partial void OnEndDateChanged(DateTimeOffset? value) => ApplyFilter();

    [RelayCommand]
    private void ClearDateFilter()
    {
        StartDate = null;
        EndDate = null;
    }

    public AuditLogViewModel() : this(null!, new User()) { } // tasarımcı önizlemesi için

    public AuditLogViewModel(ContractService contractService, User currentUser)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var logs = await _contractService.GetAuditLogsAsync(_currentUser);
            _all = logs.Select(l => new AuditLogRowViewModel(l)).ToList();

            var users = new List<string> { "Tümü" };
            users.AddRange(_all.Select(l => l.UserText).Distinct().OrderBy(u => u));
            UserOptions = new ObservableCollection<string>(users);

            ApplyFilter();
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

    private void ApplyFilter()
    {
        IEnumerable<AuditLogRowViewModel> items = _all;

        if (!string.IsNullOrEmpty(SelectedUser) && SelectedUser != "Tümü")
            items = items.Where(l => l.UserText == SelectedUser);

        if (StartDate.HasValue)
            items = items.Where(l => l.ActionDate.Date >= StartDate.Value.Date);

        if (EndDate.HasValue)
            items = items.Where(l => l.ActionDate.Date <= EndDate.Value.Date);

        FilteredLogs = new ObservableCollection<AuditLogRowViewModel>(items);
    }
}