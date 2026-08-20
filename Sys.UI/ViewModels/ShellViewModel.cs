using System;
using System.Collections.Generic;
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
    private readonly UserManagementService? _userManagementService;
    private readonly string _attachmentsPath;

    public User CurrentUser { get; }
    public event Action? LogoutRequested;

    public string RoleLabel => CurrentUser.Role switch
    {
        UserRole.Personel => "Personel",
        UserRole.SYB => "SYB Uzmanı",
        UserRole.Mudur => "Yönetim / Mali İşler",
        UserRole.Admin => "Sistem Yöneticisi",
        _ => CurrentUser.Role.ToString()
    };

    public ObservableCollection<NavItem> NavItems { get; }

    [ObservableProperty]
    public partial NavItem? SelectedNavItem { get; set; }

    [ObservableProperty]
    public partial string CurrentPageTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ViewModelBase? CurrentPageContent { get; set; }

    public ShellViewModel() : this(new User { FullName = "Tasarım Modu", Role = UserRole.Personel }, null, null, string.Empty) { }

    public ShellViewModel(User currentUser, ContractService? contractService, UserManagementService? userManagementService, string attachmentsPath)
    {
        CurrentUser = currentUser;
        _contractService = contractService;
        _userManagementService = userManagementService;
        _attachmentsPath = attachmentsPath;
        NavItems = new ObservableCollection<NavItem>(BuildNavItems(currentUser.Role));
        SelectedNavItem = NavItems.Count > 0 ? NavItems[0] : null;
        UpdateCurrentPage(SelectedNavItem);
    }

    // Dashboard kartı, "Detay"/"Sözleşme Yarat"/"Son Kontrol" gibi programatik geçişlerde
    // SelectedNavItem'ı (yalnızca sol menüdeki vurguyu doğru göstermek için) güncellerken
    // UpdateCurrentPage'in devreye girip az önce elle kurduğumuz içeriği ezmesini engeller.
    private bool _suppressNavUpdate;

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (_suppressNavUpdate) return;
        UpdateCurrentPage(value);
    }

    // Sol menüdeki vurguyu, ekran içeriğini yeniden oluşturmadan ilgili öğeye taşır.
    // Kullanıcı hangi ekranda olduğunu (ve dashboard'a nasıl geri döneceğini) sol menüden
    // her zaman görebilsin diye, CurrentPageContent'i elle set eden her yerde kullanılır.
    private void SetSelectedNavItemSilently(string key)
    {
        var navItem = NavItems.FirstOrDefault(n => n.Key == key);
        if (navItem is null || ReferenceEquals(navItem, SelectedNavItem)) return;
        _suppressNavUpdate = true;
        SelectedNavItem = navItem;
        _suppressNavUpdate = false;
    }

    // Talep/sözleşme listesi ekranının nav anahtarı ve başlığı role göre değişir
    // (Personel: "Taleplerim", SYB: "Sözleşmeler"). Listeye geri dönen tüm akışlar
    // (düzenleme iptali, detaydan geri, sihirbazdan geri, son kontrolden geri) bunu kullanır.
    private (string Key, string Title) ListNavInfo => CurrentUser.Role == UserRole.Personel
        ? ("talepList", "Taleplerim")
        : ("sozlesmeList", "Sözleşmeler");

    // Menüler arası geçişte bazı ekranların durumu (filtre, arama metni, seçim, sayfa)
    // kaybolmasın diye burada saklanır. "Sözleşmeler"/"Taleplerim" listesi kendi önbellek
    // mantığını GetOrCreateContractListViewModel üzerinden yönetir; burada yalnızca
    // "Sözleşmeleri Görüntüle" ve "İşlem Geçmişi" için basitçe önbelleklenir. Formlar
    // (Yeni Talep, Sözleşme Yarat, Düzenle, İhlal, Fesih) ve Dashboard kasıtlı olarak
    // önbelleklenmez: formlarda eski/gönderilmiş verinin tekrar görünmesi kafa karıştırır,
    // Dashboard'daki sayılar ise her girişte güncel olmalı. Not: ViewModel önbelleklense de
    // ekranın kendisi (View) her geçişte yeniden oluşturulduğu için kaydırma (scroll)
    // konumu sıfırlanır — kaybolmayan şey filtre/arama/seçim durumu ve yeniden yüklenmeyen veridir.
    private readonly Dictionary<string, ViewModelBase> _pageCache = new();
    private static readonly HashSet<string> SimpleCacheableKeys = new() { "sozlesmeGoruntule", "auditLog" };

    private void UpdateCurrentPage(NavItem? value)
    {
        CurrentPageTitle = value?.Label ?? string.Empty;
        _ = RefreshPendingApprovalCountAsync();

        if (_contractService is null)
        {
            CurrentPageContent = new PlaceholderViewModel { Title = CurrentPageTitle };
            return;
        }

        var key = value?.Key;

        if (key is not null && SimpleCacheableKeys.Contains(key) && _pageCache.TryGetValue(key, out var cached))
        {
            CurrentPageContent = cached;
            return;
        }

        ViewModelBase page = key switch
        {
            "dashboard" => CreateDashboardViewModel(),
            "sozlesmeList" => GetOrCreateContractListViewModel(),
            "talepList" => GetOrCreateContractListViewModel(),
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
            "kullaniciYonetimi" => new UserManagementViewModel(_userManagementService!, CurrentUser),
            _ => new PlaceholderViewModel { Title = CurrentPageTitle }
        };

        if (key is not null && SimpleCacheableKeys.Contains(key))
            _pageCache[key] = page;

        CurrentPageContent = page;
    }

    // "Sözleşmeler"/"Taleplerim" ekranına dönen tüm yollar (sol menü, dashboard kartı,
    // detay/düzenle/sihirbaz/son-kontrol ekranlarından "Geri") buradan geçer. Bir filtre
    // özellikle istenmediyse önbellekteki mevcut liste (filtre/arama durumuyla birlikte)
    // döndürülür; istendiyse (örn. dashboard kartından) yeni bir liste oluşturulup
    // önbelleğin yerini alır.
    private ContractListViewModel GetOrCreateContractListViewModel(string? initialFilter = null)
    {
        var key = ListNavInfo.Key;

        if (initialFilter is null && _pageCache.TryGetValue(key, out var cached) && cached is ContractListViewModel cachedList)
            return cachedList;

        var vm = CreateContractListViewModel(initialFilter);
        _pageCache[key] = vm;
        return vm;
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

    private ContractListViewModel CreateContractListViewModel(string? initialFilter = null)
    {
        var vm = new ContractListViewModel(_contractService!, CurrentUser, initialFilter);
        vm.EditRequested += OnEditRequested;
        vm.ViewDetailsRequested += OnViewDetailsRequested;
        vm.ContractCreationRequested += OnContractCreationRequested;
        vm.SonKontrolRequested += OnSonKontrolRequested;
        return vm;
    }

    // Gösterge panelini ve durum kartı/hızlı aksiyon olaylarını birbirine bağlar.
    private DashboardViewModel CreateDashboardViewModel()
    {
        var vm = new DashboardViewModel(_contractService!, CurrentUser);
        vm.FilterCardClicked += OnDashboardFilterClicked;
        vm.QuickActionClicked += OnDashboardQuickActionClicked;
        vm.UpcomingContractClicked += OnDashboardContractOpened;
        return vm;
    }

    // Gösterge panelindeki durum kartlarına tıklandığında ilgili listeye o filtre
    // uygulanmış şekilde yönlendirir; sol menü de o ekranı vurgular ki kullanıcı
    // nerede olduğunu görsün ve dashboard'a dönmek istediğinde menüden bulabilsin.
    // Müdür rolünde genel bir "tüm sözleşmeler" listesi olmadığından yalnızca
    // "Onay Bekliyor" kartı (var olan Onay Bekleyenler ekranına) çalışır.
    private void OnDashboardFilterClicked(string filterKey)
    {
        switch (CurrentUser.Role)
        {
            case UserRole.Personel:
                SetSelectedNavItemSilently("talepList");
                CurrentPageTitle = "Taleplerim";
                CurrentPageContent = GetOrCreateContractListViewModel(filterKey);
                break;
            case UserRole.SYB:
                SetSelectedNavItemSilently("sozlesmeList");
                CurrentPageTitle = "Sözleşmeler";
                CurrentPageContent = GetOrCreateContractListViewModel(filterKey);
                break;
            case UserRole.Mudur when filterKey == "onay_bekliyor":
                SetSelectedNavItemSilently("onayBekleyen");
                CurrentPageTitle = "Onay Bekleyenler";
                CurrentPageContent = CreateApprovalQueueViewModel();
                break;
        }
    }

    // Gösterge panelindeki "Yaklaşan Bitişler" listesinden bir sözleşmeye tıklandığında
    // çalışır. Sol menüde "Sözleşmeleri Görüntüle" vurgulanır; detaydaki "Geri" butonu
    // kullanıcıyı geldiği yere (dashboard'a) götürür — listeye değil, çünkü buraya
    // listeden değil dashboard'dan gelindi.
    private void OnDashboardContractOpened(Contract contract)
    {
        var detailVm = new ContractDetailViewModel(_contractService!, CurrentUser, contract);
        detailVm.BackRequested += () =>
        {
            SetSelectedNavItemSilently("dashboard");
            CurrentPageTitle = "Gösterge Paneli";
            CurrentPageContent = CreateDashboardViewModel();
        };

        SetSelectedNavItemSilently("sozlesmeGoruntule");
        CurrentPageTitle = "Sözleşmeleri Görüntüle";
        CurrentPageContent = detailVm;
    }

    // Gösterge panelindeki hızlı aksiyon butonlarına tıklandığında, sol menüdeki
    // karşılık gelen öğeyi seçili hale getirip normal gezinme akışını tetikler.
    private void OnDashboardQuickActionClicked(string navKey)
    {
        var navItem = NavItems.FirstOrDefault(n => n.Key == navKey);
        if (navItem is not null)
            SelectedNavItem = navItem;
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
        var (listKey, listTitle) = ListNavInfo;
        editVm.CancelRequested += () =>
        {
            SetSelectedNavItemSilently(listKey);
            CurrentPageTitle = listTitle;
            CurrentPageContent = GetOrCreateContractListViewModel();
        };

        // Düzenleme, "Yeni Sözleşme Talebi" ekranıyla aynı ViewModel'i kullanır,
        // bu yüzden sol menüde de o öğe vurgulanır.
        SetSelectedNavItemSilently("yeniTalep");
        CurrentPageTitle = "Talebi Düzenle";
        CurrentPageContent = editVm;
    }

    private void OnViewDetailsRequested(Contract contract)
    {
        var detailVm = new ContractDetailViewModel(_contractService!, CurrentUser, contract);
        var (listKey, listTitle) = ListNavInfo;
        detailVm.BackRequested += () =>
        {
            SetSelectedNavItemSilently(listKey);
            CurrentPageTitle = listTitle;
            CurrentPageContent = GetOrCreateContractListViewModel();
        };

        SetSelectedNavItemSilently("sozlesmeGoruntule");
        CurrentPageTitle = "Sözleşmeleri Görüntüle";
        CurrentPageContent = detailVm;
    }

    private void OnContractCreationRequested(Contract contract)
    {
        var wizardVm = new ContractWizardViewModel(_contractService!, CurrentUser, _attachmentsPath, contract);
        var (listKey, listTitle) = ListNavInfo;
        wizardVm.BackRequested += () =>
        {
            SetSelectedNavItemSilently(listKey);
            CurrentPageTitle = listTitle;
            CurrentPageContent = GetOrCreateContractListViewModel();
        };

        SetSelectedNavItemSilently("sozlesmeYarat");
        CurrentPageTitle = "Sözleşme Yarat";
        CurrentPageContent = wizardVm;
    }

    private void OnSonKontrolRequested(Contract contract)
    {
        var approvalVm = CreateApprovalQueueViewModel(contract);
        var (listKey, listTitle) = ListNavInfo;
        approvalVm.BackRequested += () =>
        {
            SetSelectedNavItemSilently(listKey);
            CurrentPageTitle = listTitle;
            CurrentPageContent = GetOrCreateContractListViewModel();
        };

        SetSelectedNavItemSilently("sozlesmeKontrol");
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
        // Admin, sözleşme iş akışına hiç katılmaz — tek sorumluluğu hesap yönetimidir.
        UserRole.Admin =>
        [
            new("kullaniciYonetimi", "Kullanıcı Yönetimi"),
        ],
        _ => []
    };

    [RelayCommand]
    private void Logout()
    {
        LogoutRequested?.Invoke();
    }
}