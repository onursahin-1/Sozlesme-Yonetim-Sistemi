using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class ContractEditViewModel : ViewModelBase
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;

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
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SuccessMessage { get; set; } = string.Empty;

    public ContractEditViewModel() : this(null!, new User()) { } // tasarımcı önizlemesi için

    public ContractEditViewModel(ContractService contractService, User currentUser)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        SelectedChangeType = ChangeTypes[0];
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var list = await _contractService.GetEditableContractsAsync(_currentUser);
        AvailableContracts = new ObservableCollection<Contract>(list);
    }

    partial void OnSelectedContractChanged(Contract? value)
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        Reason = string.Empty;
        NewTotalAmountText = string.Empty;
        NewEndDate = null;
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
            if (!decimal.TryParse(NewTotalAmountText, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out var parsed))
            {
                ErrorMessage = "Yeni bedel geçerli bir sayı olmalı.";
                return;
            }
            newAmount = parsed;
        }

        DateTime? newEnd = NewEndDate?.DateTime;

        try
        {
            await _contractService.EditContractAsync(SelectedContract, _currentUser, SelectedChangeType, Reason, newAmount, newEnd);
            SuccessMessage = "Değişiklik talebi gönderildi. Sözleşme yeniden onay sürecine alındı.";
            SelectedContract = null;
            Reason = string.Empty;
            NewTotalAmountText = string.Empty;
            NewEndDate = null;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}