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
    
    [ObservableProperty]
    public partial DateTimeOffset? NewEndDate { get; set; }

    [ObservableProperty]
    public partial string? SelectedFilePath { get; set; }

    [ObservableProperty]
    public partial string SelectedFileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SuccessMessage { get; set; } = string.Empty;

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
        Reason = string.Empty;
        NewTotalAmountText = string.Empty;
        NewEndDate = null;
        SelectedFilePath = null;
        SelectedFileName = string.Empty;
    }

    [RelayCommand]
    private async Task Submit()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        if (SelectedContract is null)
        {
            ErrorMessage = "Önce bir sözleşme seçin.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Reason))
        {
            ErrorMessage = "Değişiklik gerekçesi zorunludur.";
            return;
        }

        decimal? newAmount = null;
        if (!string.IsNullOrWhiteSpace(NewTotalAmountText))
        {
            if (!decimal.TryParse(NewTotalAmountText, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out var parsed) || parsed < 0)
            {
                ErrorMessage = "Yeni bedel geçerli, negatif olmayan bir sayı olmalı.";
                return;
            }
            newAmount = parsed;
        }

        DateTime? newEnd = NewEndDate?.DateTime;

        try
        {
            var contractId = SelectedContract.Id;
            await _contractService.EditContractAsync(SelectedContract, _currentUser, SelectedChangeType, Reason, newAmount, newEnd);

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
                });
            }

            SuccessMessage = "Değişiklik talebi gönderildi. Sözleşme yeniden onay sürecine alındı.";
            SelectedContract = null;
            Reason = string.Empty;
            NewTotalAmountText = string.Empty;
            NewEndDate = null;
            SelectedFilePath = null;
            SelectedFileName = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}