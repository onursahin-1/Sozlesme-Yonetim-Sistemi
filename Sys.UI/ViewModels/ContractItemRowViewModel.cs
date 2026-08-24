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

    // Birim artık serbest metin değil, listeden seçiliyor: elle yazıldığında herkes
    // farklı yazıyordu ("adet", "Adet", "ad.") ve raporlamada tutarsızlık oluşuyordu.
    public static string[] UnitOptions { get; } =
    {
        "adet", "kg", "litre", "metre", "m²", "m³",
        "saat", "gün", "ay", "yıl", "paket", "kutu", "sefer", "hizmet"
    };

    [ObservableProperty]
    public partial string Unit { get; set; } = "adet";

    [ObservableProperty]
    public partial string UnitPriceText { get; set; } = "0";

    partial void OnUnitPriceTextChanged(string value)
    {
        OnPropertyChanged(nameof(UnitPrice));
        OnPropertyChanged(nameof(LineTotal));
    }

    // Birim fiyat kutusuna yazılan metni AmountFormatHelper tr-TR biçimine sokuyor
    // ("12.348,00"). Ayrıştırma ise makinenin geçerli kültürüne bırakılmıştı: kültür
    // tr-TR değilse (örn. en-US) nokta binlik değil ONDALIK ayraç sayılıyor ve tutar
    // sessizce yanlış okunuyordu. Biçimlendirme ile ayrıştırma artık aynı kültürü
    // kullanıyor.
    private static readonly CultureInfo AmountCulture = CultureInfo.GetCultureInfo("tr-TR");

    public int Quantity => int.TryParse(QuantityText, NumberStyles.Integer, AmountCulture, out var q) ? q : 0;

    public decimal UnitPrice =>
        decimal.TryParse(UnitPriceText, NumberStyles.Any, AmountCulture, out var p) ? p : 0;

    public decimal LineTotal => Quantity * UnitPrice;

    // Kayıtlı bir kalem forma geri yüklenirken kutuya yazılacak metin. Ekrandaki
    // biçimle birebir aynı olsun diye burada üretiliyor.
    public static string FormatAmount(decimal value) => value.ToString("N2", AmountCulture);

    public ContractItemRowViewModel(Action<ContractItemRowViewModel> onRemove)
    {
        _onRemove = onRemove;
    }

    partial void OnQuantityTextChanged(string value)
    {
        OnPropertyChanged(nameof(Quantity));
        OnPropertyChanged(nameof(LineTotal));
    }

     [RelayCommand]
    private void Remove() => _onRemove(this);
}