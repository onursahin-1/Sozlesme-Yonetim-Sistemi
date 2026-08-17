using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class NavItem : ObservableObject
{
    public string Key { get; }
    public string Label { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowBadge))]
    public partial int Count { get; set; }

    public bool ShowBadge => Count > 0;

    public NavItem(string key, string label)
    {
        Key = key;
        Label = label;
    }
}

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
        _ = RefreshPendingApprovalCountAsync();

        if (_contractService is null)
        {
            CurrentPageContent = new PlaceholderViewModel { Title = CurrentPageTitle };
            return;
        }

        CurrentPageContent = value?.Key switch
        {
            "dashboard" => new DashboardViewModel(_contractService, CurrentUser),
            "sozlesmeList" => CreateContractListViewModel(),
            "talepList" => CreateContractListViewModel(),
            "yeniTalep" => new NewRequestViewModel(_contractService, CurrentUser, _attachmentsPath),
            "sozlesmeYarat" => new ContractWizardViewModel(_contractService, CurrentUser, _attachmentsPath),
            "sozlesmeGoruntule" => new ContractDetailViewModel(_contractService, CurrentUser),
            "sozlesmeKontrol" => CreateApprovalQueueViewModel(),
            "sozlesmeDegistir" => new ContractEditViewModel(_contractService, CurrentUser, _attachmentsPath),
            "ihlal" => new ViolationReportViewModel(_contractService, CurrentUser, _attachmentsPath),
            "fesih" => new ContractTerminationViewModel(_contractService, CurrentUser, _attachmentsPath),
            "arsiv" => new ArchiveViewModel(_contractService, CurrentUser),
            "onayBekleyen" => CreateApprovalQueueViewModel(),
            "auditLog" => new AuditLogViewModel(_contractService, CurrentUser),
            _ => new PlaceholderViewModel { Title = CurrentPageTitle }
        };
    }

    // Sol menüdeki "Onay Bekleyenler" (Müdür) yanında kaç sözleşmenin
    // onayını beklediğini gösteren rozeti günceller. Her ekran geçişinde
    // tazelenir, böylece kullanıcı menüde gezindikçe sayı güncel kalır.
    private async Task RefreshPendingApprovalCountAsync()
    {
        if (_contractService is null || CurrentUser.Role != UserRole.Mudur) return;

        var navItem = NavItems.FirstOrDefault(n => n.Key == "onayBekleyen");
        if (navItem is null) return;

        try
        {
            var pending = await _contractService.GetPendingApprovalsAsync(CurrentUser);
            navItem.Count = pending.Count;
        }
        catch
        {
            // Rozet güncellenemezse sessizce yut — kritik bir işlev değil.
        }
    }

    private ContractListViewModel CreateContractListViewModel()
    {
        var vm = new ContractListViewModel(_contractService!, CurrentUser);
        vm.EditRequested += OnEditRequested;
        vm.ViewDetailsRequested += OnViewDetailsRequested;
        vm.ContractCreationRequested += OnContractCreationRequested;
        vm.SonKontrolRequested += OnSonKontrolRequested;
        return vm;
    }

    // Onay/red kararı verildiğinde "Onay Bekleyenler" rozetinin ekran
    // değiştirmeyi beklemeden anında tazelenmesi için DecisionMade olayına abone olur.
    private ApprovalQueueViewModel CreateApprovalQueueViewModel(Contract? initialContract = null)
    {
        var vm = new ApprovalQueueViewModel(_contractService!, CurrentUser, initialContract);
        vm.DecisionMade += () => _ = RefreshPendingApprovalCountAsync();
        return vm;
    }

    private void OnEditRequested(Contract contract)
    {
        var editVm = new NewRequestViewModel(_contractService!, CurrentUser, _attachmentsPath, contract);
        editVm.CancelRequested += () =>
        {
            CurrentPageTitle = "Taleplerim";
            CurrentPageContent = CreateContractListViewModel();
        };

        CurrentPageTitle = "Talebi Düzenle";
        CurrentPageContent = editVm;
    }

    private void OnViewDetailsRequested(Contract contract)
    {
        var detailVm = new ContractDetailViewModel(_contractService!, CurrentUser, contract);
        detailVm.BackRequested += () =>
        {
            CurrentPageTitle = "Sözleşmeler";
            CurrentPageContent = CreateContractListViewModel();
        };

        CurrentPageTitle = "Sözleşmeleri Görüntüle";
        CurrentPageContent = detailVm;
    }

    private void OnContractCreationRequested(Contract contract)
    {
        var wizardVm = new ContractWizardViewModel(_contractService!, CurrentUser, _attachmentsPath, contract);
        wizardVm.BackRequested += () =>
        {
            CurrentPageTitle = "Sözleşmeler";
            CurrentPageContent = CreateContractListViewModel();
        };

        CurrentPageTitle = "Sözleşme Yarat";
        CurrentPageContent = wizardVm;
    }

    private void OnSonKontrolRequested(Contract contract)
    {
        var approvalVm = CreateApprovalQueueViewModel(contract);
        approvalVm.BackRequested += () =>
        {
            CurrentPageTitle = "Sözleşmeler";
            CurrentPageContent = CreateContractListViewModel();
        };

        CurrentPageTitle = "Son Kontrol (SYB)";
        CurrentPageContent = approvalVm;
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
            new("auditLog", "İşlem Geçmişi"),
        ],
        _ => []
    };

    [RelayCommand]
    private void Logout()
    {
        LogoutRequested?.Invoke();
    }
}