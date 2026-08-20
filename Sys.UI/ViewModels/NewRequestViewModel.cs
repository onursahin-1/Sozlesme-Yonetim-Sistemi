using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Infrastructure;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class NewRequestViewModel : ViewModelBase, IEscapeHandler
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;
    private readonly string _attachmentsBasePath;
    private readonly int? _editingContractId;

    [ObservableProperty]
    public partial bool IsEditMode { get; set; }

    // Gönderim sırasında true olur; hem butonun tekrar tıklanmasını engellemek
    // (çift gönderim koruması) hem de kullanıcıya "Gönderiliyor..." geri bildirimi
    // vermek için kullanılır.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubmitButtonText))]
    public partial bool IsBusy { get; set; }

    public string SubmitButtonText => IsBusy ? "Gönderiliyor..." : (IsEditMode ? "Kaydet ve Yeniden Gönder" : "Onaya Gönder");

    public event Action? CancelRequested;

    [RelayCommand]
    private void Cancel()
    {
        CancelRequested?.Invoke();
    }

    // Esc: yalnızca düzenleme modunda (İptal/Geri butonu görünürken) anlamlı.
    // Yeni talep doldururken Esc'in formu kapatması istenmez — girilen veri kaybolurdu.
    public bool CanHandleEscape => IsEditMode;
    public void HandleEscape() => CancelRequested?.Invoke();

    public string[] TypeOptions { get; } = { "Hizmet", "Tedarik", "Eser", "Danışmanlık", "Kira", "Diğer" };

    [ObservableProperty]
    public partial string RequestRefNo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Type { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EstimatedAmountText { get; set; } = string.Empty;

    // Talep aşamasında belirlenen para birimi, sözleşme oluşturulurken sihirbaza taşınır.
    public string[] CurrencyOptions { get; } = CurrencyHelper.Options;

    [ObservableProperty]
    public partial string SelectedCurrency { get; set; } = "TRY";

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CompanyName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TaxNo { get; set; } = string.Empty;
    public string[] CompanyTypeOptions { get; } = { "Yerli Firma", "Yabancı Firma", "Kamu Kurumu" };

    [ObservableProperty]
    public partial string SapCariKodu { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedCompanyType { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? SelectedFilePath { get; set; }

    [ObservableProperty]
    public partial string SelectedFileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SuccessMessage { get; set; } = string.Empty;

    // --- Alan bazlı doğrulama mesajları ---
    // Hatalar eskiden tek bir satırda toplu gösteriliyordu ("Lütfen şu alanları kontrol
    // edin: ..."); uzun formda hangi kutunun sorunlu olduğunu bulmak zordu. Artık her
    // alanın kendi mesajı, kutusunun hemen altında görünüyor. Alttaki ErrorMessage ise
    // yalnızca kısa bir özet olarak kalıyor.

    [ObservableProperty]
    public partial string TitleError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TypeError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AmountError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DescriptionError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CompanyNameError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TaxNoError { get; set; } = string.Empty;

    private bool HasFieldErrors =>
        !string.IsNullOrEmpty(TitleError) ||
        !string.IsNullOrEmpty(TypeError) ||
        !string.IsNullOrEmpty(AmountError) ||
        !string.IsNullOrEmpty(DescriptionError) ||
        !string.IsNullOrEmpty(CompanyNameError) ||
        !string.IsNullOrEmpty(TaxNoError);

    private void ClearFieldErrors()
    {
        TitleError = string.Empty;
        TypeError = string.Empty;
        AmountError = string.Empty;
        DescriptionError = string.Empty;
        CompanyNameError = string.Empty;
        TaxNoError = string.Empty;
    }

    // Kullanıcı hatalı alanı düzeltmeye başladığında mesaj hemen kaybolur; hâlâ kırmızı
    // duran bir uyarıyla uğraşmak zorunda kalmaz. Doğrulama yine gönderimde yapılır.
    partial void OnTitleChanged(string value) => TitleError = string.Empty;
    partial void OnTypeChanged(string value) => TypeError = string.Empty;
    partial void OnEstimatedAmountTextChanged(string value) => AmountError = string.Empty;
    partial void OnDescriptionChanged(string value) => DescriptionError = string.Empty;
    partial void OnCompanyNameChanged(string value) => CompanyNameError = string.Empty;
    partial void OnTaxNoChanged(string value) => TaxNoError = string.Empty;

    public NewRequestViewModel() : this(null!, new User(), string.Empty) { } // yalnızca tasarımcı önizlemesi için

    public NewRequestViewModel(ContractService contractService, User currentUser, string attachmentsBasePath)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        _attachmentsBasePath = attachmentsBasePath;
    }

    public NewRequestViewModel(ContractService contractService, User currentUser, string attachmentsBasePath, Contract editingContract)
        : this(contractService, currentUser, attachmentsBasePath)
    {
        _editingContractId = editingContract.Id;
        IsEditMode = true;
        RequestRefNo = editingContract.RequestRefNo;
        Title = editingContract.Title;
        Type = editingContract.Type;
        EstimatedAmountText = editingContract.TotalAmount == 0
            ? string.Empty
            : editingContract.TotalAmount.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
        Description = editingContract.Description;
        CompanyName = editingContract.CompanyName;
        TaxNo = editingContract.TaxNo;
        SapCariKodu = editingContract.SapCariKodu ?? string.Empty;
        SelectedCompanyType = editingContract.CompanyType ?? string.Empty;
        SelectedCurrency = string.IsNullOrWhiteSpace(editingContract.Currency) ? "TRY" : editingContract.Currency;
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


    [RelayCommand]
    private async Task SubmitAsync()
    {
        // Hızlı çift tıklamada aynı talebin/güncellemenin iki kez gönderilmesini engeller.
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;
            ClearFieldErrors();

            if (string.IsNullOrWhiteSpace(Title)) TitleError = "Konu / başlık zorunludur.";
            if (string.IsNullOrWhiteSpace(Type)) TypeError = "Sözleşme türü seçilmelidir.";
            if (string.IsNullOrWhiteSpace(Description)) DescriptionError = "İşin tanımı zorunludur.";
            if (string.IsNullOrWhiteSpace(CompanyName)) CompanyNameError = "Firma adı zorunludur.";

            if (string.IsNullOrWhiteSpace(TaxNo))
                TaxNoError = "Vergi no zorunludur.";
            else if (TaxNo.Length != 10 || !TaxNo.All(char.IsDigit))
                TaxNoError = "Vergi no 10 haneli ve yalnızca rakamlardan oluşmalıdır.";

            decimal amount = 0;
            if (!string.IsNullOrWhiteSpace(EstimatedAmountText))
            {
                if (!decimal.TryParse(EstimatedAmountText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), out amount))
                    AmountError = "Geçerli bir tutar girin (örn. 12.500,00).";
                else if (amount < 0)
                    AmountError = "Tutar negatif olamaz.";
            }

            if (HasFieldErrors)
            {
                ErrorMessage = "Lütfen işaretli alanları düzeltin.";
                return;
            }

            try
            {
                if (IsEditMode && _editingContractId.HasValue)
                {
                    var editedContract = new Contract
                    {
                        Id = _editingContractId.Value,
                        CreatedByUserId = _currentUser.Id,
                        Status = ContractStatus.Talep,
                        RequestRefNo = RequestRefNo,
                        Title = Title,
                        Type = Type,
                        Description = Description,
                        CompanyName = CompanyName,
                        TaxNo = TaxNo,
                        SapCariKodu = string.IsNullOrWhiteSpace(SapCariKodu) ? null : SapCariKodu,
                        CompanyType = string.IsNullOrWhiteSpace(SelectedCompanyType) ? null : SelectedCompanyType,
                        TotalAmount = amount,
                        Currency = SelectedCurrency,
                    };

                    await _contractService.UpdateRequestAsync(editedContract, _currentUser);

                    if (!string.IsNullOrEmpty(SelectedFilePath))
                    {
                        var savedPath = AttachmentFileHelper.SaveFile(SelectedFilePath, _attachmentsBasePath, editedContract.Id);
                        await _contractService.AddAttachmentAsync(new Attachment
                        {
                            ContractId = editedContract.Id,
                            Category = AttachmentCategory.Talep,
                            FileName = SelectedFileName,
                            FilePath = savedPath,
                            UploadedAt = DateTime.Now,
                            UploadedByUserId = _currentUser.Id,
                        }, _currentUser);
                    }

                    SuccessMessage = "Talep güncellendi ve yeniden gönderildi.";
                    SelectedFilePath = null;
                    SelectedFileName = string.Empty;
                    return;
                }

                var contract = new Contract
                {
                    RequestRefNo = RequestRefNo,
                    Title = Title,
                    Type = Type,
                    Description = Description,
                    CompanyName = CompanyName,
                    TaxNo = TaxNo,
                    SapCariKodu = string.IsNullOrWhiteSpace(SapCariKodu) ? null : SapCariKodu,
                    CompanyType = string.IsNullOrWhiteSpace(SelectedCompanyType) ? null : SelectedCompanyType,
                    TotalAmount = amount,
                    Currency = SelectedCurrency,
                    CreatedByUserId = _currentUser.Id,
                };

                var saved = await _contractService.CreateRequestAsync(contract, _currentUser);

                if (!string.IsNullOrEmpty(SelectedFilePath))
                {
                    var savedPath = AttachmentFileHelper.SaveFile(SelectedFilePath, _attachmentsBasePath, saved.Id);
                    await _contractService.AddAttachmentAsync(new Attachment
                    {
                        ContractId = saved.Id,
                        Category = AttachmentCategory.Talep,
                        FileName = SelectedFileName,
                        FilePath = savedPath,
                        UploadedAt = DateTime.Now,
                        UploadedByUserId = _currentUser.Id,
                    }, _currentUser);
                }

                SuccessMessage = "Talep başarıyla oluşturuldu.";
                Title = string.Empty;
                Type = string.Empty;
                CompanyName = string.Empty;
                TaxNo = string.Empty;
                SapCariKodu = string.Empty;
                SelectedCompanyType = string.Empty;
                Description = string.Empty;
                RequestRefNo = string.Empty;
                EstimatedAmountText = string.Empty;
                SelectedFilePath = null;
                SelectedFileName = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Hata: " + ex.Message;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}