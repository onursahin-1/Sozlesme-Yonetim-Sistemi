using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Infrastructure;
using Sys.Services;

namespace Sys.UI.ViewModels;

public class WizardFileItem
{
    public string FilePath { get; }
    public string FileName { get; }

    public WizardFileItem(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
    }
}

public partial class ContractWizardViewModel : ViewModelBase
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;
    private readonly string _attachmentsBasePath;

    [ObservableProperty]
    public partial int CurrentStep { get; set; } = 1;

    [ObservableProperty]
    public partial ObservableCollection<Contract> PendingRequests { get; set; } = new();

    [ObservableProperty]
    public partial Contract? SelectedRequest { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset? StartDate { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset? EndDate { get; set; }

    public string[] PaymentPeriodOptions { get; } = { "Aylık", "Tek Seferlik", "Üç Aylık", "Yıllık", "İş Tamamlandığında" };

    [ObservableProperty]
    public partial string SelectedPaymentPeriod { get; set; } = string.Empty;

    public string[] CompanyTypeOptions { get; } = { "Yerli Firma", "Yabancı Firma", "Kamu Kurumu" };

    [ObservableProperty]
    public partial string SapCariKodu { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedCompanyType { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial bool IsSubmitting { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SuccessMessage { get; set; } = string.Empty;

    public ObservableCollection<ContractItemRowViewModel> Items { get; } = new();
    public ObservableCollection<WizardFileItem> SozlesmeFileNames { get; } = new();
    public ObservableCollection<WizardFileItem> EkFileNames { get; } = new();
    public ObservableCollection<WizardFileItem> TeminatFileNames { get; } = new();

    public decimal Toplam => Items.Sum(i => i.LineTotal);
    public string ToplamText => Toplam.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("tr-TR")) + " TL";

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;

    public ContractWizardViewModel() : this(null!, new User(), string.Empty) { } // yalnızca tasarımcı önizlemesi için

    public ContractWizardViewModel(ContractService contractService, User currentUser, string attachmentsBasePath)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        _attachmentsBasePath = attachmentsBasePath;
        _ = LoadAsync();
        AddItem();
    }

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        OnPropertyChanged(nameof(IsStep3));
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var all = await _contractService.GetContractsAsync(_currentUser);
            PendingRequests = new ObservableCollection<Contract>(all.Where(c => c.Status == ContractStatus.Talep));
        }
        catch (Exception ex)
        {
            ErrorMessage = "Talepler yüklenirken bir hata oluştu: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void AddItem()
    {
        var row = new ContractItemRowViewModel(RemoveItemRow);
        row.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ContractItemRowViewModel.LineTotal))
            {
                OnPropertyChanged(nameof(Toplam));
                OnPropertyChanged(nameof(ToplamText));
            }
        };
        Items.Add(row);
        OnPropertyChanged(nameof(Toplam));
        OnPropertyChanged(nameof(ToplamText));
    }

    private void RemoveItemRow(ContractItemRowViewModel row)
    {
        Items.Remove(row);
        OnPropertyChanged(nameof(Toplam));
        OnPropertyChanged(nameof(ToplamText));
    }

    public void AddFile(string path, AttachmentCategory category)
    {
        var item = new WizardFileItem(path);

        switch (category)
        {
            case AttachmentCategory.Sozlesme:
                SozlesmeFileNames.Add(item);
                break;
            case AttachmentCategory.Ek:
                EkFileNames.Add(item);
                break;
            case AttachmentCategory.Teminat:
                TeminatFileNames.Add(item);
                break;
        }
    }

    [RelayCommand]
    private void RemoveSozlesmeFile(WizardFileItem item) => SozlesmeFileNames.Remove(item);

    [RelayCommand]
    private void RemoveEkFile(WizardFileItem item) => EkFileNames.Remove(item);

    [RelayCommand]
    private void RemoveTeminatFile(WizardFileItem item) => TeminatFileNames.Remove(item);

    partial void OnSelectedRequestChanged(Contract? value)
    {
        SapCariKodu = value?.SapCariKodu ?? string.Empty;
        SelectedCompanyType = value?.CompanyType ?? string.Empty;
    }

    [RelayCommand]
    private void NextStep()
    {
        ErrorMessage = string.Empty;

        if (CurrentStep == 1)
        {
            if (SelectedRequest is null)
            {
                ErrorMessage = "Lütfen bir talep seçin.";
                return;
            }

            if (StartDate is null)
            {
                ErrorMessage = "Başlangıç tarihi zorunludur.";
                return;
            }

            if (EndDate is null)
            {
                ErrorMessage = "Bitiş tarihi zorunludur.";
                return;
            }

            if (EndDate.Value.Date <= StartDate.Value.Date)
            {
                ErrorMessage = "Bitiş tarihi, başlangıç tarihinden sonra olmalı.";
                return;
            }
            if (string.IsNullOrEmpty(SelectedPaymentPeriod))
            {
                ErrorMessage = "Ödeme periyodu seçilmelidir.";
                return;
            }
            if (string.IsNullOrWhiteSpace(SapCariKodu))
            {
                ErrorMessage = "SAP Cari Kodu zorunludur.";
                return;
            }
        }

        if (CurrentStep == 2)
        {
            if (Items.Count == 0)
            {
                ErrorMessage = "Lütfen en az bir kalem ekleyin.";
                return;
            }

            if (Items.Any(i => string.IsNullOrWhiteSpace(i.Description)))
            {
                ErrorMessage = "Lütfen tüm kalemlerin açıklamasını doldurun.";
                return;
            }

            if (Items.Any(i => i.Quantity <= 0))
            {
                ErrorMessage = "Kalem miktarı 0'dan büyük olmalı.";
                return;
            }

            if (Items.Any(i => i.UnitPrice < 0))
            {
                ErrorMessage = "Birim fiyat negatif olamaz.";
                return;
            }
        }

        if (CurrentStep < 3) CurrentStep++;
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStep > 1) CurrentStep--;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        if (SelectedRequest is null)
        {
            ErrorMessage = "Talep seçilmedi.";
            return;
        }

        IsSubmitting = true;
        try
        {
            SelectedRequest.StartDate = StartDate!.Value.DateTime;
            SelectedRequest.EndDate = EndDate!.Value.DateTime;
            SelectedRequest.PaymentPeriod = SelectedPaymentPeriod;
            SelectedRequest.SapCariKodu = SapCariKodu;
            SelectedRequest.CompanyType = string.IsNullOrWhiteSpace(SelectedCompanyType) ? null : SelectedCompanyType;
            var items = Items.Select(r => new ContractItem
            {
                Description = r.Description,
                Quantity = r.Quantity,
                Unit = r.Unit,
                UnitPrice = r.UnitPrice,
            }).ToList();

            var attachments = new List<Attachment>();
            attachments.AddRange(SaveFiles(SozlesmeFileNames, AttachmentCategory.Sozlesme));
            attachments.AddRange(SaveFiles(EkFileNames, AttachmentCategory.Ek));
            attachments.AddRange(SaveFiles(TeminatFileNames, AttachmentCategory.Teminat));

            await _contractService.FinalizeContractAsync(SelectedRequest, items, attachments, _currentUser);

            SuccessMessage = "Sözleşme başarıyla oluşturuldu. Sol menüden 'Sözleşmeler'e bakarak kontrol edebilirsin.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Hata: " + ex.Message;
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private List<Attachment> SaveFiles(IEnumerable<WizardFileItem> files, AttachmentCategory category)
    {
        var result = new List<Attachment>();
        foreach (var file in files)
        {
            var savedPath = AttachmentFileHelper.SaveFile(file.FilePath, _attachmentsBasePath, SelectedRequest!.Id);
            result.Add(new Attachment
            {
                Category = category,
                FileName = file.FileName,
                FilePath = savedPath,
                UploadedAt = DateTime.Now,
                UploadedByUserId = _currentUser.Id,
            });
        }
        return result;
    }
}