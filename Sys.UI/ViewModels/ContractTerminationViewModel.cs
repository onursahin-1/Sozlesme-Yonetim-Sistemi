using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Infrastructure;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class ContractTerminationViewModel : ViewModelBase
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;
    private readonly string _attachmentsBasePath;

    // "Haklı Fesih" sözleşmenin ihlal edilmiş olmasına dayanır; sistemdeki ihlal
    // kayıtlarıyla ilişkisi aşağıda kontrol ediliyor.
    private const string HakliFesih = "Haklı Fesih (İhlal Nedeniyle)";

    public string[] TerminationTypes { get; } =
    {
        "Karşılıklı Mutabakat ile Fesih",
        HakliFesih,
        "İhbarlı Fesih",
        "Zorunlu Fesih (Mücbir Sebep)"
    };

    public string[] CompensationDirections { get; } =
    {
        "Tazminat yok",
        "Biz ödüyoruz",
        "Karşı taraf ödüyor"
    };

    [ObservableProperty]
    public partial ObservableCollection<Contract> AvailableContracts { get; set; } = new();

    [ObservableProperty]
    public partial Contract? SelectedContract { get; set; }

    [ObservableProperty]
    public partial string SelectedTerminationType { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTimeOffset? TerminationDate { get; set; }

    [ObservableProperty]
    public partial string Reason { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CompensationAmountText { get; set; } = string.Empty;

    // Tazminat, sözleşmenin para biriminde ödenir; etiket sabit "TL" yazıyordu.
    [ObservableProperty]
    public partial string CompensationLabel { get; set; } = "Fesih Tazminatı";

    // --- Seçilen sözleşmenin MEVCUT künyesi ---
    //
    // Ekranda hiç görünmüyordu. Oysa fesih kararının en belirleyici bilgisi budur:
    // 5 gün kalmış bir sözleşmeyi feshetmekle 2 yıl kalmış olanı feshetmek bambaşka
    // şeyler ve tazminat çoğunlukla kalan süreye bağlı hesaplanıyor.

    [ObservableProperty]
    public partial string CurrentTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentNo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentCompany { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentAmountText { get; set; } = "-";

    [ObservableProperty]
    public partial string CurrentStartText { get; set; } = "-";

    [ObservableProperty]
    public partial string CurrentEndText { get; set; } = "-";

    [ObservableProperty]
    public partial string CurrentRemainingText { get; set; } = "-";

    // --- Alan bazlı doğrulama mesajları ---

    [ObservableProperty]
    public partial string ContractError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DateError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ReasonError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CompensationError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FileError { get; set; } = string.Empty;

    partial void OnReasonChanged(string value) => ReasonError = string.Empty;
    partial void OnTerminationDateChanged(DateTimeOffset? value) => DateError = string.Empty;
    partial void OnCompensationAmountTextChanged(string value) => CompensationError = string.Empty;

    // Tazminat tutarı kutusu, yalnızca bir yön seçilmişken açık olur. Yön "Tazminat yok"
    // iken kutu kapalı ve boş: çelişkili veri (örn. "50.000 TL — Tazminat yok") artık
    // doğrulamayla yakalanmıyor, hiç oluşamıyor.
    public bool IsCompensationEnabled => SelectedCompensationDirection != NoCompensation
                                      && !string.IsNullOrWhiteSpace(SelectedCompensationDirection);

    partial void OnSelectedCompensationDirectionChanged(string value)
    {
        CompensationError = string.Empty;

        if (!IsCompensationEnabled)
            CompensationAmountText = string.Empty;

        OnPropertyChanged(nameof(IsCompensationEnabled));
    }
    partial void OnSelectedFileNameChanged(string value) => FileError = string.Empty;

    partial void OnSelectedContractChanged(Contract? value)
    {
        CompensationLabel = value is null
            ? "FESİH TAZMİNATI"
            : $"FESİH TAZMİNATI ({CurrencyHelper.Symbol(value.Currency)})";

        ContractError = string.Empty;
        SuccessMessage = string.Empty;

        var tr = CultureInfo.GetCultureInfo("tr-TR");
        CurrentTitle = value?.Title ?? string.Empty;
        CurrentNo = value is null
            ? string.Empty
            : (string.IsNullOrWhiteSpace(value.ContractNo) ? value.RequestRefNo : value.ContractNo!);
        CurrentCompany = value?.CompanyName ?? string.Empty;
        CurrentAmountText = value is null ? "-" : CurrencyHelper.Format(value.TotalAmount, value.Currency);
        CurrentStartText = value?.StartDate?.ToString("dd.MM.yyyy", tr) ?? "-";
        CurrentEndText = value?.EndDate?.ToString("dd.MM.yyyy", tr) ?? "-";
        CurrentRemainingText = RemainingText(value);

        ViolationCount = 0;
        ViolationsLoaded = false;
        var token = ++_violationLoadToken;
        _ = LoadViolationCountAsync(value, token);
    }

    private static string RemainingText(Contract? contract)
    {
        if (contract?.EndDate is null) return "-";

        var days = (contract.EndDate.Value.Date - DateTime.Today).Days;
        return days > 0 ? $"{days} gün kaldı" : "Sona erdi";
    }

    // --- Haklı feshin dayanağı ---
    //
    // "Haklı Fesih (İhlal Nedeniyle)" sözleşmenin ihlal edilmiş olmasına dayanır ama
    // sistemde kayıtlı ihlal olup olmadığı hiç kontrol edilmiyordu. Dayanağı olmayan
    // bir haklı fesih hukuki olarak tam tersi sonuç doğurabilir.
    //
    // ENGELLEMİYORUZ, uyarıyoruz: ihlal sistem dışında da (yazışma, tutanak)
    // belgelenmiş olabilir.

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUnsupportedJustCauseWarning))]
    public partial int ViolationCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUnsupportedJustCauseWarning))]
    public partial bool ViolationsLoaded { get; set; }

    public bool ShowUnsupportedJustCauseWarning =>
        ViolationsLoaded && SelectedTerminationType == HakliFesih && ViolationCount == 0;

    partial void OnSelectedTerminationTypeChanged(string value)
        => OnPropertyChanged(nameof(ShowUnsupportedJustCauseWarning));

    // Art arda sözleşme değiştirildiğinde geç dönen eski bir sorgunun yeni seçimin
    // verisini ezmesini engelleyen istek sayacı.
    private int _violationLoadToken;

    private async Task LoadViolationCountAsync(Contract? contract, int token)
    {
        if (contract is null) return;

        try
        {
            var full = await _contractService.GetContractDetailAsync(contract.Id, _currentUser);
            if (token != _violationLoadToken || full is null) return;

            ViolationCount = full.Violations.Count;
            ViolationsLoaded = true;
        }
        catch
        {
            // İhlal sayısı yalnızca bir uyarıyı besliyor; okunamazsa fesih akışı
            // engellenmemeli, uyarı da gösterilmez.
            if (token == _violationLoadToken) ViolationsLoaded = false;
        }
    }

    [ObservableProperty]
    public partial string SelectedCompensationDirection { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? SelectedFilePath { get; set; }

    [ObservableProperty]
    public partial string SelectedFileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SuccessMessage { get; set; } = string.Empty;

    // Gönderim sırasında true olur; çift tıklamada aynı fesih talebinin
    // iki kez gönderilmesini engeller.
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public ContractTerminationViewModel() : this(null!, new User(), string.Empty) { } // tasarımcı önizlemesi için

    public ContractTerminationViewModel(ContractService contractService, User currentUser, string attachmentsBasePath)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        _attachmentsBasePath = attachmentsBasePath;
        SelectedTerminationType = TerminationTypes[0];
        SelectedCompensationDirection = CompensationDirections[0];
        TerminationDate = DateTimeOffset.Now;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        ErrorMessage = string.Empty;
        try
        {
            var list = await _contractService.GetTerminableContractsAsync(_currentUser);
            AvailableContracts = new ObservableCollection<Contract>(list);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Sözleşmeler yüklenirken bir hata oluştu: " + ex.Message;
        }
    }

    public void SetSelectedFile(string path)
    {
        SelectedFilePath = path;
        SelectedFileName = System.IO.Path.GetFileName(path);
    }

    [RelayCommand]
    private void ClearFile()
    {
        SelectedFilePath = null;
        SelectedFileName = string.Empty;
    }

    private const string NoCompensation = "Tazminat yok";

    // Tüm alanlar birlikte doğrulanır ve her mesaj kendi alanının altında gösterilir;
    // eskiden ilk hatada durulup tek bir satırda yazılıyordu.
    private bool ValidateForm(out decimal? compensation)
    {
        compensation = null;
        ErrorMessage = string.Empty;
        ContractError = string.Empty;
        DateError = string.Empty;
        ReasonError = string.Empty;
        CompensationError = string.Empty;
        FileError = string.Empty;

        if (SelectedContract is null)
            ContractError = "Bir sözleşme seçilmelidir.";

        if (string.IsNullOrWhiteSpace(Reason))
            ReasonError = "Fesih gerekçesi zorunludur.";

        if (TerminationDate is null)
        {
            DateError = "Fesih tarihi zorunludur.";
        }
        else if (SelectedContract?.StartDate is { } start && TerminationDate.Value.Date < start.Date)
        {
            // Sözleşme başlamadan feshedilemez. Bu kontrol hiç yoktu; geçmişe dönük
            // herhangi bir tarih kabul ediliyordu.
            DateError = $"Fesih tarihi, sözleşme başlangıcından ({start:dd.MM.yyyy}) önce olamaz.";
        }
        else if (SelectedContract?.EndDate is { } end && TerminationDate.Value.Date > end.Date)
        {
            DateError = $"Fesih tarihi, sözleşme bitişinden ({end:dd.MM.yyyy}) sonra olamaz; sözleşme o tarihte zaten sona eriyor.";
        }

        if (string.IsNullOrEmpty(SelectedFilePath))
            FileError = "Fesih bildirimi / tutanak (PDF) zorunludur.";

        var hasAmountText = !string.IsNullOrWhiteSpace(CompensationAmountText);
        if (hasAmountText)
        {
            if (!decimal.TryParse(CompensationAmountText, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out var parsed) || parsed < 0)
                CompensationError = "Tazminat tutarı geçerli, negatif olmayan bir sayı olmalı.";
            else
                compensation = parsed;
        }

        // Tutar kutusu yön seçilmeden açılmadığı için "tutar var ama yön yok" durumu
        // arayüzde oluşamıyor; geriye yönü seçip tutarı boş bırakma ihtimali kalıyor.
        if (string.IsNullOrEmpty(CompensationError) && IsCompensationEnabled && compensation is null or 0)
            CompensationError = "Tazminat yönü seçildiğinde tutar girilmelidir.";

        // Yön "Tazminat yok" ise tutar kaydedilmez; sıfır da yazılmaz.
        if (!IsCompensationEnabled) compensation = null;

        var valid = string.IsNullOrEmpty(ContractError)
                 && string.IsNullOrEmpty(DateError)
                 && string.IsNullOrEmpty(ReasonError)
                 && string.IsNullOrEmpty(CompensationError)
                 && string.IsNullOrEmpty(FileError);

        if (!valid)
        {
            ErrorMessage = "Lütfen işaretli alanları düzeltin.";
            compensation = null;
        }

        return valid;
    }

    public bool CanSubmit() => ValidateForm(out _);

    [RelayCommand]
    private async Task Submit()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            SuccessMessage = string.Empty;

            if (!ValidateForm(out var compensation))
                return;

            try
            {
                var contractId = SelectedContract!.Id;
                await _contractService.RequestTerminationAsync(SelectedContract, _currentUser, SelectedTerminationType, TerminationDate!.Value.DateTime, Reason, compensation, SelectedCompensationDirection);

                var savedPath = AttachmentFileHelper.SaveFile(SelectedFilePath!, _attachmentsBasePath, contractId);
                await _contractService.AddAttachmentAsync(new Attachment
                {
                    ContractId = contractId,
                    Category = AttachmentCategory.Fesih,
                    FileName = SelectedFileName,
                    FilePath = savedPath,
                    UploadedAt = DateTime.Now,
                    UploadedByUserId = _currentUser.Id,
                }, _currentUser);

                // SelectedContract sıfırlaması OnSelectedContractChanged'i tetikleyip
                // SuccessMessage'ı temizlediği için mesaj ondan SONRA atanıyor.
                SelectedContract = null;
                Reason = string.Empty;
                CompensationAmountText = string.Empty;
                SelectedCompensationDirection = NoCompensation;
                SelectedFilePath = null;
                SelectedFileName = string.Empty;
                TerminationDate = DateTimeOffset.Now;
                await LoadAsync();
                SuccessMessage = "Fesih talebi gönderildi. SYB son kontrolü ve ardından Müdür onayı bekleniyor.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}