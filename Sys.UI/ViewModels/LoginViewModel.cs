using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Services;

namespace Sys.UI.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly AuthService _authService;

    public event Action<User>? LoginSucceeded;

    [ObservableProperty]
    public partial string Username { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public LoginViewModel() : this(null!) { } // yalnızca tasarımcı önizlemesi için

    public LoginViewModel(AuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;
        var result = await _authService.LoginAsync(Username, Password);
        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage ?? "Giriş başarısız.";
            return;
        }

        LoginSucceeded?.Invoke(result.User!);
    }
}