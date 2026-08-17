using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Infrastructure;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class NewRequestViewModel : ViewModelBase
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

    public string[] TypeOptions { get; } = { "Hizmet", "Tedarik", "Eser", "Danışmanlık", "Kira", "Diğer" };

    [ObservableProperty]
    public partial string RequestRefNo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Type { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EstimatedAmountText { get; set; } = string.Empty;

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

            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Title)) errors.Add("Konu / Başlık");
            if (string.IsNullOrWhiteSpace(Type)) errors.Add("Sözleşme Türü");
            if (string.IsNullOrWhiteSpace(Description)) errors.Add("İşin Tanımı");
            if (string.IsNullOrWhiteSpace(CompanyName)) errors.Add("Firma Adı");

            if (string.IsNullOrWhiteSpace(TaxNo))
                errors.Add("Vergi No");
            else if (TaxNo.Length != 10 || !TaxNo.All(char.IsDigit))
                errors.Add("Vergi No (10 haneli rakamdan oluşmalı)");

            decimal amount = 0;
            if (!string.IsNullOrWhiteSpace(EstimatedAmountText))
            {
                if (!decimal.TryParse(EstimatedAmountText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), out amount) || amount < 0)
                    errors.Add("Tahmini Bedel (negatif olamaz)");
            }

            if (errors.Count > 0)
            {
                ErrorMessage = "Lütfen şu alanları kontrol edin: " + string.Join(", ", errors);
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
                        });
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
                    CreatedByUserId = _currentUser.Id,
                };

                var saved = await _contractService.CreateRequestAsync(contract);

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
                    });
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