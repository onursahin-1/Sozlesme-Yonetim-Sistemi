using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class ContractWizardViewModel : ViewModelBase
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;

    [ObservableProperty]
    public partial int CurrentStep { get; set; } = 1;

    [ObservableProperty]
    public partial ObservableCollection<Contract> PendingRequests { get; set; } = new();

    [ObservableProperty]
    public partial Contract? SelectedRequest { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public ObservableCollection<ContractItemRowViewModel> Items { get; } = new();

    public decimal Toplam => Items.Sum(i => i.LineTotal);

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;

    public ContractWizardViewModel() : this(null!, new User()) { } // yalnızca tasarımcı önizlemesi için

    public ContractWizardViewModel(ContractService contractService, User currentUser)
    {
        _contractService = contractService;
        _currentUser = currentUser;
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
}