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

    public string[] TerminationTypes { get; } =
    {
        "Karşılıklı Mutabakat ile Fesih",
        "Haklı Fesih (İhlal Nedeniyle)",
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

    private bool ValidateForm(out decimal? compensation)
    {
        compensation = null;
        ErrorMessage = string.Empty;

        if (SelectedContract is null)
        {
            ErrorMessage = "Önce bir sözleşme seçin.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Reason))
        {
            ErrorMessage = "Fesih gerekçesi zorunludur.";
            return false;
        }

        if (TerminationDate is null)
        {
            ErrorMessage = "Fesih tarihi zorunludur.";
            return false;
        }

        if (string.IsNullOrEmpty(SelectedFilePath))
        {
            ErrorMessage = "Fesih belgesi (PDF) zorunludur.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(CompensationAmountText))
        {
            if (!decimal.TryParse(CompensationAmountText, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out var parsed) || parsed < 0)
            {
                ErrorMessage = "Tazminat tutarı geçerli, negatif olmayan bir sayı olmalı.";
                return false;
            }
            compensation = parsed;
        }

        return true;
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

                SuccessMessage = "Fesih talebi gönderildi. SYB ve Müdür onayı bekleniyor.";
                SelectedContract = null;
                Reason = string.Empty;
                CompensationAmountText = string.Empty;
                SelectedFilePath = null;
                SelectedFileName = string.Empty;
                TerminationDate = DateTimeOffset.Now;
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