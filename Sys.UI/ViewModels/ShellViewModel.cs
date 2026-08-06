using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;

namespace Sys.UI.ViewModels;

public record NavItem(string Key, string Label);

public partial class ShellViewModel : ViewModelBase
{
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

    public ShellViewModel() : this(new User { FullName = "Tasarım Modu", Role = UserRole.Personel }) { }

    public ShellViewModel(User currentUser)
    {
        CurrentUser = currentUser;
        NavItems = new ObservableCollection<NavItem>(BuildNavItems(currentUser.Role));
        SelectedNavItem = NavItems.Count > 0 ? NavItems[0] : null;
        CurrentPageTitle = SelectedNavItem?.Label ?? string.Empty;
    }

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        CurrentPageTitle = value?.Label ?? string.Empty;
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