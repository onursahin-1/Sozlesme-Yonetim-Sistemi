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
    private readonly NotificationService? _notificationService;
    private readonly AuthService? _authService;
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

    // Üst çubuktaki avatar dairesi için baş harfler: "Emin Ramazanoğlu" → "ER".
    // Tek kelimelik adlarda ilk iki harf alınır ("Admin" → "AD").
    public string UserInitials
    {
        get
        {
            var parts = (CurrentUser.FullName ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0) return "?";
            if (parts.Length == 1)
                return parts[0].Length >= 2
                    ? parts[0][..2].ToUpperInvariant()
                    : parts[0].ToUpperInvariant();

            return (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
        }
    }

    public ObservableCollection<NavItem> NavItems { get; }

    [ObservableProperty]
    public partial NavItem? SelectedNavItem { get; set; }

    [ObservableProperty]
    public partial string CurrentPageTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ViewModelBase? CurrentPageContent { get; set; }

    // --- Bildirimler (üst çubuktaki zil) ---

    [ObservableProperty]
    public partial ObservableCollection<NotificationRowViewModel> Notifications { get; set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnreadNotifications))]
    [NotifyPropertyChangedFor(nameof(UnreadCountText))]
    public partial int UnreadNotificationCount { get; set; }

    [ObservableProperty]
    public partial bool IsNotificationPanelOpen { get; set; }

    [ObservableProperty]
    public partial bool HasNoNotifications { get; set; }

    // Başlıktaki "Okunanları sil" aksiyonu yalnızca silinecek bir şey varken görünür.
    [ObservableProperty]
    public partial bool HasReadNotifications { get; set; }

    public bool HasUnreadNotifications => UnreadNotificationCount > 0;
    // 99'dan fazlasında rozet genişleyip başlığı bozmasın diye kısaltılır.
    public string UnreadCountText => UnreadNotificationCount > 99 ? "99+" : UnreadNotificationCount.ToString();

    // Zil tüm rollerde görünür. Admin sözleşme iş akışına katılmaz ama şifre sıfırlama
    // taleplerinden haberdar olması gerekir — aksi halde talebi fark etmek için Kullanıcı
    // Yönetimi ekranını açmayı beklemek zorunda kalırdı.
    public bool ShowNotificationBell => _notificationService is not null;

    // --- Kullanıcı menüsü (üst çubukta ada tıklayınca açılır) ---
    // Şifre değiştirme ve çıkış birer "bölüm" değil hesap işlemi olduğu için sol
    // menüde değil burada duruyorlar.

    [ObservableProperty]
    public partial bool IsUserMenuOpen { get; set; }

    [RelayCommand]
    private void ToggleUserMenu()
    {
        IsUserMenuOpen = !IsUserMenuOpen;
        if (IsUserMenuOpen) IsNotificationPanelOpen = false;
    }

    [RelayCommand]
    private void CloseUserMenu() => IsUserMenuOpen = false;

    // Şifre değiştirme ekranı sol menüde olmadığı için hiçbir menü öğesi seçili
    // kalmıyor; kullanıcı "bir bölümün içinde değilim" hissini alıyor. Ekranın
    // kendi "Geri" butonu ve Esc kısayolu onu ilk bölüme geri döndürüyor.
    [RelayCommand]
    private void OpenChangePassword()
    {
        IsUserMenuOpen = false;
        if (_authService is null) return;

        var vm = new ChangePasswordViewModel(_authService, CurrentUser);
        vm.BackRequested += () =>
        {
            var first = NavItems.FirstOrDefault();
            if (first is null) return;
            SelectedNavItem = first;      // normal gezinme akışını tetikler
            UpdateCurrentPage(first);
        };

        _suppressNavUpdate = true;
        SelectedNavItem = null;
        _suppressNavUpdate = false;

        CurrentPageTitle = "Şifre Değiştir";
        CurrentPageContent = vm;
    }

    public ShellViewModel() : this(new User { FullName = "Tasarım Modu", Role = UserRole.Personel }, null, null, null, null, string.Empty) { }

    public ShellViewModel(User currentUser, ContractService? contractService, UserManagementService? userManagementService, NotificationService? notificationService, AuthService? authService, string attachmentsPath)
    {
        CurrentUser = currentUser;
        _contractService = contractService;
        _userManagementService = userManagementService;
        _notificationService = notificationService;
        _authService = authService;
        _attachmentsPath = attachmentsPath;
        NavItems = new ObservableCollection<NavItem>(BuildNavItems(currentUser.Role));
        SelectedNavItem = NavItems.Count > 0 ? NavItems[0] : null;
        UpdateCurrentPage(SelectedNavItem);
        _ = RefreshUnreadNotificationCountAsync();
        StartNotificationPolling();
    }

    // Kullanıcı aynı ekranda dursa bile başka birinin ürettiği bildirimin rozete
    // yansıması için dakikada bir okunmamış sayısı tazelenir. Yalnızca sayı sorgulanır
    // (COUNT), bildirim listesi çekilmez — maliyeti düşüktür.
    private Avalonia.Threading.DispatcherTimer? _notificationTimer;

    private void StartNotificationPolling()
    {
        if (_notificationService is null) return;

        _notificationTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _notificationTimer.Tick += async (_, _) =>
        {
            await RefreshUnreadNotificationCountAsync();
            // Panel açıkken liste de tazelensin ki yeni bildirim anında görünsün.
            if (IsNotificationPanelOpen)
                await LoadNotificationsAsync();
        };
        _notificationTimer.Start();
    }

    // Zile tıklandığında paneli açar/kapatır. Açılışta liste veritabanından tazelenir,
    // böylece başka bir kullanıcının az önce oluşturduğu bildirim de görünür.
    [RelayCommand]
    private async Task ToggleNotificationPanel()
    {
        IsNotificationPanelOpen = !IsNotificationPanelOpen;
        if (IsNotificationPanelOpen)
            await LoadNotificationsAsync();
    }

    [RelayCommand]
    private void CloseNotificationPanel() => IsNotificationPanelOpen = false;

    private async Task LoadNotificationsAsync()
    {
        if (_notificationService is null) return;
        try
        {
            var items = await _notificationService.GetForUserAsync(CurrentUser);
            Notifications = new ObservableCollection<NotificationRowViewModel>(
                items.Select(n => new NotificationRowViewModel(n)));
            UnreadNotificationCount = await _notificationService.GetUnreadCountAsync(CurrentUser);
            UpdateNotificationListFlags();
        }
        catch
        {
            // Bildirimler yüklenemezse ekranın geri kalanı çalışmaya devam etmeli.
        }
    }

    // Rozetteki sayıyı, paneli açmadan tazeler. Her ekran geçişinde çağrılır.
    private async Task RefreshUnreadNotificationCountAsync()
    {
        if (_notificationService is null) return;
        try
        {
            UnreadNotificationCount = await _notificationService.GetUnreadCountAsync(CurrentUser);
        }
        catch
        {
            // Rozet güncellenemezse sessizce yut — kritik bir işlev değil.
        }
    }

    // Bildirime tıklandığında okundu işaretlenir ve (varsa) ilgili sözleşme detayı açılır.
    [RelayCommand]
    private async Task OpenNotification(NotificationRowViewModel row)
    {
        if (_notificationService is null) return;

        if (!row.IsRead)
        {
            try
            {
                await _notificationService.MarkReadAsync(CurrentUser, row.Id);
                row.IsRead = true;
                if (UnreadNotificationCount > 0) UnreadNotificationCount--;
                UpdateNotificationListFlags();
            }
            catch
            {
                // Okundu işaretlenemezse yönlendirmeyi yine de yapalım.
            }
        }

        IsNotificationPanelOpen = false;

        // Şifre sıfırlama talebi bir sözleşmeye değil, Kullanıcı Yönetimi ekranına götürür.
        if (row.Type == NotificationType.SifreSifirlamaTalebi)
        {
            var navItem = NavItems.FirstOrDefault(n => n.Key == "kullaniciYonetimi");
            if (navItem is not null)
                SelectedNavItem = navItem;
            return;
        }

        if (row.ContractId is null || _contractService is null) return;

        try
        {
            var contract = await _contractService.GetContractDetailAsync(row.ContractId.Value, CurrentUser);
            if (contract is null) return;
            OpenContractFromNotification(contract);
        }
        catch
        {
            // Sözleşme açılamazsa (yetki/silinmiş kayıt) sessizce geç.
        }
    }

    [RelayCommand]
    private async Task MarkAllNotificationsRead()
    {
        if (_notificationService is null) return;
        try
        {
            await _notificationService.MarkAllReadAsync(CurrentUser);
            foreach (var row in Notifications)
                row.IsRead = true;
            UnreadNotificationCount = 0;
            UpdateNotificationListFlags();
        }
        catch
        {
            // Sessizce geç.
        }
    }

    // Tek bir okunmuş bildirimi siler. Silme butonu yalnızca okunmuş satırlarda görünür,
    // böylece henüz görülmemiş bir bildirim yanlışlıkla kaybolmaz.
    [RelayCommand]
    private async Task DeleteNotification(NotificationRowViewModel row)
    {
        if (_notificationService is null || !row.IsRead) return;
        try
        {
            await _notificationService.DeleteReadAsync(CurrentUser, row.Id);
            Notifications.Remove(row);
            UpdateNotificationListFlags();
        }
        catch
        {
            // Sessizce geç.
        }
    }

    [RelayCommand]
    private async Task DeleteReadNotifications()
    {
        if (_notificationService is null) return;
        try
        {
            await _notificationService.DeleteAllReadAsync(CurrentUser);
            foreach (var row in Notifications.Where(n => n.IsRead).ToList())
                Notifications.Remove(row);
            UpdateNotificationListFlags();
        }
        catch
        {
            // Sessizce geç.
        }
    }

    private void UpdateNotificationListFlags()
    {
        HasNoNotifications = Notifications.Count == 0;
        HasReadNotifications = Notifications.Any(n => n.IsRead);
    }

    // Bildirimden açılan sözleşmenin "Geri" butonu kullanıcıyı geldiği ekrana değil,
    // gösterge paneline götürür — bildirim her ekrandan açılabildiği için sabit,
    // tahmin edilebilir bir dönüş noktası daha anlaşılır.
    private void OpenContractFromNotification(Contract contract)
    {
        var detailVm = new ContractDetailViewModel(_contractService!, CurrentUser, contract);
        detailVm.BackRequested += () =>
        {
            var dashboard = NavItems.FirstOrDefault(n => n.Key == "dashboard");
            if (dashboard is not null)
            {
                SetSelectedNavItemSilently("dashboard");
                CurrentPageTitle = "Gösterge Paneli";
                CurrentPageContent = CreateDashboardViewModel();
            }
        };

        SetSelectedNavItemSilently("sozlesmeGoruntule");
        CurrentPageTitle = "Sözleşmeleri Görüntüle";
        CurrentPageContent = detailVm;
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
        // Gösterge panelinde başlık gizlenir: ekranın kendi karşılama satırı
        // ("Günaydın, ...") zaten başlığın işlevini görüyor, ikisi birlikte
        // gereksiz tekrar oluşturuyordu.
        CurrentPageTitle = value?.Key == "dashboard" ? string.Empty : (value?.Label ?? string.Empty);
        _ = RefreshPendingApprovalCountAsync();
        _ = RefreshUnreadNotificationCountAsync();

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

    // Şifre değiştirme sol menüde bir "bölüm" değil, üst çubuktaki kullanıcı
    // menüsünden açılan bir hesap işlemi — bkz. OpenChangePasswordCommand.
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
        // Çıkışta zamanlayıcı durdurulmazsa, oturum kapandıktan sonra da eski kullanıcının
        // bildirimlerini sorgulamaya devam ederdi.
        _notificationTimer?.Stop();
        _notificationTimer = null;
        LogoutRequested?.Invoke();
    }
}