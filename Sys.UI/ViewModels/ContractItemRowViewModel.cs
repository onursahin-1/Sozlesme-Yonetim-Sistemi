using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Globalization;

namespace Sys.UI.ViewModels;

public partial class ContractItemRowViewModel : ViewModelBase
{
    private readonly Action<ContractItemRowViewModel> _onRemove;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string QuantityText { get; set; } = "1";

    [ObservableProperty]
    public partial string Unit { get; set; } = "adet";

    [ObservableProperty]
    public partial string UnitPriceText { get; set; } = "0";

    [ObservableProperty]
    public partial string UnitPricePreview { get; set; } = string.Empty;

    public int Quantity => int.TryParse(QuantityText, out var q) ? q : 0;
    public decimal UnitPrice => decimal.TryParse(UnitPriceText, out var p) ? p : 0;
    public decimal LineTotal => Quantity * UnitPrice;

    public ContractItemRowViewModel(Action<ContractItemRowViewModel> onRemove)
    {
        _onRemove = onRemove;
    }

    partial void OnQuantityTextChanged(string value)
    {
        OnPropertyChanged(nameof(Quantity));
        OnPropertyChanged(nameof(LineTotal));
    }

    partial void OnUnitPriceTextChanged(string value)
    {
        OnPropertyChanged(nameof(UnitPrice));
        OnPropertyChanged(nameof(LineTotal));

        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out var amount))
            UnitPricePreview = "→ " + amount.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"));
        else
            UnitPricePreview = string.Empty;
    }

    [RelayCommand]
    private void Remove() => _onRemove(this);
}