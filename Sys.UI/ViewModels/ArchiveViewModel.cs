using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Sys.Domain;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class ArchiveViewModel : ViewModelBase
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
    public partial bool HasTerminationInfo { get; set; }

    [ObservableProperty]
    public partial string TerminationInfo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<ContractItem> Items { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<Attachment> Attachments { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<ApprovalLog> ApprovalLogs { get; set; } = new();

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
        ErrorMessage = string.Empty;
        try
        {
            var contracts = await _contractService.GetArchivedContractsAsync(_currentUser);
            AvailableContracts = new ObservableCollection<Contract>(contracts);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Arşiv yüklenirken bir hata oluştu: " + ex.Message;
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
        HasTerminationInfo = false;
        TerminationInfo = string.Empty;

        if (summary is null) return;

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
            DetailCompany = "Firma: " + full.CompanyName;
            DetailStatus = "Durum: " + full.Status;
            DetailTotal = "Toplam Tutar: " + full.TotalAmount.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"));
            DetailStart = "Başlangıç: " + (full.StartDate?.ToString("dd.MM.yyyy") ?? "-");
            DetailEnd = "Bitiş: " + (full.EndDate?.ToString("dd.MM.yyyy") ?? "-");

            Items = new ObservableCollection<ContractItem>(full.Items);
            Attachments = new ObservableCollection<Attachment>(full.Attachments);
            ApprovalLogs = new ObservableCollection<ApprovalLog>(full.ApprovalLogs);

            if (full.Status == ContractStatus.Feshedildi)
            {
                var term = full.Terminations.OrderByDescending(t => t.RequestedAt).FirstOrDefault();
                if (term is not null)
                {
                    HasTerminationInfo = true;
                    TerminationInfo = $"Fesih Türü: {term.TerminationType}\nFesih Tarihi: {term.TerminationDate:dd.MM.yyyy}\nGerekçe: {term.Reason}";
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Sözleşme detayı yüklenirken bir hata oluştu: " + ex.Message;
        }
    }
}