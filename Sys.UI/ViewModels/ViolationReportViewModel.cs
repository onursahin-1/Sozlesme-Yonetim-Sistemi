using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Infrastructure;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class ViolationReportViewModel : ViewModelBase
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;
    private readonly string _attachmentsBasePath;

    public string[] ViolationTypes { get; } =
    {
        "Gecikme / Zamanında ifa etmeme",
        "Eksik ifa / Kapsam dışı",
        "Kalite uyumsuzluğu",
        "Sözleşme şartlarına aykırılık",
        "Diğer"
    };

    [ObservableProperty]
    public partial ObservableCollection<Contract> AvailableContracts { get; set; } = new();

    [ObservableProperty]
    public partial Contract? SelectedContract { get; set; }

    [ObservableProperty]
    public partial string SelectedViolationType { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTimeOffset? ViolationDate { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    // --- Seçilen sözleşmenin künyesi ve mevcut ihlalleri ---
    //
    // Ekranda hiç görünmüyordu. Özellikle önceki ihlaller önemli: aynı ihlal ikinci kez
    // bildirilmesin ve bildirimi yapan, sözleşmenin sicilini görerek karar versin.

    [ObservableProperty]
    public partial string CurrentTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentNo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentCompany { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentStartText { get; set; } = "-";

    [ObservableProperty]
    public partial string CurrentEndText { get; set; } = "-";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExistingViolations))]
    public partial ObservableCollection<ViolationRowViewModel> ExistingViolations { get; set; } = new();

    public bool HasExistingViolations => ExistingViolations.Count > 0;

    // --- Alan bazlı doğrulama mesajları ---

    [ObservableProperty]
    public partial string ContractError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DateError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DescriptionError { get; set; } = string.Empty;

    partial void OnDescriptionChanged(string value) => DescriptionError = string.Empty;
    partial void OnViolationDateChanged(DateTimeOffset? value) => DateError = string.Empty;

    [ObservableProperty]
    public partial string? SelectedFilePath { get; set; }

    [ObservableProperty]
    public partial string SelectedFileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SuccessMessage { get; set; } = string.Empty;

    // Gönderim sırasında true olur; çift tıklamada aynı ihlal bildiriminin
    // iki kez gönderilmesini engeller.
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public ViolationReportViewModel() : this(null!, new User(), string.Empty) { } // tasarımcı önizlemesi için

    public ViolationReportViewModel(ContractService contractService, User currentUser, string attachmentsBasePath)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        _attachmentsBasePath = attachmentsBasePath;
        SelectedViolationType = ViolationTypes[0];
        ViolationDate = DateTimeOffset.Now;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        ErrorMessage = string.Empty;
        try
        {
            var list = await _contractService.GetViolationReportableContractsAsync(_currentUser);
            AvailableContracts = new ObservableCollection<Contract>(list);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Sözleşmeler yüklenirken bir hata oluştu: " + ex.Message;
        }
    }

    // Art arda sözleşme değiştirildiğinde geç dönen eski bir sorgunun yeni seçimin
    // verisini ezmesini engelleyen istek sayacı.
    private int _detailLoadToken;

    partial void OnSelectedContractChanged(Contract? value)
    {
        ContractError = string.Empty;
        SuccessMessage = string.Empty;

        var tr = CultureInfo.GetCultureInfo("tr-TR");
        CurrentTitle = value?.Title ?? string.Empty;
        CurrentNo = value is null
            ? string.Empty
            : (string.IsNullOrWhiteSpace(value.ContractNo) ? value.RequestRefNo : value.ContractNo!);
        CurrentCompany = value?.CompanyName ?? string.Empty;
        CurrentStartText = value?.StartDate?.ToString("dd.MM.yyyy", tr) ?? "-";
        CurrentEndText = value?.EndDate?.ToString("dd.MM.yyyy", tr) ?? "-";
        ExistingViolations = new ObservableCollection<ViolationRowViewModel>();

        var token = ++_detailLoadToken;
        _ = LoadExistingViolationsAsync(value, token);
    }

    private async Task LoadExistingViolationsAsync(Contract? contract, int token)
    {
        if (contract is null) return;

        try
        {
            var full = await _contractService.GetContractDetailAsync(contract.Id, _currentUser);
            if (token != _detailLoadToken || full is null) return;

            // Açık ihlaller üstte: bu ekranda önemli olan sözleşmede halen çözülmemiş
            // bir sorun olup olmadığı.
            ExistingViolations = new ObservableCollection<ViolationRowViewModel>(
                full.Violations
                    .OrderBy(v => v.IsResolved)
                    .ThenByDescending(v => v.ViolationDate).ThenByDescending(v => v.Id)
                    .Select(v => new ViolationRowViewModel(v)));
        }
        catch (Exception ex)
        {
            if (token == _detailLoadToken)
                ErrorMessage = "Sözleşmenin ihlal geçmişi yüklenemedi: " + ex.Message;
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

    // Tüm alanlar birlikte doğrulanır ve her mesaj kendi alanının altında gösterilir.
    private bool ValidateForm()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        ContractError = string.Empty;
        DateError = string.Empty;
        DescriptionError = string.Empty;

        if (SelectedContract is null)
            ContractError = "Bir sözleşme seçilmelidir.";

        if (string.IsNullOrWhiteSpace(Description))
            DescriptionError = "Açıklama zorunludur.";

        if (ViolationDate is null)
        {
            DateError = "İhlal tarihi zorunludur.";
        }
        // Tarih hiç doğrulanmıyordu: gelecek tarihli ya da sözleşme başlamadan önceki
        // bir ihlal kabul ediliyordu. İkisi de mantıksız — ihlal yaşanmış bir olaydır.
        else if (ViolationDate.Value.Date > DateTime.Today)
        {
            DateError = "İhlal tarihi gelecekte olamaz.";
        }
        else if (SelectedContract?.StartDate is { } start && ViolationDate.Value.Date < start.Date)
        {
            DateError = $"İhlal tarihi, sözleşme başlangıcından ({start:dd.MM.yyyy}) önce olamaz.";
        }

        var valid = string.IsNullOrEmpty(ContractError)
                 && string.IsNullOrEmpty(DateError)
                 && string.IsNullOrEmpty(DescriptionError);

        if (!valid) ErrorMessage = "Lütfen işaretli alanları düzeltin.";
        return valid;
    }

    // Onay penceresi kod-arkasından açıldığı için doğrulama dışarıdan da çağrılabilmeli.
    public bool CanSubmit() => ValidateForm();

    [RelayCommand]
    private async Task Submit()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            if (!ValidateForm()) return;

            // ValidateForm bunların dolu olduğunu garanti ediyor; derleyici bunu
            // göremediği için açıkça kontrol ediliyor.
            if (SelectedContract is null || ViolationDate is null) return;

            try
            {
                var contractId = SelectedContract.Id;
                await _contractService.ReportViolationAsync(SelectedContract, _currentUser, SelectedViolationType, ViolationDate.Value.DateTime, Description);

                if (!string.IsNullOrEmpty(SelectedFilePath))
                {
                    var savedPath = AttachmentFileHelper.SaveFile(SelectedFilePath, _attachmentsBasePath, contractId);
                    await _contractService.AddAttachmentAsync(new Attachment
                    {
                        ContractId = contractId,
                        Category = AttachmentCategory.Ihlal,
                        FileName = SelectedFileName,
                        FilePath = savedPath,
                        UploadedAt = DateTime.Now,
                        UploadedByUserId = _currentUser.Id,
                    }, _currentUser);
                }

                // SelectedContract sıfırlaması OnSelectedContractChanged'i tetikleyip
                // SuccessMessage'ı temizlediği için mesaj ondan SONRA atanıyor.
                SelectedContract = null;
                Description = string.Empty;
                SelectedFilePath = null;
                SelectedFileName = string.Empty;
                ViolationDate = DateTimeOffset.Now;
                await LoadAsync();
                SuccessMessage = "İhlal bildirimi kaydedildi. Sözleşme \"İhlal Mevcut\" durumuna geçti ve SYB'ye bildirim gönderildi.";
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