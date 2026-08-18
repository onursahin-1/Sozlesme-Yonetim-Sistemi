using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
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
    public partial ObservableCollection<ContractRevision> Revisions { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<ContractTermination> Terminations { get; set; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    // "Sözleşmeler" ekranından belirli bir sözleşme için buraya yönlendirildiysek true olur.
    public bool ShowBackButton { get; }
    public event Action? BackRequested;

    [RelayCommand]
    private void Back() => BackRequested?.Invoke();

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
        Items = new ObservableCollection<ContractItem>();
        Attachments = new ObservableCollection<Attachment>();
        ApprovalLogs = new ObservableCollection<ApprovalLog>();
        Revisions = new ObservableCollection<ContractRevision>();
        Terminations = new ObservableCollection<ContractTermination>();
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
            Revisions = new ObservableCollection<ContractRevision>(full.Revisions);
            Terminations = new ObservableCollection<ContractTermination>(full.Terminations);
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