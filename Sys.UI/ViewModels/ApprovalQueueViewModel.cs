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

public partial class ChecklistItemViewModel : ObservableObject
{
    public string Label { get; }
    [ObservableProperty]
    public partial bool IsChecked { get; set; }
    public ChecklistItemViewModel(string label)
    {
        Label = label;
    }
}
public partial class ApprovalQueueViewModel : ViewModelBase
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;
    [ObservableProperty]
    public partial string PageTitle { get; set; } = string.Empty;
    [ObservableProperty]
    public partial ObservableCollection<Contract> PendingContracts { get; set; } = new();
    public ObservableCollection<ChecklistItemViewModel> ChecklistItems { get; } = new();
    public bool IsSybFinalCheck => Detail?.Stage == 1;
    public bool AllChecked => !IsSybFinalCheck || ChecklistItems.All(i => i.IsChecked);
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
    public partial bool IsPendingTermination { get; set; }
    [ObservableProperty]
    public partial string TerminationInfo { get; set; } = string.Empty;
    [ObservableProperty]
    public partial bool HasRevisionHistory { get; set; }
    [ObservableProperty]
    public partial string RevisionInfo { get; set; } = string.Empty;
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
    // "Sözleşmeler" ekranından belirli bir sözleşme için buraya yönlendirildiysek true olur.
    public bool ShowBackButton { get; }
    public event Action? BackRequested;

    // Bir onay/red kararı başarıyla kaydedildiğinde tetiklenir.
    // ShellViewModel bunu dinleyerek "Onay Bekleyenler" rozetini anında tazeler.
    public event Action? DecisionMade;

    [RelayCommand]
    private void Back() => BackRequested?.Invoke();
    public ApprovalQueueViewModel() : this(null!, new User()) { } // tasarımcı önizlemesi için
    public ApprovalQueueViewModel(ContractService contractService, User currentUser, Contract? initialContract = null)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        PageTitle = currentUser.Role == UserRole.Mudur ? "Onay Bekleyenler" : "Son Kontrol (SYB)";
        ShowBackButton = initialContract is not null;
        _ = InitializeAsync(initialContract);
    }
    private async Task InitializeAsync(Contract? initialContract)
    {
        await LoadQueueAsync();
        // "Sözleşmeler" ekranından "Son Kontrol'e Git" butonuyla buraya yönlendirildiysek,
        // ilgili sözleşmeyi kuyruktan bulup otomatik olarak seçili hale getiriyoruz.
        if (initialContract is not null)
        {
            var match = PendingContracts.FirstOrDefault(c => c.Id == initialContract.Id);
            SelectedContract = match ?? initialContract;
        }
    }
    private async Task LoadQueueAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var list = await _contractService.GetPendingApprovalsAsync(_currentUser);
            PendingContracts = new ObservableCollection<Contract>(list);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Liste yüklenirken bir hata oluştu: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
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
        IsPendingTermination = false;
        ChecklistItems.Clear();
        TerminationInfo = string.Empty;
        HasRevisionHistory = false;
        RevisionInfo = string.Empty;
        if (summary is null) return;
        try
        {
            var full = await _contractService.GetContractDetailAsync(summary.Id, _currentUser);
            if (full is null)
            {
                ErrorMessage = "Sözleşme yüklenemedi.";
                return;
            }
            Detail = full;
            if (full.Stage == 1)
            {
                var labels = new[]
                {
                    "Sözleşme dosyası eksiksiz yüklendi",
                    "Bedel kalemleri ve toplam tutar doğru",
                    "SAP Cari Kodu ve Vergi No doğrulandı",
                    "Başlangıç/Bitiş tarihleri ve ödeme periyodu doğru",
                    "Ek belgeler (teminat, ek dosya) kontrol edildi",
                    "Fesih/Revizyon bağlamı gözden geçirildi (varsa yukarıdaki kutuda)",
                };
                foreach (var label in labels)
                {
                    var item = new ChecklistItemViewModel(label);
                    item.PropertyChanged += (_, __) => OnPropertyChanged(nameof(AllChecked));
                    ChecklistItems.Add(item);
                }
            }
            OnPropertyChanged(nameof(IsSybFinalCheck));
            OnPropertyChanged(nameof(AllChecked));
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
            IsPendingTermination = full.PendingTermination;
            if (full.PendingTermination)
            {
                var term = full.Terminations.OrderByDescending(t => t.RequestedAt).FirstOrDefault();
                if (term is not null)
                {
                    var compensation = term.CompensationAmount.HasValue
                        ? term.CompensationAmount.Value.ToString("N2", CultureInfo.GetCultureInfo("tr-TR")) + " TL — " + term.CompensationDirection
                        : "Yok";
                    TerminationInfo = $"Fesih Türü: {term.TerminationType}\nFesih Tarihi: {term.TerminationDate:dd.MM.yyyy}\nGerekçe: {term.Reason}\nTazminat: {compensation}";
                }
            }
            else if (full.Revisions.Count > 0)
            {
                var rev = full.Revisions.OrderByDescending(r => r.ChangedAt).FirstOrDefault();
                if (rev is not null)
                {
                    HasRevisionHistory = true;
                    RevisionInfo = $"Değişiklik Türü: {rev.ChangeType}\nGerekçe: {rev.Reason}\nÖnceki Bedel: {rev.PreviousTotalAmount:N2} TL";
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Sözleşme detayı yüklenirken bir hata oluştu: " + ex.Message;
        }
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
            DecisionMade?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            SuccessMessage = string.Empty;
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