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

public partial class ContractWizardViewModel : ViewModelBase
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;
    private readonly string _attachmentsBasePath;

    private readonly List<string> _sozlesmeFilePaths = new();
    private readonly List<string> _ekFilePaths = new();
    private readonly List<string> _teminatFilePaths = new();

    [ObservableProperty]
    public partial int CurrentStep { get; set; } = 1;

    [ObservableProperty]
    public partial ObservableCollection<Contract> PendingRequests { get; set; } = new();

    [ObservableProperty]
    public partial Contract? SelectedRequest { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial bool IsSubmitting { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SuccessMessage { get; set; } = string.Empty;

    public ObservableCollection<ContractItemRowViewModel> Items { get; } = new();
    public ObservableCollection<string> SozlesmeFileNames { get; } = new();
    public ObservableCollection<string> EkFileNames { get; } = new();
    public ObservableCollection<string> TeminatFileNames { get; } = new();

    public decimal Toplam => Items.Sum(i => i.LineTotal);

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
        var all = await _contractService.GetContractsAsync(_currentUser);
        PendingRequests = new ObservableCollection<Contract>(all.Where(c => c.Status == ContractStatus.Talep));
        IsLoading = false;
    }

    [RelayCommand]
    private void AddItem()
    {
        var row = new ContractItemRowViewModel(RemoveItemRow);
        row.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ContractItemRowViewModel.LineTotal))
                OnPropertyChanged(nameof(Toplam));
        };
        Items.Add(row);
        OnPropertyChanged(nameof(Toplam));
    }

    private void RemoveItemRow(ContractItemRowViewModel row)
    {
        Items.Remove(row);
        OnPropertyChanged(nameof(Toplam));
    }

    public void AddFile(string path, AttachmentCategory category)
    {
        switch (category)
        {
            case AttachmentCategory.Sozlesme:
                _sozlesmeFilePaths.Add(path);
                SozlesmeFileNames.Add(Path.GetFileName(path));
                break;
            case AttachmentCategory.Ek:
                _ekFilePaths.Add(path);
                EkFileNames.Add(Path.GetFileName(path));
                break;
            case AttachmentCategory.Teminat:
                _teminatFilePaths.Add(path);
                TeminatFileNames.Add(Path.GetFileName(path));
                break;
        }
    }

    [RelayCommand]
    private void NextStep()
    {
        ErrorMessage = string.Empty;

        if (CurrentStep == 1 && SelectedRequest is null)
        {
            ErrorMessage = "Lütfen bir talep seçin.";
            return;
        }

        if (CurrentStep == 2 && (Items.Count == 0 || Items.Any(i => string.IsNullOrWhiteSpace(i.Description))))
        {
            ErrorMessage = "Lütfen en az bir kalem ekleyin ve açıklamalarını doldurun.";
            return;
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
            var items = Items.Select(r => new ContractItem
            {
                Description = r.Description,
                Quantity = r.Quantity,
                Unit = r.Unit,
                UnitPrice = r.UnitPrice,
            }).ToList();

            var attachments = new List<Attachment>();
            attachments.AddRange(SaveFiles(_sozlesmeFilePaths, AttachmentCategory.Sozlesme));
            attachments.AddRange(SaveFiles(_ekFilePaths, AttachmentCategory.Ek));
            attachments.AddRange(SaveFiles(_teminatFilePaths, AttachmentCategory.Teminat));

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

    private List<Attachment> SaveFiles(List<string> sourcePaths, AttachmentCategory category)
    {
        var result = new List<Attachment>();
        foreach (var path in sourcePaths)
        {
            var savedPath = AttachmentFileHelper.SaveFile(path, _attachmentsBasePath, SelectedRequest!.Id);
            result.Add(new Attachment
            {
                Category = category,
                FileName = Path.GetFileName(path),
                FilePath = savedPath,
                UploadedAt = DateTime.Now,
                UploadedByUserId = _currentUser.Id,
            });
        }
        return result;
    }
}