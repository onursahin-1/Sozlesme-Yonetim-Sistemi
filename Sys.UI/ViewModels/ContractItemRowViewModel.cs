using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace Sys.UI.ViewModels;

public partial class ContractItemRowViewModel : ViewModelBase
{
    private readonly Action<ContractItemRowViewModel> _onRemove;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int Quantity { get; set; } = 1;

    [ObservableProperty]
    public partial string Unit { get; set; } = "adet";

    [ObservableProperty]
    public partial decimal UnitPrice { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;

    public ContractItemRowViewModel(Action<ContractItemRowViewModel> onRemove)
    {
        _onRemove = onRemove;
    }

    partial void OnQuantityChanged(int value) => OnPropertyChanged(nameof(LineTotal));
    partial void OnUnitPriceChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));

    [RelayCommand]
    private void Remove() => _onRemove(this);
}