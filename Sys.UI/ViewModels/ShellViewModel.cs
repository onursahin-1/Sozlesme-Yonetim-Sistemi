using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Services;

namespace Sys.UI.ViewModels;

public record NavItem(string Key, string Label);

public partial class ShellViewModel : ViewModelBase
{
    private readonly ContractService? _contractService;
    private readonly string _attachmentsPath;

    public User CurrentUser { get; }
    public event Action? LogoutRequested;

    public string RoleLabel => CurrentUser.Role switch
    {
        UserRole.Personel => "Personel",
        UserRole.SYB => "SYB Uzmanı",
        UserRole.Mudur => "Yönetim / Mali İşler",
        _ => CurrentUser.Role.ToString()
    };

    public ObservableCollection<NavItem> NavItems { get; }

    [ObservableProperty]
    public partial NavItem? SelectedNavItem { get; set; }

    [ObservableProperty]
    public partial string CurrentPageTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ViewModelBase? CurrentPageContent { get; set; }

    public ShellViewModel() : this(new User { FullName = "Tasarım Modu", Role = UserRole.Personel }, null, string.Empty) { }

    public ShellViewModel(User currentUser, ContractService? contractService, string attachmentsPath)
    {
        CurrentUser = currentUser;
        _contractService = contractService;
        _attachmentsPath = attachmentsPath;
        NavItems = new ObservableCollection<NavItem>(BuildNavItems(currentUser.Role));
        SelectedNavItem = NavItems.Count > 0 ? NavItems[0] : null;
        UpdateCurrentPage(SelectedNavItem);
    }

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        UpdateCurrentPage(value);
    }

    private void UpdateCurrentPage(NavItem? value)
    {
        CurrentPageTitle = value?.Label ?? string.Empty;

        if (_contractService is null)
        {
            CurrentPageContent = new PlaceholderViewModel { Title = CurrentPageTitle };
            return;
        }

        CurrentPageContent = value?.Key switch
        {
            "dashboard" => new DashboardViewModel(_contractService, CurrentUser),
            "sozlesmeList" => new ContractListViewModel(_contractService, CurrentUser),
            "yeniTalep" => new NewRequestViewModel(_contractService, CurrentUser, _attachmentsPath),
            "sozlesmeYarat" => new ContractWizardViewModel(_contractService, CurrentUser, _attachmentsPath),
            "sozlesmeGoruntule" => new ContractDetailViewModel(_contractService, CurrentUser),
            "sozlesmeKontrol" => new ApprovalQueueViewModel(_contractService, CurrentUser),
            "sozlesmeDegistir" => new ContractEditViewModel(_contractService, CurrentUser, _attachmentsPath),
            "ihlal" => new ViolationReportViewModel(_contractService, CurrentUser, _attachmentsPath),
            "onayBekleyen" => new ApprovalQueueViewModel(_contractService, CurrentUser),
            _ => new PlaceholderViewModel { Title = CurrentPageTitle }
        };
    }

    private static NavItem[] BuildNavItems(UserRole role) => role switch
    {
        UserRole.Personel =>
        [
            new("dashboard", "Gösterge Paneli"),
            new("talepList", "Taleplerim"),
            new("yeniTalep", "Yeni Sözleşme Talebi"),
            new("sozlesmeGoruntule", "Sözleşmeleri Görüntüle"),
            new("ihlal", "İhlal Bildir"),
            new("arsiv", "Arşiv"),
        ],
        UserRole.SYB =>
        [
            new("dashboard", "Gösterge Paneli"),
            new("sozlesmeList", "Sözleşmeler"),
            new("yeniTalep", "Yeni Sözleşme Talebi"),
            new("sozlesmeYarat", "Sözleşme Yarat"),
            new("sozlesmeGoruntule", "Sözleşmeleri Görüntüle"),
            new("sozlesmeKontrol", "Son Kontrol (SYB)"),
            new("sozlesmeDegistir", "Sözleşme Değiştir"),
            new("fesih", "Sözleşme Fesih"),
            new("ihlal", "İhlal Bildir"),
            new("arsiv", "Arşiv"),
        ],
        UserRole.Mudur =>
        [
            new("dashboard", "Gösterge Paneli"),
            new("onayBekleyen", "Onay Bekleyenler"),
            new("sozlesmeGoruntule", "Sözleşmeleri Görüntüle"),
            new("arsiv", "Arşiv"),
        ],
        _ => []
    };

    [RelayCommand]
    private void Logout()
    {
        LogoutRequested?.Invoke();
    }
}