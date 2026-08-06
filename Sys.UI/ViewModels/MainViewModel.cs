using CommunityToolkit.Mvvm.ComponentModel;
using Sys.Domain;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly ContractService _contractService;

    [ObservableProperty]
    public partial ViewModelBase CurrentViewModel { get; set; }

    public MainViewModel() : this(null!, null!) { }

    public MainViewModel(AuthService authService, ContractService contractService)
    {
        _authService = authService;
        _contractService = contractService;
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
        var shell = new ShellViewModel(user, _contractService);
        shell.LogoutRequested += OnLogoutRequested;
        CurrentViewModel = shell;
    }

    private void OnLogoutRequested()
    {
        CurrentViewModel = CreateLogin();
    }
}