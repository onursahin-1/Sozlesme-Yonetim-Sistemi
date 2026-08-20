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

// Kullanıcı listesindeki her satır için: satıra özel "şifre sıfırlama" alanının açık/kapalı
// durumu ve girilen yeni şifre burada tutulur (ObservableObject, bu yüzden UI anında güncellenir).
public partial class UserRowViewModel : ObservableObject
{
    private User _user;

    public UserRowViewModel(User user) => _user = user;

    public User RawUser => _user;
    public int Id => _user.Id;
    public string Username => _user.Username;
    public string FullName => _user.FullName;
    public string? Department => _user.Department;

    public string RoleLabel => _user.Role switch
    {
        UserRole.Personel => "Personel",
        UserRole.SYB => "SYB Uzmanı",
        UserRole.Mudur => "Yönetim / Mali İşler",
        UserRole.Admin => "Sistem Yöneticisi",
        _ => _user.Role.ToString()
    };

    public bool IsDisabled => _user.IsDisabled;
    public string StatusLabel => IsDisabled ? "Devre Dışı" : "Aktif";
    public string StatusColorHex => IsDisabled ? "#A32D2D" : "#1A6B2A";
    public string StatusBgHex => IsDisabled ? "#FDECEA" : "#E6F4E7";
    public string ToggleButtonLabel => IsDisabled ? "Etkinleştir" : "Devre Dışı Bırak";
    public string ToggleButtonBgHex => IsDisabled ? "#1A6B2A" : "#A32D2D";

    [ObservableProperty]
    public partial bool IsResettingPassword { get; set; }

    [ObservableProperty]
    public partial string NewPasswordText { get; set; } = string.Empty;

    // Devre dışı bırakma/etkinleştirme sonrası güncel kullanıcıyı alıp bağımlı
    // (hesaplanmış) alanların yeniden çizilmesini tetikler.
    public void Refresh(User updatedUser)
    {
        _user = updatedUser;
        OnPropertyChanged(nameof(IsDisabled));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(StatusColorHex));
        OnPropertyChanged(nameof(StatusBgHex));
        OnPropertyChanged(nameof(ToggleButtonLabel));
        OnPropertyChanged(nameof(ToggleButtonBgHex));
    }
}

public partial class UserManagementViewModel : ViewModelBase
{
    private readonly UserManagementService _userManagementService;
    private readonly User _currentUser;

    public UserRole[] RoleOptions { get; } = Enum.GetValues<UserRole>();

    [ObservableProperty]
    public partial ObservableCollection<UserRowViewModel> Users { get; set; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SuccessMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowNewUserForm { get; set; }

    [ObservableProperty]
    public partial string NewUsername { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewFullName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewDepartment { get; set; } = string.Empty;

    [ObservableProperty]
    public partial UserRole NewRole { get; set; } = UserRole.Personel;

    [ObservableProperty]
    public partial string NewPassword { get; set; } = string.Empty;

    // Aynı anda birden fazla işlemin (oluşturma/sıfırlama/devre dışı bırakma) tetiklenmesini
    // engeller — diğer ekranlardaki IsBusy koruma desenizle aynı.
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public UserManagementViewModel() : this(null!, new User()) { } // tasarımcı önizlemesi için

    public UserManagementViewModel(UserManagementService userManagementService, User currentUser)
    {
        _userManagementService = userManagementService;
        _currentUser = currentUser;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var users = await _userManagementService.GetAllUsersAsync(_currentUser);
            Users = new ObservableCollection<UserRowViewModel>(users.Select(u => new UserRowViewModel(u)));
        }
        catch (Exception ex)
        {
            ErrorMessage = "Kullanıcılar yüklenirken bir hata oluştu: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ToggleNewUserForm()
    {
        ShowNewUserForm = !ShowNewUserForm;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        if (!ShowNewUserForm) return;
        NewUsername = string.Empty;
        NewFullName = string.Empty;
        NewDepartment = string.Empty;
        NewRole = UserRole.Personel;
        NewPassword = string.Empty;
    }

    [RelayCommand]
    private async Task CreateUser()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(NewFullName) || string.IsNullOrWhiteSpace(NewPassword))
            {
                ErrorMessage = "Kullanıcı adı, ad soyad ve şifre zorunludur.";
                return;
            }

            if (NewPassword.Length < 6)
            {
                ErrorMessage = "Şifre en az 6 karakter olmalı.";
                return;
            }

            try
            {
                var created = await _userManagementService.CreateUserAsync(
                    _currentUser, NewUsername.Trim(), NewFullName.Trim(), NewRole,
                    string.IsNullOrWhiteSpace(NewDepartment) ? null : NewDepartment.Trim(), NewPassword);

                Users.Add(new UserRowViewModel(created));
                SuccessMessage = $"{created.FullName} kullanıcısı oluşturuldu.";
                ShowNewUserForm = false;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleResetPassword(UserRowViewModel row)
    {
        row.IsResettingPassword = !row.IsResettingPassword;
        row.NewPasswordText = string.Empty;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
    }

    [RelayCommand]
    private async Task ConfirmResetPassword(UserRowViewModel row)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(row.NewPasswordText) || row.NewPasswordText.Length < 6)
            {
                ErrorMessage = "Yeni şifre en az 6 karakter olmalı.";
                return;
            }

            try
            {
                await _userManagementService.ResetPasswordAsync(_currentUser, row.Id, row.NewPasswordText);
                SuccessMessage = $"{row.FullName} kullanıcısının şifresi sıfırlandı.";
                row.IsResettingPassword = false;
                row.NewPasswordText = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleDisabled(UserRowViewModel row)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;

            try
            {
                var updated = await _userManagementService.SetDisabledAsync(_currentUser, row.Id, !row.IsDisabled);
                row.Refresh(updated);
                SuccessMessage = updated.IsDisabled
                    ? $"{updated.FullName} devre dışı bırakıldı."
                    : $"{updated.FullName} yeniden etkinleştirildi.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}