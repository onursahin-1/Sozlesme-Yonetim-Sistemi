using System;
using System.Collections.ObjectModel;
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

    [ObservableProperty]
    public partial string? SelectedFilePath { get; set; }

    [ObservableProperty]
    public partial string SelectedFileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SuccessMessage { get; set; } = string.Empty;

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
    private async Task Submit()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        if (SelectedContract is null)
        {
            ErrorMessage = "Önce bir sözleşme seçin.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            ErrorMessage = "Açıklama zorunludur.";
            return;
        }

        if (ViolationDate is null)
        {
            ErrorMessage = "İhlal tarihi zorunludur.";
            return;
        }

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
                });
            }

            SuccessMessage = "İhlal formu SYB'ye iletildi.";
            SelectedContract = null;
            Description = string.Empty;
            SelectedFilePath = null;
            SelectedFileName = string.Empty;
            ViolationDate = DateTimeOffset.Now;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}