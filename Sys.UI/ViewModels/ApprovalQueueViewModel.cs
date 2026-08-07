using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class ApprovalQueueViewModel : ViewModelBase
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;

    [ObservableProperty]
    public partial string PageTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<Contract> PendingContracts { get; set; } = new();

    [ObservableProperty]
    public partial Contract? SelectedContract { get; set; }

    [ObservableProperty]
    public partial Contract? Detail { get; set; }

    [ObservableProperty]
    public partial string DetailTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailCompany { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailTotal { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailStart { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailEnd { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<ContractItem> Items { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<Attachment> Attachments { get; set; } = new();

    [ObservableProperty]
    public partial string Note { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SuccessMessage { get; set; } = string.Empty;

    public ApprovalQueueViewModel() : this(null!, new User()) { } // tasarımcı önizlemesi için

    public ApprovalQueueViewModel(ContractService contractService, User currentUser)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        PageTitle = currentUser.Role == UserRole.Mudur ? "Onay Bekleyenler" : "Son Kontrol (SYB)";
        _ = LoadQueueAsync();
    }

    private async Task LoadQueueAsync()
    {
        IsLoading = true;
        var list = await _contractService.GetPendingApprovalsAsync(_currentUser);
        PendingContracts = new ObservableCollection<Contract>(list);
        IsLoading = false;
    }

    partial void OnSelectedContractChanged(Contract? value)
    {
        _ = LoadDetailAsync(value);
    }

    private async Task LoadDetailAsync(Contract? summary)
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        Note = string.Empty;
        Items = new ObservableCollection<ContractItem>();
        Attachments = new ObservableCollection<Attachment>();
        Detail = null;

        if (summary is null) return;

        var full = await _contractService.GetContractDetailAsync(summary.Id, _currentUser);
        if (full is null)
        {
            ErrorMessage = "Sözleşme yüklenemedi.";
            return;
        }

        Detail = full;
        DetailTitle = full.Title;
        DetailCompany = "Firma: " + full.CompanyName;
        DetailStatus = "Durum: " + full.Status;
        DetailTotal = "Toplam Tutar: " + full.TotalAmount.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"));
        DetailStart = "Başlangıç: " + (full.StartDate?.ToString("dd.MM.yyyy") ?? "-");
        DetailEnd = "Bitiş: " + (full.EndDate?.ToString("dd.MM.yyyy") ?? "-");

        Items = new ObservableCollection<ContractItem>(full.Items);
        Attachments = new ObservableCollection<Attachment>(full.Attachments);
    }

    [RelayCommand]
    private async Task Approve() => await SubmitDecisionAsync(ApprovalDecision.Onay);

    [RelayCommand]
    private async Task Reject() => await SubmitDecisionAsync(ApprovalDecision.Red);

    private async Task SubmitDecisionAsync(ApprovalDecision decision)
    {
        if (Detail is null)
        {
            ErrorMessage = "Önce listeden bir sözleşme seçin.";
            return;
        }

        try
        {
            await _contractService.DecideApprovalAsync(Detail, _currentUser, decision, string.IsNullOrWhiteSpace(Note) ? null : Note);
            SuccessMessage = decision == ApprovalDecision.Onay ? "Sözleşme onaylandı." : "Sözleşme reddedildi.";
            ErrorMessage = string.Empty;
            SelectedContract = null;
            await LoadQueueAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            SuccessMessage = string.Empty;
        }
    }
}