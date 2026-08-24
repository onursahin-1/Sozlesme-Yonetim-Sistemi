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

public partial class ContractEditViewModel : ViewModelBase
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;
    private readonly string _attachmentsBasePath;

    public string[] ChangeTypes { get; } =
    {
        "Bedel Değişikliği",
        "Süre Uzatımı / Kısalması",
        "Kapsam Değişikliği",
        "Firma Bilgisi Güncelleme",
        "Ödeme Koşulları Değişikliği",
        "Diğer"
    };

    [ObservableProperty]
    public partial ObservableCollection<Contract> AvailableContracts { get; set; } = new();

    [ObservableProperty]
    public partial Contract? SelectedContract { get; set; }

    [ObservableProperty]
    public partial string SelectedChangeType { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Reason { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewTotalAmountText { get; set; } = string.Empty;

    // Etiket sabit "Yeni Bedel (TL)" yazıyordu; sözleşme EUR/USD ise yanlış bilgi
    // veriyordu. Seçilen sözleşmenin para birimine göre güncelleniyor.
    [ObservableProperty]
    public partial string NewAmountLabel { get; set; } = "Yeni Bedel";

    [ObservableProperty]
    public partial DateTimeOffset? NewEndDate { get; set; }

    // "Kapsam Değişikliği" / "Firma Bilgisi Güncelleme" / "Ödeme Koşulları Değişikliği"
    // seçildiğinde kullanılan alanlar. Hepsi opsiyoneldir — boş bırakılırsa ilgili
    // alan değişmez.
    [ObservableProperty]
    public partial string NewDescription { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewCompanyName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewTaxNo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPaymentPeriod { get; set; } = string.Empty;

    // --- Seçilen sözleşmenin MEVCUT değerleri ---
    //
    // Ekranda yalnızca "Yeni Bedel", "Yeni Bitiş Tarihi" gibi boş kutular vardı:
    // kullanıcı neyi neyle değiştirdiğini göremeden yazıyordu. Bir bedel revizyonunda
    // mevcut tutarı bilmeden yeni tutar girmek doğrudan hata kaynağı. Bu değerler
    // ilgili kutunun altında ve sağdaki özet kartında gösteriliyor.

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
    public partial string CurrentDescription { get; set; } = "-";

    [ObservableProperty]
    public partial string CurrentCompanyName { get; set; } = "-";

    [ObservableProperty]
    public partial string CurrentTaxNo { get; set; } = "-";

    [ObservableProperty]
    public partial string CurrentPaymentPeriod { get; set; } = "-";

    // --- Alan bazlı doğrulama mesajları ---
    // Diğer formlarda olduğu gibi hata, ilgili kutunun hemen altında gösteriliyor;
    // eskiden hepsi tek bir kırmızı satırda toplanıyordu.

    [ObservableProperty]
    public partial string ContractError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ReasonError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AmountError { get; set; } = string.Empty;

    partial void OnReasonChanged(string value) => ReasonError = string.Empty;
    partial void OnNewTotalAmountTextChanged(string value) => AmountError = string.Empty;

    [ObservableProperty]
    public partial string? SelectedFilePath { get; set; }

    [ObservableProperty]
    public partial string SelectedFileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SuccessMessage { get; set; } = string.Empty;

    // Gönderim sırasında true olur; çift tıklamada aynı düzenleme talebinin
    // iki kez gönderilmesini engeller.
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public ContractEditViewModel() : this(null!, new User(), string.Empty) { } // tasarımcı önizlemesi için

    public ContractEditViewModel(ContractService contractService, User currentUser, string attachmentsBasePath)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        _attachmentsBasePath = attachmentsBasePath;
        SelectedChangeType = ChangeTypes[0];
        _ = LoadAsync();
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

    private async Task LoadAsync()
    {
        ErrorMessage = string.Empty;
        try
        {
            var list = await _contractService.GetEditableContractsAsync(_currentUser);
            AvailableContracts = new ObservableCollection<Contract>(list);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Sözleşmeler yüklenirken bir hata oluştu: " + ex.Message;
        }
    }

    partial void OnSelectedContractChanged(Contract? value)
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        ContractError = string.Empty;
        ReasonError = string.Empty;
        AmountError = string.Empty;
        Reason = string.Empty;
        NewTotalAmountText = string.Empty;
        NewAmountLabel = value is null
            ? "YENİ BEDEL"
            : $"YENİ BEDEL ({CurrencyHelper.Symbol(value.Currency)})";
        NewEndDate = null;
        NewDescription = string.Empty;
        NewCompanyName = string.Empty;
        NewTaxNo = string.Empty;
        NewPaymentPeriod = string.Empty;
        SelectedFilePath = null;
        SelectedFileName = string.Empty;

        var tr = CultureInfo.GetCultureInfo("tr-TR");
        CurrentTitle = value?.Title ?? string.Empty;
        CurrentNo = value is null
            ? string.Empty
            : (string.IsNullOrWhiteSpace(value.ContractNo) ? value.RequestRefNo : value.ContractNo!);
        CurrentCompany = value?.CompanyName ?? string.Empty;
        CurrentAmountText = value is null ? "-" : CurrencyHelper.Format(value.TotalAmount, value.Currency);
        CurrentStartText = value?.StartDate?.ToString("dd.MM.yyyy", tr) ?? "-";
        CurrentEndText = value?.EndDate?.ToString("dd.MM.yyyy", tr) ?? "-";
        CurrentDescription = Dash(value?.Description);
        CurrentCompanyName = Dash(value?.CompanyName);
        CurrentTaxNo = Dash(value?.TaxNo);
        CurrentPaymentPeriod = Dash(value?.PaymentPeriod);
    }

    private static string Dash(string? text) => string.IsNullOrWhiteSpace(text) ? "-" : text!;

    [RelayCommand]
    private async Task Submit()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;

            // Tüm alanlar birlikte doğrulanıp her mesaj kendi alanının altında
            // gösteriliyor; eskiden ilk hatada durulup tek satırda yazılıyordu.
            ContractError = string.Empty;
            ReasonError = string.Empty;
            AmountError = string.Empty;

            if (SelectedContract is null)
                ContractError = "Bir sözleşme seçilmelidir.";

            if (string.IsNullOrWhiteSpace(Reason))
                ReasonError = "Değişiklik gerekçesi zorunludur.";

            decimal? newAmount = null;
            if (!string.IsNullOrWhiteSpace(NewTotalAmountText))
            {
                if (!decimal.TryParse(NewTotalAmountText, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out var parsed) || parsed < 0)
                    AmountError = "Yeni bedel geçerli, negatif olmayan bir sayı olmalı.";
                else
                    newAmount = parsed;
            }

            if (!string.IsNullOrEmpty(ContractError) ||
                !string.IsNullOrEmpty(ReasonError) ||
                !string.IsNullOrEmpty(AmountError))
            {
                ErrorMessage = "Lütfen işaretli alanları düzeltin.";
                return;
            }

            // Buraya gelindiyse ContractError boştur, yani seçim yapılmıştır.
            if (SelectedContract is null) return;

            DateTime? newEnd = NewEndDate?.DateTime;

            try
            {
                var contractId = SelectedContract.Id;
                await _contractService.EditContractAsync(
                    SelectedContract, _currentUser, SelectedChangeType, Reason, newAmount, newEnd,
                    NewDescription, NewCompanyName, NewTaxNo, NewPaymentPeriod);

                if (!string.IsNullOrEmpty(SelectedFilePath))
                {
                    var savedPath = AttachmentFileHelper.SaveFile(SelectedFilePath, _attachmentsBasePath, contractId);
                    await _contractService.AddAttachmentAsync(new Attachment
                    {
                        ContractId = contractId,
                        Category = AttachmentCategory.Ek,
                        FileName = SelectedFileName,
                        FilePath = savedPath,
                        UploadedAt = DateTime.Now,
                        UploadedByUserId = _currentUser.Id,
                    }, _currentUser);
                }

                SuccessMessage = "Değişiklik talebi gönderildi. Sözleşme yeniden onay sürecine alındı.";
                SelectedContract = null;
                Reason = string.Empty;
                NewTotalAmountText = string.Empty;
                NewEndDate = null;
                NewDescription = string.Empty;
                NewCompanyName = string.Empty;
                NewTaxNo = string.Empty;
                NewPaymentPeriod = string.Empty;
                SelectedFilePath = null;
                SelectedFileName = string.Empty;
                await LoadAsync();
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