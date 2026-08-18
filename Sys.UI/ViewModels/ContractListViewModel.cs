using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Services;
using System.Collections.Generic;

namespace Sys.UI.ViewModels;

public partial class ContractListViewModel : ViewModelBase
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;
    private System.Collections.Generic.List<ContractCardViewModel> _allContracts = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial ObservableCollection<ContractCardViewModel> FilteredContracts { get; set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    public partial string SelectedFilter { get; set; } = "tumu";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    public partial string SearchText { get; set; } = string.Empty;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; set; } = true;

    // Filtre butonlarında hangisinin seçili olduğunu görsel olarak belirtmek ve
    // sonuç bulunamadığında "filtreleri temizle" aksiyonunu göstermek için kullanılır.
    public bool IsEmpty => !IsLoading && FilteredContracts.Count == 0;
    public bool HasActiveFilters => SelectedFilter != "tumu" || !string.IsNullOrWhiteSpace(SearchText);

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ContractCardViewModel? SelectedContract { get; set; }

    public event Action<Contract>? EditRequested;
    public event Action<Contract>? ViewDetailsRequested;
    public event Action<Contract>? ContractCreationRequested;
    public event Action<Contract>? SonKontrolRequested;

    public ContractListViewModel(ContractService contractService, User currentUser)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        _ = LoadAsync();
    }

    [RelayCommand]
    private void EditRequest(ContractCardViewModel card)
    {
        EditRequested?.Invoke(card.RawContract);
    }

    [RelayCommand]
    private void ViewDetails(ContractCardViewModel card)
    {
        ViewDetailsRequested?.Invoke(card.RawContract);
    }

    [RelayCommand]
    private void RequestContractCreation(ContractCardViewModel card)
    {
        ContractCreationRequested?.Invoke(card.RawContract);
    }

    [RelayCommand]
    private void RequestSonKontrol(ContractCardViewModel card)
    {
        SonKontrolRequested?.Invoke(card.RawContract);
    }

    // Ekran açıkken başka bir kullanıcının eklediği/güncellediği sözleşmeleri
    // görebilmek için üstteki "Yenile" butonuna bağlanır.
    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var contracts = await _contractService.GetContractsAsync(_currentUser);
            var editableUserId = _currentUser.Role == UserRole.Personel ? _currentUser.Id : 0;
            var isSyb = _currentUser.Role == UserRole.SYB;
            _allContracts = contracts
                .Where(c => c.Status != ContractStatus.Tamamlandi && c.Status != ContractStatus.Feshedildi)
                .Select(c => new ContractCardViewModel(c, editableUserId, isSyb))
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

    [RelayCommand]
    private void ClearFilters()
    {
        SelectedFilter = "tumu";
        SearchText = string.Empty;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<ContractCardViewModel> items = SelectedFilter == "tumu"
            ? _allContracts
            : _allContracts.Where(c => MatchesFilter(c.Status, SelectedFilter));

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            items = items.Where(c =>
                c.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                c.CompanyName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

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