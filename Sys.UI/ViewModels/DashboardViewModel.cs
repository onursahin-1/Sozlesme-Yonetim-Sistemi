using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Sys.Domain;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;

    [ObservableProperty]
    public partial int Aktif { get; set; }

    [ObservableProperty]
    public partial int OnayBekliyor { get; set; }

    [ObservableProperty]
    public partial int Uyari { get; set; }

    [ObservableProperty]
    public partial int Ihlal { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public DashboardViewModel(ContractService contractService, User currentUser)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var stats = await _contractService.GetDashboardStatsAsync(_currentUser);
            Aktif = stats.Aktif;
            OnayBekliyor = stats.OnayBekliyor;
            Uyari = stats.Uyari;
            Ihlal = stats.Ihlal;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Panel yüklenemedi: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}