using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class ContractDetailViewModel : ViewModelBase
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;

    [ObservableProperty]
    public partial ObservableCollection<Contract> AvailableContracts { get; set; } = new();

    [ObservableProperty]
    public partial Contract? SelectedContract { get; set; }

    [ObservableProperty]
    public partial Contract? Detail { get; set; }

    [ObservableProperty]
    public partial string DetailTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailNo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailPeriod { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailRequester { get; set; } = string.Empty;

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
    public partial ObservableCollection<ApprovalLog> ApprovalLogs { get; set; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public ContractDetailViewModel() : this(null!, new User()) { } // yalnızca tasarımcı önizlemesi için

    public ContractDetailViewModel(ContractService contractService, User currentUser)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        _ = LoadListAsync();
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

    partial void OnSelectedContractChanged(Contract? value)
    {
        _ = LoadDetailAsync(value);
    }

    private async Task LoadDetailAsync(Contract? summary)
    {
        ErrorMessage = string.Empty;
        Items = new ObservableCollection<ContractItem>();
        Attachments = new ObservableCollection<Attachment>();
        ApprovalLogs = new ObservableCollection<ApprovalLog>();
        Detail = null;

        if (summary is null) return;

        IsLoading = true;
        try
        {
            var full = await _contractService.GetContractDetailAsync(summary.Id, _currentUser);

            if (full is null)
            {
                ErrorMessage = "Bu sözleşmeyi görüntüleme yetkiniz yok.";
                return;
            }

            Detail = full;
            DetailTitle = full.Title;
            DetailNo = "Sözleşme No: " + (string.IsNullOrEmpty(full.ContractNo) ? full.RequestRefNo : full.ContractNo!);
            DetailPeriod = string.IsNullOrEmpty(full.PaymentPeriod) ? string.Empty : "Ödeme Periyodu: " + full.PaymentPeriod;
            DetailRequester = full.CreatedByUser is null
                ? string.Empty
                : "Talep Eden: " + full.CreatedByUser.FullName + (string.IsNullOrEmpty(full.CreatedByUser.Department) ? "" : $" ({full.CreatedByUser.Department})");
            DetailCompany = "Firma: " + full.CompanyName;
            DetailStatus = "Durum: " + ContractStatusHelper.ToLabel(full.Status);
            DetailTotal = "Toplam Tutar: " + full.TotalAmount.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"));
            DetailStart = "Başlangıç: " + (full.StartDate?.ToString("dd.MM.yyyy") ?? "-");
            DetailEnd = "Bitiş: " + (full.EndDate?.ToString("dd.MM.yyyy") ?? "-");

            Items = new ObservableCollection<ContractItem>(full.Items);
            Attachments = new ObservableCollection<Attachment>(full.Attachments);
            ApprovalLogs = new ObservableCollection<ApprovalLog>(full.ApprovalLogs);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Sözleşme detayı yüklenirken bir hata oluştu: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenAttachment(Attachment attachment)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(attachment.FilePath) { UseShellExecute = true };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Dosya açılamadı: " + ex.Message;
        }
    }
}