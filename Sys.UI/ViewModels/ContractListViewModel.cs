using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class ContractListViewModel : ViewModelBase
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;
    private System.Collections.Generic.List<ContractCardViewModel> _allContracts = new();

    [ObservableProperty]
    public partial ObservableCollection<ContractCardViewModel> FilteredContracts { get; set; } = new();

    [ObservableProperty]
    public partial string SelectedFilter { get; set; } = "tumu";

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;
    
    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ContractCardViewModel? SelectedContract { get; set; }

    public ContractListViewModel(ContractService contractService, User currentUser)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var contracts = await _contractService.GetContractsAsync(_currentUser);
            _allContracts = contracts
                .Where(c => c.Status != ContractStatus.Tamamlandi && c.Status != ContractStatus.Feshedildi)
                .Select(c => new ContractCardViewModel(c))
                .ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Sözleşmeler yüklenirken bir hata oluştu: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SetFilter(string filter)
    {
        SelectedFilter = filter;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var items = SelectedFilter == "tumu"
            ? _allContracts
            : _allContracts.Where(c => MatchesFilter(c.Status, SelectedFilter)).ToList();
        FilteredContracts = new ObservableCollection<ContractCardViewModel>(items);
    }

    private static bool MatchesFilter(ContractStatus status, string filter) => filter switch
    {
        "aktif" => status == ContractStatus.Aktif,
        "onay_bekliyor" => status == ContractStatus.OnayBekliyor,
        "uyari" => status == ContractStatus.Uyari,
        "ihlal" => status == ContractStatus.Ihlal,
        "tamamlandi" => status == ContractStatus.Tamamlandi,
        _ => true
    };
}