using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Infrastructure;
using Sys.Services;
using System.Globalization;

namespace Sys.UI.ViewModels;

public partial class NewRequestViewModel : ViewModelBase
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;
    private readonly string _attachmentsBasePath;

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
    public partial string EstimatedAmountPreview { get; set; } = string.Empty;

    partial void OnEstimatedAmountTextChanged(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out var amount))
            EstimatedAmountPreview = "→ " + amount.ToString("N2", CultureInfo.GetCultureInfo("tr-TR")) + " TL";
        else
            EstimatedAmountPreview = string.Empty;
    }

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CompanyName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TaxNo { get; set; } = string.Empty;

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

    public void SetSelectedFile(string path)
    {
        SelectedFilePath = path;
        SelectedFileName = System.IO.Path.GetFileName(path);
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(CompanyName) ||
        string.IsNullOrWhiteSpace(TaxNo) || string.IsNullOrWhiteSpace(Description) ||
        string.IsNullOrWhiteSpace(Type))
        {
            ErrorMessage = "Lütfen zorunlu alanları (Başlık, Sözleşme Türü, Firma, Vergi No, İşin Tanımı) doldurun.";
            return;
        }

        if (TaxNo.Length != 10 || !TaxNo.All(char.IsDigit))
        {
            ErrorMessage = "Vergi No 10 haneli rakamlardan oluşmalıdır.";
            return;
        }

        decimal.TryParse(EstimatedAmountText, out var amount);

        var contract = new Contract
        {
            RequestRefNo = RequestRefNo,
            Title = Title,
            Type = Type,
            Description = Description,
            CompanyName = CompanyName,
            TaxNo = TaxNo,
            TotalAmount = amount,
            CreatedByUserId = _currentUser.Id,
        };

        try
        {
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
            CompanyName = string.Empty;
            TaxNo = string.Empty;
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
}