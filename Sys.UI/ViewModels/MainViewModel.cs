using CommunityToolkit.Mvvm.ComponentModel;
using Sys.Domain;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly ContractService _contractService;
    private readonly string _attachmentsPath;

    [ObservableProperty]
    public partial ViewModelBase CurrentViewModel { get; set; }

    public MainViewModel() : this(null!, null!, string.Empty) { }

    public MainViewModel(AuthService authService, ContractService contractService, string attachmentsPath)
    {
        _authService = authService;
        _contractService = contractService;
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
        var shell = new ShellViewModel(user, _contractService, _attachmentsPath);
        shell.LogoutRequested += OnLogoutRequested;
        CurrentViewModel = shell;
    }

    private void OnLogoutRequested()
    {
        CurrentViewModel = CreateLogin();
    }
}