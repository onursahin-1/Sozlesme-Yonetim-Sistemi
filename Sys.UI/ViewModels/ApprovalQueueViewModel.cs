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
public partial class ApprovalQueueViewModel : ViewModelBase, IEscapeHandler
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

    // Kontrol listesinin ne kadarının işaretlendiği. "Onayla" butonu liste bitmeden
    // pasif kaldığı için, kaç maddenin kaldığı ekranda yazılı olmalı — eskiden buton
    // sebepsizce tıklanamaz görünüyordu.
    private int CheckedCount => ChecklistItems.Count(i => i.IsChecked);
    public string ChecklistProgressText => $"{CheckedCount} / {ChecklistItems.Count} madde";
    public double ChecklistRatio => ChecklistItems.Count == 0
        ? 0
        : (double)CheckedCount / ChecklistItems.Count;

    // "Onayla" neden pasif? Karar panelinde tek satırlık gerekçe.
    public bool ShowChecklistWarning => IsSybFinalCheck && !AllChecked;

    // Son Kontrol aşamasında (Stage 1) SYB'nin karşısına çıkan liste, ONAYLANAN ŞEYE
    // göre değişir. Eskiden tek bir sabit liste vardı: fesih talebi onaylanırken de
    // "Bedel kalemleri ve toplam tutar doğru", "Vergi No ve SAP Cari Kodu doğrulandı"
    // gibi maddeler soruluyordu. Bunlar fesihte anlamsız (kalem girilmiyor, firma
    // bilgileri aylar önce doğrulanmıştı); asıl kontrol edilmesi gerekenler ise —
    // fesih türü, tarihi, tazminatı, bildirim belgesi — hiç sorulmuyordu. Liste
    // körlemesine işaretlenen bir formaliteye dönüşüyordu.
    //
    // Her madde, ekranda GÖRÜLEBİLEN bir bilgiye karşılık gelmelidir.
    private static string BuildChecklistTitle(Contract contract)
    {
        if (contract.PendingTermination) return "FESİH SON KONTROLÜ";
        if (contract.PendingEdit) return "DEĞİŞİKLİK SON KONTROLÜ";
        return "SÖZLEŞME SON KONTROLÜ";
    }

    private static string[] BuildChecklistLabels(Contract contract)
    {
        // Fesih: karar sözleşmenin sonlandırılması. Ekrandaki fesih kutusunda tür,
        // tarih, gerekçe ve tazminat; dosya listesinde "Fesih" kategorili belge var.
        if (contract.PendingTermination)
        {
            return new[]
            {
                "Fesih türü ve gerekçesi sözleşme hükümleriyle uyumlu",
                "Fesih tarihi doğru; kalan süre ve yükümlülükler değerlendirildi",
                "Tazminat tutarı ve yönü (ödenecek / alınacak) doğru",
                "Fesih bildirimi veya yazışma belgesi eklendi",
                "Firmayla açık bakiye ve devam eden ihlal durumu kontrol edildi",
            };
        }

        // Düzenleme: karar mevcut sözleşmenin değiştirilmesi. Ekranda revizyon kutusu
        // (değişiklik türü, gerekçe, önceki bedel) ve güncel künye yan yana duruyor.
        if (contract.PendingEdit)
        {
            return new[]
            {
                "Değişiklik türü ve gerekçesi açık ve yeterli",
                "Yeni değerler, önceki değerlerle karşılaştırıldı",
                "Bedel değişikliği bütçe açısından uygun",
                "Yeni tarih / ödeme koşulları sözleşmeyle tutarlı",
                "Değişikliği destekleyen belge (zeyilname, yazışma) eklendi",
            };
        }

        // Yeni sözleşme: ilk kez yürürlüğe girecek.
        // "Fesih/Revizyon bağlamı gözden geçirildi" maddesi kaldırıldı — bu akışta
        // öyle bir bağlam yok, madde her zaman boşa işaretleniyordu.
        return new[]
        {
            "Kapsam, talebin konusuyla örtüşüyor",
            "Bedel kalemleri ve toplam tutar doğru",
            "Firma bilgileri doğru (Vergi No, SAP Cari Kodu)",
            "Başlangıç/Bitiş tarihleri ve ödeme periyodu doğru",
            "Sözleşme dosyası yüklendi; ek ve teminat belgeleri tam",
        };
    }

    // Onay/red penceresinin metni de karar neye aitse ona göre yazılır. "Bu sözleşmeyi
    // onaylamak istediğinizden emin misiniz?" bir fesih talebinde yanıltıcıydı:
    // onaylanan şey sözleşme değil, sözleşmenin FESHİ. Aynısı düzenleme için de geçerli.
    public string ApproveConfirmMessage
    {
        get
        {
            if (Detail is null) return "Bu kararı onaylamak istediğinizden emin misiniz?";

            if (Detail.PendingTermination)
                return "Bu fesih talebini onaylamak istediğinizden emin misiniz? " +
                       (IsSybFinalCheck
                           ? "Talep yönetim onayına gönderilecek."
                           : "Sözleşme feshedilecek ve arşive alınacak.");

            if (Detail.PendingEdit)
                return "Bu değişiklik talebini onaylamak istediğinizden emin misiniz? " +
                       (IsSybFinalCheck
                           ? "Talep yönetim onayına gönderilecek."
                           : "Yeni değerler sözleşmede kalıcı olacak.");

            return "Bu sözleşmeyi onaylamak istediğinizden emin misiniz? " +
                   (IsSybFinalCheck
                       ? "Sözleşme yönetim onayına gönderilecek."
                       : "Sözleşme yürürlüğe girecek.");
        }
    }

    public string ApproveConfirmButtonText => Detail switch
    {
        { PendingTermination: true } => "Evet, Feshi Onayla",
        { PendingEdit: true } => "Evet, Değişikliği Onayla",
        _ => "Evet, Onayla"
    };

    public string RejectConfirmMessage
    {
        get
        {
            if (Detail is null) return "Bu kararı reddetmek istediğinizden emin misiniz?";

            if (Detail.PendingTermination)
                return "Bu fesih talebini reddetmek istediğinizden emin misiniz? " +
                       "Sözleşme feshedilmeyecek, yürürlükte kalmaya devam edecek.";

            if (Detail.PendingEdit)
                return "Bu değişiklik talebini reddetmek istediğinizden emin misiniz? " +
                       "Sözleşme değişiklik öncesi değerlerine döndürülecek.";

            // Stage 1 reddi talebi başa döndürür, Stage 2 reddi SYB'ye geri gönderir.
            return "Bu sözleşmeyi reddetmek istediğinizden emin misiniz? " +
                   (IsSybFinalCheck
                       ? "Talep, düzeltilmek üzere sahibine geri gönderilecek."
                       : "Sözleşme SYB son kontrolüne geri gönderilecek.");
        }
    }

    public string RejectConfirmButtonText => Detail switch
    {
        { PendingTermination: true } => "Evet, Feshi Reddet",
        { PendingEdit: true } => "Evet, Değişikliği Reddet",
        _ => "Evet, Reddet"
    };

    private void NotifyChecklistChanged()
    {
        OnPropertyChanged(nameof(AllChecked));
        OnPropertyChanged(nameof(ChecklistProgressText));
        OnPropertyChanged(nameof(ChecklistRatio));
        OnPropertyChanged(nameof(ShowChecklistWarning));
    }
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

    // Aşağıdaki dört alan Son Kontrol listesindeki maddeleri doğrulayabilmek için
    // eklendi: "SAP Cari Kodu ve Vergi No doğrulandı" maddesi ekranda bu iki değer
    // hiç görünmediği için körlemesine işaretleniyordu.
    [ObservableProperty]
    public partial string DetailTaxNo { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string DetailSapCariKodu { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string DetailType { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string DetailCompanyType { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string DetailRefNo { get; set; } = string.Empty;

    // Talebin kapsamı: onaycının "ne onaylıyorum" sorusunun cevabı. Ekranda hiç
    // gösterilmiyordu; sözleşmenin kalemleri vardı ama işin tanımı yoktu.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDescription))]
    public partial string DetailDescription { get; set; } = string.Empty;

    public bool HasDescription => !string.IsNullOrWhiteSpace(DetailDescription);
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

    // Bu sözleşme daha önce reddedilip bu kuyruğa GERİ mi döndü? Eskiden bunun hiçbir
    // izi yoktu: Müdür reddedip SYB'ye geri gönderdiğinde ekran ilk incelemeden
    // ayırt edilemiyordu, gerekçe yalnızca detaydaki zaman çizelgesinden bulunabiliyordu.
    // Kontrol listesinin başlığı da bağlama göre değişir; SYB neyi onayladığını
    // listeye bakmadan da görsün.
    [ObservableProperty]
    public partial string ChecklistTitle { get; set; } = "SON KONTROL LİSTESİ";

    [ObservableProperty]
    public partial bool HasPreviousRejection { get; set; }
    [ObservableProperty]
    public partial string PreviousRejectionInfo { get; set; } = string.Empty;
    [ObservableProperty]
    public partial ObservableCollection<ContractItem> Items { get; set; } = new();
    // Ekler kategori rozetiyle gösterilebilsin diye ham Attachment yerine satır
    // görünüm modeli tutuluyor.
    [ObservableProperty]
    public partial ObservableCollection<AttachmentRowViewModel> Attachments { get; set; } = new();
    [ObservableProperty]
    public partial string Note { get; set; } = string.Empty;
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    // Onay/red kararı kaydedilirken true olur. Approve/Reject butonları koda arkası
    // Click olayıyla tetiklendiği için (Command binding değil), platform bunları
    // otomatik devre dışı bırakmaz — bu yüzden çift tıklamada aynı kararın iki kez
    // gönderilmesini burada elle engelliyoruz.
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

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

    public bool CanHandleEscape => ShowBackButton;
    public void HandleEscape() => BackRequested?.Invoke();
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
    // Kullanıcı listede hızlıca birden fazla sözleşmeye art arda tıklarsa, eski bir
    // seçimin sorgusu yeni seçimden SONRA tamamlanabilir. Bu sayaç her seçimde
    // artırılıp yakalanıyor; sonuç geldiğinde hâlâ "en son" istek mi diye kontrol
    // edilip değilse göz ardı ediliyor.
    private int _loadRequestId;

    partial void OnSelectedContractChanged(Contract? value)
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        Note = string.Empty;
        Items = new ObservableCollection<ContractItem>();
        Attachments = new ObservableCollection<AttachmentRowViewModel>();
        DetailDescription = string.Empty;
        Detail = null;
        IsPendingTermination = false;
        ChecklistItems.Clear();
        TerminationInfo = string.Empty;
        HasRevisionHistory = false;
        RevisionInfo = string.Empty;
        HasPreviousRejection = false;
        PreviousRejectionInfo = string.Empty;
        ChecklistTitle = "SON KONTROL LİSTESİ";
        OnPropertyChanged(nameof(IsSybFinalCheck));
        NotifyChecklistChanged();

        var requestId = ++_loadRequestId;
        _ = LoadDetailAsync(value, requestId);
    }
    private async Task LoadDetailAsync(Contract? summary, int requestId)
    {
        if (summary is null) return;
        try
        {
            var full = await _contractService.GetContractDetailAsync(summary.Id, _currentUser);
            if (requestId != _loadRequestId) return; // daha yeni bir seçim yapılmış, bu sonuç artık geçersiz

            if (full is null)
            {
                ErrorMessage = "Sözleşme yüklenemedi.";
                return;
            }
            Detail = full;

            // WasRejected, sözleşme zincirde ileri gittiği anda temizleniyor; hâlâ true
            // ise bu inceleme bir REDDEN SONRAKİ tekrar incelemedir. Gerekçe metni
            // ApprovalLog'dan okunur: adım adı ve tarihi de orada duruyor.
            if (full.WasRejected)
            {
                var lastRejection = full.ApprovalLogs
                    .Where(l => l.Decision == ApprovalDecision.Red)
                    .OrderByDescending(l => l.ActionDate)
                    .ThenByDescending(l => l.Id)
                    .FirstOrDefault();

                if (lastRejection is not null)
                {
                    HasPreviousRejection = true;
                    var gerekce = string.IsNullOrWhiteSpace(lastRejection.Note)
                        ? string.IsNullOrWhiteSpace(full.LastRejectionNote) ? "belirtilmemiş" : full.LastRejectionNote!
                        : lastRejection.Note!;

                    PreviousRejectionInfo =
                        $"Reddeden adım: {lastRejection.StepName}\n" +
                        $"Tarih: {lastRejection.ActionDate:dd.MM.yyyy HH:mm}\n" +
                        $"Gerekçe: {gerekce}";
                }
            }

            if (full.Stage == 1)
            {
                ChecklistTitle = BuildChecklistTitle(full);
                foreach (var label in BuildChecklistLabels(full))
                {
                    var item = new ChecklistItemViewModel(label);
                    item.PropertyChanged += (_, __) => NotifyChecklistChanged();
                    ChecklistItems.Add(item);
                }
            }
            OnPropertyChanged(nameof(IsSybFinalCheck));
            NotifyChecklistChanged();
            // Künye alanları ham değer olarak tutulur; etiket ("FİRMA", "BEDEL" vb.)
            // ekranda ayrı bir TextBlock olarak yazıldığı için buraya ön ek konmuyor.
            var tr = CultureInfo.GetCultureInfo("tr-TR");
            DetailTitle = full.Title;
            DetailNo = string.IsNullOrEmpty(full.ContractNo) ? full.RequestRefNo : full.ContractNo!;
            DetailPeriod = string.IsNullOrWhiteSpace(full.PaymentPeriod) ? "-" : full.PaymentPeriod!;
            DetailRequester = full.CreatedByUser is null
                ? "-"
                : full.CreatedByUser.FullName + (string.IsNullOrWhiteSpace(full.CreatedByUser.Department) ? "" : $" ({full.CreatedByUser.Department})");
            DetailCompany = full.CompanyName;
            DetailTaxNo = string.IsNullOrWhiteSpace(full.TaxNo) ? "-" : full.TaxNo;
            DetailSapCariKodu = string.IsNullOrWhiteSpace(full.SapCariKodu) ? "-" : full.SapCariKodu!;
            DetailType = string.IsNullOrWhiteSpace(full.Type) ? "-" : full.Type;
            DetailCompanyType = string.IsNullOrWhiteSpace(full.CompanyType) ? "-" : full.CompanyType!;
            DetailRefNo = string.IsNullOrWhiteSpace(full.RequestRefNo) ? "-" : full.RequestRefNo;
            DetailDescription = full.Description ?? string.Empty;
            DetailTotal = CurrencyHelper.Format(full.TotalAmount, full.Currency);
            DetailStart = full.StartDate?.ToString("dd.MM.yyyy", tr) ?? "-";
            DetailEnd = full.EndDate?.ToString("dd.MM.yyyy", tr) ?? "-";
            Items = new ObservableCollection<ContractItem>(full.Items);
            Attachments = new ObservableCollection<AttachmentRowViewModel>(
                full.Attachments.Select(a => new AttachmentRowViewModel(a)));
            // Karar bekleyen kayıtlar, sonucu henüz yazılmamış (IsApproved null) olanlardır.
            IsPendingTermination = full.PendingTermination;
            if (full.PendingTermination)
            {
                var term = full.Terminations
                    .Where(t => t.IsApproved is null)
                    .OrderByDescending(t => t.RequestedAt)
                    .ThenByDescending(t => t.Id)
                    .FirstOrDefault();

                if (term is not null)
                {
                    var compensation = term.CompensationAmount.HasValue
                        ? term.CompensationAmount.Value.ToString("N2", CultureInfo.GetCultureInfo("tr-TR")) + " TL — " + term.CompensationDirection
                        : "Yok";
                    TerminationInfo = $"Fesih Türü: {term.TerminationType}\nFesih Tarihi: {term.TerminationDate:dd.MM.yyyy}\nGerekçe: {term.Reason}\nTazminat: {compensation}";
                }
            }
            // ŞARTA DİKKAT: eskiden "Revisions.Count > 0" idi, yani geçmişte BİR KEZ
            // revizyon görmüş her sözleşmede "Bu bir değişiklik onayıdır" kutusu
            // çıkıyordu — sıradan bir ilk onayda bile. Doğru şart, o an bekleyen bir
            // düzenleme talebinin olması.
            else if (full.PendingEdit)
            {
                var rev = full.Revisions
                    .Where(r => r.IsApproved is null)
                    .OrderByDescending(r => r.ChangedAt)
                    .ThenByDescending(r => r.Id)
                    .FirstOrDefault();

                if (rev is not null)
                {
                    HasRevisionHistory = true;
                    RevisionInfo = $"Değişiklik Türü: {rev.ChangeType}\nGerekçe: {rev.Reason}\n" +
                                  $"Önceki Bedel: {CurrencyHelper.Format(rev.PreviousTotalAmount, full.Currency)}";
                }
            }
        }
        catch (Exception ex)
        {
            if (requestId == _loadRequestId)
                ErrorMessage = "Sözleşme detayı yüklenirken bir hata oluştu: " + ex.Message;
        }
    }
    [RelayCommand]
    private async Task Approve() => await SubmitDecisionAsync(ApprovalDecision.Onay);
    [RelayCommand]
    private async Task Reject() => await SubmitDecisionAsync(ApprovalDecision.Red);
    private async Task SubmitDecisionAsync(ApprovalDecision decision)
    {
        if (IsBusy) return; // çift tıklamada aynı kararın iki kez gönderilmesini engeller
        if (Detail is null)
        {
            ErrorMessage = "Önce listeden bir sözleşme seçin.";
            return;
        }
        if (decision == ApprovalDecision.Red && string.IsNullOrWhiteSpace(Note))
        {
            ErrorMessage = "Reddetme işlemi için bir gerekçe girmelisiniz.";
            return;
        }
        IsBusy = true;
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
        finally
        {
            IsBusy = false;
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