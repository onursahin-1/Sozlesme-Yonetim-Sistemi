using CommunityToolkit.Mvvm.ComponentModel;
using Sys.Domain;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly ContractService _contractService;
    private readonly UserManagementService _userManagementService;
    private readonly NotificationService _notificationService;
    private readonly string _attachmentsPath;

    [ObservableProperty]
    public partial ViewModelBase CurrentViewModel { get; set; }

    public MainViewModel() : this(null!, null!, null!, null!, string.Empty) { }

    public MainViewModel(AuthService authService, ContractService contractService, UserManagementService userManagementService, NotificationService notificationService, string attachmentsPath)
    {
        _authService = authService;
        _contractService = contractService;
        _userManagementService = userManagementService;
        _notificationService = notificationService;
        _attachmentsPath = attachmentsPath;
        CurrentViewModel = CreateLogin();
    }

    private LoginViewModel CreateLogin()
    {
        var login = new LoginViewModel(_authService);
        login.LoginSucceeded += OnLoginSucceeded;
        return login;
    }

    private void OnLoginSucceeded(User user)
    {
        // Şifreyi yönetici belirlediyse kullanıcı uygulamaya HİÇ girmez; önce
        // şifresini değiştirmesi gerekir. Kabuk içinde bir kilit yerine kabuğun
        // öncesinde durdurmak daha güvenli: kabuk açılsaydı bildirimler, klavye
        // kısayolları ve arka plan yüklemeleri zaten çalışmaya başlamış olurdu.
        if (user.MustChangePassword)
        {
            CurrentViewModel = CreateForcedPasswordChange(user);
            return;
        }

        OpenShell(user);
    }

    private ChangePasswordViewModel CreateForcedPasswordChange(User user)
    {
        var vm = new ChangePasswordViewModel(_authService, user, isForced: true);
        vm.ForcedChangeCompleted += () => OpenShell(user);
        vm.ForcedLogoutRequested += OnLogoutRequested;
        return vm;
    }

    private void OpenShell(User user)
    {
        var shell = new ShellViewModel(user, _contractService, _userManagementService, _notificationService, _authService, _attachmentsPath);
        shell.LogoutRequested += OnLogoutRequested;
        CurrentViewModel = shell;
    }

    private void OnLogoutRequested()
    {
        CurrentViewModel = CreateLogin();
    }
}