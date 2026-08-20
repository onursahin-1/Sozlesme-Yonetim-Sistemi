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

    // Kullanıcı listede hızlıca birden fazla sözleşmeye art arda tıklarsa, eski bir
    // seçimin sorgusu yeni seçimden SONRA tamamlanıp ekrandaki veriyi ezebilir.
    // Diğer ekranlarda kullanılan istek sayacı deseni burada da uygulanıyor.
    private int _loadRequestId;

    partial void OnSelectedContractChanged(Contract? value)
    {
        ErrorMessage = string.Empty;
        Items = new ObservableCollection<ContractItem>();
        Attachments = new ObservableCollection<Attachment>();
        Steps = new ObservableCollection<ContractStepViewModel>();
        Detail = null;
        HasTerminationInfo = false;
        TerminationInfo = string.Empty;

        var requestId = ++_loadRequestId;
        _ = LoadDetailAsync(value, requestId);
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
            DetailTotal = full.TotalAmount.ToString("N2", tr) + " TL";
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
