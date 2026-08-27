using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Infrastructure;
using Sys.Services;

namespace Sys.UI.ViewModels;

public class WizardFileItem
{
    public string FilePath { get; }
    public string FileName { get; }

    public WizardFileItem(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
    }
}

// Sağ paneldeki dikey adım göstergesinin tek bir satırı. Renkler hex metin olarak
// döner (ContractStepViewModel ile aynı desen); böylece üç durumu ayırt etmek için
// ayrı bir converter yazmak gerekmiyor.
public class WizardStepViewModel
{
    public WizardStepViewModel(int number, string label, string hint, int currentStep)
    {
        Number = number;
        Label = label;
        Hint = hint;
        IsDone = currentStep > number;
        IsCurrent = currentStep == number;
    }

    public int Number { get; }
    public string Label { get; }
    public string Hint { get; }
    public bool IsDone { get; }
    public bool IsCurrent { get; }

    public string NumberText => IsDone ? "✓" : Number.ToString();
    // Henüz gelinmemiş adımın dairesi KART YÜZEYİYLE aynı olmalı: yalnızca
    // çerçevesi görünsün, içi zeminle kaynaşsın. Eskiden burada "beyaz" yazıyordu
    // ve açık temada kart da beyaz olduğu için doğru görünüyordu; koyu temada
    // parlayan beyaz daireler çıktı.
    public string CircleBgHex => IsDone ? "SuccessBrightSolid" : IsCurrent ? "AccentSolid" : "SurfaceCard";
    public string CircleBorderHex => IsDone ? "SuccessBright" : IsCurrent ? "AccentBase" : "BorderInput";
    public string NumberColorHex => IsDone || IsCurrent ? "TextOnAccent" : "TextFaintAlt";
    public string LabelColorHex => IsCurrent ? "TextPrimary" : IsDone ? "TextBodyAlt" : "TextFaintAlt";
    public string LabelWeight => IsCurrent ? "Bold" : "Normal";

    // İpucu satırı yalnızca içinde bulunulan adımda gösterilir; diğerleri sade kalsın.
    public bool ShowHint => IsCurrent;
}

