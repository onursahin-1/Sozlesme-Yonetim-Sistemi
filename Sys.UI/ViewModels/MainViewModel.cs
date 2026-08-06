using CommunityToolkit.Mvvm.ComponentModel;
using Sys.Domain;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly AuthService _authService;

    [ObservableProperty]
    public partial ViewModelBase CurrentViewModel { get; set; }

    public MainViewModel() : this(null!) { } // yalnızca tasarımcı önizlemesi için

    public MainViewModel(AuthService authService)
    {
        _authService = authService;
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
        var shell = new ShellViewModel(user);
        shell.LogoutRequested += OnLogoutRequested;
        CurrentViewModel = shell;
    }

    private void OnLogoutRequested()
    {
        CurrentViewModel = CreateLogin();
    }
}