public partial class ContractWizardViewModel : ViewModelBase, IEscapeHandler
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;
    private readonly string _attachmentsBasePath;

    [ObservableProperty]
    public partial int CurrentStep { get; set; } = 1;

    [ObservableProperty]
    public partial ObservableCollection<Contract> PendingRequests { get; set; } = new();

    [ObservableProperty]
    public partial Contract? SelectedRequest { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset? StartDate { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset? EndDate { get; set; }

    public string[] PaymentPeriodOptions { get; } = { "Aylık", "Tek Seferlik", "Üç Aylık", "Yıllık", "İş Tamamlandığında" };

    [ObservableProperty]
    public partial string SelectedPaymentPeriod { get; set; } = string.Empty;

    public string[] CompanyTypeOptions { get; } = { "Yerli Firma", "Yabancı Firma", "Kamu Kurumu" };

    [ObservableProperty]
    public partial string SapCariKodu { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedCompanyType { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    public partial bool IsSubmitting { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SuccessMessage { get; set; } = string.Empty;

    // --- 1. adımın alan bazlı doğrulama mesajları ---

    [ObservableProperty]
    public partial string RequestError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StartDateError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EndDateError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PaymentPeriodError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SapCariKoduError { get; set; } = string.Empty;

    private bool HasStepOneErrors =>
        !string.IsNullOrEmpty(RequestError) ||
        !string.IsNullOrEmpty(StartDateError) ||
        !string.IsNullOrEmpty(EndDateError) ||
        !string.IsNullOrEmpty(PaymentPeriodError) ||
        !string.IsNullOrEmpty(SapCariKoduError);

    private void ClearStepOneErrors()
    {
        RequestError = string.Empty;
        StartDateError = string.Empty;
        EndDateError = string.Empty;
        PaymentPeriodError = string.Empty;
        SapCariKoduError = string.Empty;
    }

    // Kullanıcı alanı düzeltmeye başlar başlamaz uyarı kaybolur.
    partial void OnStartDateChanged(DateTimeOffset? value)
    {
        StartDateError = string.Empty;
        EndDateError = string.Empty; // bitiş/başlangıç karşılaştırması da geçersizleşir
    }
    partial void OnEndDateChanged(DateTimeOffset? value) => EndDateError = string.Empty;
    partial void OnSelectedPaymentPeriodChanged(string value) => PaymentPeriodError = string.Empty;
    partial void OnSapCariKoduChanged(string value) => SapCariKoduError = string.Empty;

    // --- Son adımdaki kontrol listesi ---
    //
    // Bu liste YALNIZCA Son Kontrol'ün atlanacağı durumda çıkar: talebi de aynı SYB
    // açtıysa sözleşme doğrudan yönetim onayına gidiyor ve kontrol listesi başka
    // hiçbir yerde sorulmuyor. Talep Personel'den geldiyse liste burada gösterilmez;
    // o akışta Son Kontrol aşaması duruyor ve maddeler orada soruluyor.
    //
    // Maddeler ApprovalQueue'daki "yeni sözleşme" listesiyle AYNI: ikisi de veri
    // girişini doğruluyor. Tek kaynaktan (WizardChecklist) besleniyor ki zamanla
    // ayrışmasınlar.
    public ObservableCollection<ChecklistItemViewModel> Checklist { get; } = new();

    // Talebi açan kişi ile sözleşmeyi oluşturan aynı mı? Servisteki kuralın aynısı;
    // ekranın neyi göstereceğine bu karar veriyor.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowChecklist))]
    public partial bool SkipsFinalCheck { get; set; }

    public bool ShowChecklist => SkipsFinalCheck;

    private int CheckedCount => Checklist.Count(i => i.IsChecked);
    public bool AllChecked => !ShowChecklist || Checklist.All(i => i.IsChecked);
    public string ChecklistProgressText => $"{CheckedCount} / {Checklist.Count} madde";
    public bool ShowChecklistWarning => ShowChecklist && !AllChecked;

    // Gönder butonu iki sebeple pasif olabilir: liste bitmemiştir ya da gönderim
    // sürüyordur. İkisi tek özellikte birleşiyor — XAML'de iki bağlamayı "ve" ile
    // birleştirmenin temiz bir yolu yok ve çift gönderim koruması kaybolmamalı.
    public bool CanSubmit => AllChecked && !IsSubmitting;

    private void BuildChecklist(Contract? request, User currentUser)
    {
        foreach (var item in Checklist) item.PropertyChanged -= OnChecklistItemChanged;
        Checklist.Clear();

        SkipsFinalCheck = request is not null && request.CreatedByUserId == currentUser.Id;
        if (!SkipsFinalCheck) { NotifyChecklistChanged(); return; }

        foreach (var label in WizardChecklist.Labels)
        {
            var item = new ChecklistItemViewModel(label);
            item.PropertyChanged += OnChecklistItemChanged;
            Checklist.Add(item);
        }
        NotifyChecklistChanged();
    }

    private void OnChecklistItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => NotifyChecklistChanged();

    private void NotifyChecklistChanged()
    {
        OnPropertyChanged(nameof(AllChecked));
        OnPropertyChanged(nameof(ChecklistProgressText));
        OnPropertyChanged(nameof(ShowChecklistWarning));
        OnPropertyChanged(nameof(CanSubmit));
    }

    public ObservableCollection<ContractItemRowViewModel> Items { get; } = new();
    public ObservableCollection<WizardFileItem> SozlesmeFileNames { get; } = new();
    public ObservableCollection<WizardFileItem> EkFileNames { get; } = new();
    public ObservableCollection<WizardFileItem> TeminatFileNames { get; } = new();

    // Talebe DAHA ÖNCE yüklenmiş dosyalar. Bu ekran aynı talep için ikinci kez
    // çalıştırılabildiğinden (Son Kontrol reddi sonrası), SYB'nin hangi dosyaların
    // zaten ekli olduğunu görmesi gerekir; aksi halde aynı dosyayı tekrar yükleyip
    // sözleşmeye iki kopya ekliyordu.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExistingAttachments))]
    public partial ObservableCollection<Attachment> ExistingAttachments { get; set; } = new();

    public bool HasExistingAttachments => ExistingAttachments.Count > 0;

    // Bu talep için daha önce bir sözleşme oluşturulmuş mu? (Reddedilip geri dönmüş.)
    [ObservableProperty]
    public partial bool IsRecreate { get; set; }

    [ObservableProperty]
    public partial string RecreateNotice { get; set; } = string.Empty;

    // Para birimi seçenekleri ve seçili değer. Talep aşamasında belirlenen para birimi
    // buraya taşınır (OnSelectedRequestChanged), SYB isterse değiştirebilir.
    public string[] CurrencyOptions { get; } = CurrencyHelper.Options;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToplamText))]
    public partial string SelectedCurrency { get; set; } = "TRY";

    public decimal Toplam => Items.Sum(i => i.LineTotal);
    public string ToplamText => CurrencyHelper.Format(Toplam, SelectedCurrency);

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;

    // "Sözleşmeler" ekranından belirli bir talep için buraya yönlendirildiysek true olur.
    public bool ShowBackButton { get; }
    public event Action? BackRequested;

    [RelayCommand]
    private void Back() => BackRequested?.Invoke();

    public bool CanHandleEscape => ShowBackButton;
    public void HandleEscape() => BackRequested?.Invoke();

    public ContractWizardViewModel() : this(null!, new User(), string.Empty) { } // yalnızca tasarımcı önizlemesi için

    public ContractWizardViewModel(ContractService contractService, User currentUser, string attachmentsBasePath, Contract? initialRequest = null)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        _attachmentsBasePath = attachmentsBasePath;
        ShowBackButton = initialRequest is not null;
        BuildSteps();
        _ = InitializeAsync(initialRequest);
        AddItem();
    }

    private async Task InitializeAsync(Contract? initialRequest)
    {
        await LoadAsync();

        // "Sözleşmeler" ekranından "Sözleşme Yarat" butonuyla buraya yönlendirildiysek,
        // ilgili talebi listeden bulup otomatik olarak seçili hale getiriyoruz.
        if (initialRequest is not null)
        {
            var match = PendingRequests.FirstOrDefault(c => c.Id == initialRequest.Id);
            SelectedRequest = match ?? initialRequest;
        }
    }

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        OnPropertyChanged(nameof(IsStep3));
        BuildSteps();
    }

    // Sağ paneldeki dikey adım göstergesi. Adım değiştikçe yeniden kuruluyor.
    [ObservableProperty]
    public partial ObservableCollection<WizardStepViewModel> Steps { get; set; } = new();

    private void BuildSteps() => Steps = new ObservableCollection<WizardStepViewModel>
    {
        new(1, "Temel Bilgiler", "Talebi seçin, tarihleri ve ödeme koşullarını girin.", CurrentStep),
        new(2, "Bedel Kalemleri", "Sözleşmenin tutarı kalem kalem burada oluşur.", CurrentStep),
        new(3, "Belgeler ve Gönder", "Dosyaları ekleyip sözleşmeyi onaya gönderin.", CurrentStep),
    };

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var pending = await _contractService.GetPendingRequestsAsync(_currentUser);
            PendingRequests = new ObservableCollection<Contract>(pending);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Talepler yüklenirken bir hata oluştu: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void AddItem() => AddItem(null, null, null, null);

    // Boş satır eklemek ve kayıtlı bir kalemi forma geri yüklemek aynı yolu kullanır;
    // böylece LineTotal aboneliği (toplam hesabı) her iki durumda da kurulmuş olur.
    private void AddItem(string? description, int? quantity, string? unit, decimal? unitPrice)
    {
        var row = new ContractItemRowViewModel(RemoveItemRow);

        if (description is not null) row.Description = description;
        if (quantity is not null) row.QuantityText = quantity.Value.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(unit)) row.Unit = unit!;

        // Ekrandaki biçimle aynı olsun diye satırın kendi biçimlendiricisi kullanılıyor;
        // aksi halde geri yüklenen tutar kutuda farklı görünüp yanlış ayrıştırılabilirdi.
        if (unitPrice is not null) row.UnitPriceText = ContractItemRowViewModel.FormatAmount(unitPrice.Value);

        row.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ContractItemRowViewModel.LineTotal))
            {
                OnPropertyChanged(nameof(Toplam));
                OnPropertyChanged(nameof(ToplamText));
            }
        };
        Items.Add(row);
        OnPropertyChanged(nameof(Toplam));
        OnPropertyChanged(nameof(ToplamText));
    }

    private void RemoveItemRow(ContractItemRowViewModel row)
    {
        Items.Remove(row);
        OnPropertyChanged(nameof(Toplam));
        OnPropertyChanged(nameof(ToplamText));
    }

    public void AddFile(string path, AttachmentCategory category)
    {
        var item = new WizardFileItem(path);

        switch (category)
        {
            case AttachmentCategory.Sozlesme:
                SozlesmeFileNames.Add(item);
                break;
            case AttachmentCategory.Ek:
                EkFileNames.Add(item);
                break;
            case AttachmentCategory.Teminat:
                TeminatFileNames.Add(item);
                break;
        }
    }

    [RelayCommand]
    private void RemoveSozlesmeFile(WizardFileItem item) => SozlesmeFileNames.Remove(item);

    [RelayCommand]
    private void RemoveEkFile(WizardFileItem item) => EkFileNames.Remove(item);

    [RelayCommand]
    private void RemoveTeminatFile(WizardFileItem item) => TeminatFileNames.Remove(item);

    // Art arda talep değiştirildiğinde geç dönen eski bir sorgunun yeni seçimin
    // verisini ezmesini engelleyen istek sayacı (diğer ekranlardaki desenle aynı).
    private int _requestLoadToken;

    partial void OnSelectedRequestChanged(Contract? value)
    {
        RequestError = string.Empty;
        SelectedCurrency = string.IsNullOrWhiteSpace(value?.Currency) ? "TRY" : value!.Currency;
        SapCariKodu = value?.SapCariKodu ?? string.Empty;
        SelectedCompanyType = value?.CompanyType ?? string.Empty;

        IsRecreate = false;
        RecreateNotice = string.Empty;
        ExistingAttachments = new ObservableCollection<Attachment>();

        BuildChecklist(value, _currentUser);

        var token = ++_requestLoadToken;
        _ = LoadExistingContractDataAsync(value, token);
    }

    // Reddedilip geri dönmüş bir talepte, önceki denemede girilen kalemler/tarihler
    // veritabanında duruyor. Form eskiden bomboş açılıyordu: SYB her şeyi yeniden
    // yazıyor, kaydettiğinde de kalemler eskilerin üzerine ekleniyordu. Artık önceki
    // veri forma geri yükleniyor ve kayıt sırasında kalemler değiştiriliyor —
    // ekranda görülen ile veritabanındaki aynı oluyor.
    private async Task LoadExistingContractDataAsync(Contract? request, int token)
    {
        if (request is null)
        {
            EnsureAtLeastOneItemRow();
            return;
        }

        try
        {
            var full = await _contractService.GetContractDetailAsync(request.Id, _currentUser);
            if (token != _requestLoadToken) return; // daha yeni bir seçim yapıldı

            if (full is null)
            {
                EnsureAtLeastOneItemRow();
                return;
            }

            if (full.StartDate is { } start) StartDate = new DateTimeOffset(start);
            if (full.EndDate is { } end) EndDate = new DateTimeOffset(end);
            if (!string.IsNullOrWhiteSpace(full.PaymentPeriod)) SelectedPaymentPeriod = full.PaymentPeriod!;

            Items.Clear();
            foreach (var item in full.Items)
                AddItem(item.Description, item.Quantity, item.Unit, item.UnitPrice);

            EnsureAtLeastOneItemRow();

            ExistingAttachments = new ObservableCollection<Attachment>(full.Attachments);

            // "Daha önce sözleşme oluşturulmuş" işareti YALNIZCA kalemlere bakar.
            // Kalem tek bir yerden yazılıyor: sözleşme oluşturma sihirbazından.
            // Yani kalem varsa bu talep gerçekten bir kez sözleşmeye dönüşmüş demektir.
            //
            // Ek dosyası bu soruya cevap VERMEZ: talebi açan kişi talebin kendi
            // belgesini (Category = Talep) eklediğinde de ek oluşuyor. Ek sayısına
            // bakmak iki hataya yol açıyordu — ilk kez işlenen bir talepte "daha önce
            // oluşturulmuş" uyarısı çıkıyor, dahası YENİLEME talebine dosya eklenmişse
            // aşağıdaki else-if hiç çalışmadığı için kaynak sözleşmenin kalemleri
            // forma hiç gelmiyordu.
            //
            // Yüklenen ekler zaten HasExistingAttachments ile ayrıca listeleniyor.
            IsRecreate = full.Items.Count > 0;
            if (IsRecreate)
            {
                RecreateNotice =
                    "Bu talep için daha önce sözleşme oluşturulmuş. Önceki kalemler ve tarihler " +
                    "forma yüklendi; kaydettiğinizde kalemler bu listeyle DEĞİŞTİRİLİR.";
            }
            else if (full.RenewedFromContractId is { } sourceId)
            {
                // Yenileme talebi: kalemleri kaynak sözleşmeden getir. Asıl yazma yükü
                // burada — bir sözleşmede onlarca kalem olabiliyor ve yenilemede
                // genellikle yalnızca birim fiyatlar değişiyor.
                //
                // TARİHLER kasıtlı olarak taşınmıyor: yeni dönemin başlangıç ve bitişi
                // her zaman yeniden belirlenir, eski tarihlerin forma gelmesi yanlışlıkla
                // geçmişe dönük bir sözleşme kaydedilmesine yol açardı.
                await LoadRenewalSourceItemsAsync(sourceId, token);
            }
        }
        catch (Exception ex)
        {
            if (token != _requestLoadToken) return;
            ErrorMessage = "Talebin önceki verileri yüklenemedi: " + ex.Message;
            EnsureAtLeastOneItemRow();
        }
    }

    private async Task LoadRenewalSourceItemsAsync(int sourceId, int token)
    {
        var source = await _contractService.GetContractDetailAsync(sourceId, _currentUser);
        if (token != _requestLoadToken) return;
        if (source is null || source.Items.Count == 0) return;

        Items.Clear();
        foreach (var item in source.Items)
            AddItem(item.Description, item.Quantity, item.Unit, item.UnitPrice);

        if (!string.IsNullOrWhiteSpace(source.PaymentPeriod))
            SelectedPaymentPeriod = source.PaymentPeriod!;

        var sourceNo = string.IsNullOrWhiteSpace(source.ContractNo) ? source.RequestRefNo : source.ContractNo!;
        IsRecreate = true;
        RecreateNotice =
            $"Bu bir yenileme talebi. Kalemler {sourceNo} numaralı önceki sözleşmeden yüklendi — " +
            "birim fiyatları ve miktarları yeni döneme göre kontrol edin. Tarihler kasıtlı olarak boş bırakıldı.";
    }

    private void EnsureAtLeastOneItemRow()
    {
        if (Items.Count == 0) AddItem();
    }

    [RelayCommand]
    private void NextStep()
    {
        ErrorMessage = string.Empty;

        if (CurrentStep == 1)
        {
            // Eskiden ilk hatada durulup tek bir mesaj gösteriliyordu; kullanıcı bir alanı
            // düzeltip tekrar deneyince bir sonraki hatayı görüyordu. Artık tüm alanlar
            // birlikte doğrulanıp her mesaj kendi alanının altında gösteriliyor.
            ClearStepOneErrors();

            if (SelectedRequest is null)
                RequestError = "Bir talep seçilmelidir.";

            if (StartDate is null)
                StartDateError = "Başlangıç tarihi zorunludur.";

            if (EndDate is null)
                EndDateError = "Bitiş tarihi zorunludur.";
            else if (StartDate is not null && EndDate.Value.Date <= StartDate.Value.Date)
                EndDateError = "Bitiş tarihi, başlangıç tarihinden sonra olmalıdır.";

            if (string.IsNullOrEmpty(SelectedPaymentPeriod))
                PaymentPeriodError = "Ödeme periyodu seçilmelidir.";

            if (string.IsNullOrWhiteSpace(SapCariKodu))
                SapCariKoduError = "SAP cari kodu zorunludur.";

            if (HasStepOneErrors)
            {
                ErrorMessage = "Lütfen işaretli alanları düzeltin.";
                return;
            }
        }

        if (CurrentStep == 2)
        {
            if (Items.Count == 0)
            {
                ErrorMessage = "Lütfen en az bir kalem ekleyin.";
                return;
            }

            if (Items.Any(i => string.IsNullOrWhiteSpace(i.Description)))
            {
                ErrorMessage = "Lütfen tüm kalemlerin açıklamasını doldurun.";
                return;
            }

            if (Items.Any(i => i.Quantity <= 0))
            {
                ErrorMessage = "Kalem miktarı 0'dan büyük olmalı.";
                return;
            }

            if (Items.Any(i => i.UnitPrice < 0))
            {
                ErrorMessage = "Birim fiyat negatif olamaz.";
                return;
            }
        }

        if (CurrentStep < 3) CurrentStep++;
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStep > 1) CurrentStep--;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        if (SelectedRequest is null)
        {
            ErrorMessage = "Talep seçilmedi.";
            return;
        }

        // Son Kontrol atlanacaksa liste burada zorunlu: başka hiçbir yerde
        // sorulmayacak, kaydettikten sonra sözleşme doğrudan yönetime gidiyor.
        if (ShowChecklist && !AllChecked)
        {
            ErrorMessage = "Göndermeden önce kontrol listesindeki tüm maddeleri işaretleyin.";
            return;
        }

        IsSubmitting = true;
        try
        {
            SelectedRequest.StartDate = StartDate!.Value.DateTime;
            SelectedRequest.EndDate = EndDate!.Value.DateTime;
            SelectedRequest.PaymentPeriod = SelectedPaymentPeriod;
            SelectedRequest.SapCariKodu = SapCariKodu;
            SelectedRequest.CompanyType = string.IsNullOrWhiteSpace(SelectedCompanyType) ? null : SelectedCompanyType;
            SelectedRequest.Currency = SelectedCurrency;
            var items = Items.Select(r => new ContractItem
            {
                Description = r.Description,
                Quantity = r.Quantity,
                Unit = r.Unit,
                UnitPrice = r.UnitPrice,
            }).ToList();

            var attachments = new List<Attachment>();
            attachments.AddRange(SaveFiles(SozlesmeFileNames, AttachmentCategory.Sozlesme));
            attachments.AddRange(SaveFiles(EkFileNames, AttachmentCategory.Ek));
            attachments.AddRange(SaveFiles(TeminatFileNames, AttachmentCategory.Teminat));

            await _contractService.FinalizeContractAsync(SelectedRequest, items, attachments, _currentUser);

            SuccessMessage = "Sözleşme başarıyla oluşturuldu. Sol menüden 'Sözleşmeler'e bakarak kontrol edebilirsin.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Hata: " + ex.Message;
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private List<Attachment> SaveFiles(IEnumerable<WizardFileItem> files, AttachmentCategory category)
    {
        var result = new List<Attachment>();
        foreach (var file in files)
        {
            var savedPath = AttachmentFileHelper.SaveFile(file.FilePath, _attachmentsBasePath, SelectedRequest!.Id);
            result.Add(new Attachment
            {
                Category = category,
                FileName = file.FileName,
                FilePath = savedPath,
                UploadedAt = DateTime.Now,
                UploadedByUserId = _currentUser.Id,
            });
        }
        return result;
    }
}