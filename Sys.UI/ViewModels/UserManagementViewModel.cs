using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sys.Domain;
using Sys.Services;
using Sys.UI.Localization;

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

    public UserRole Role => _user.Role;
    public string RoleLabel => UserRoleHelper.ToLabel(_user.Role);

    // Kullanıcı adının baş harfleri; satırın solundaki yuvarlak rozet.
    public string Initials
    {
        get
        {
            var name = FullName.Trim();
            if (name.Length == 0) return "?";

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0][..1].ToUpperInvariant();
            return (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
        }
    }

    public string DepartmentText => string.IsNullOrWhiteSpace(Department) ? Strings.T("Usr.NoDepartment") : Department!;

    // Yönetici kendi hesabını devre dışı bırakamaz (servis de engelliyor). Buton
    // gösterilip tıklandığında hata vermek yerine baştan gizleniyor.
    public bool IsSelf { get; set; }
    public bool CanToggleDisabled => !IsSelf;

    public bool IsDisabled => _user.IsDisabled;
    public string StatusLabel => Strings.T(IsDisabled ? "Usr.Disabled" : "Usr.Active");
    public string StatusColorHex => IsDisabled ? "DangerBase" : "SuccessBase";
    public string StatusBgHex => IsDisabled ? "DangerSoftBg" : "SuccessSoftBg";
    public string ToggleButtonLabel => Strings.T(IsDisabled ? "Usr.Enable" : "Usr.Disable");

    // Dolu renkli buton yerine çerçeveli/soluk zemin: bu iki eylem listede her satırda
    // tekrar ettiği için dolu kırmızı/yeşil butonlar ekranı gereksiz yere gürültülü
    // yapıyordu. Renk yine anlamı taşıyor ama arka planda kalıyor.
    public string ToggleButtonBgHex => IsDisabled ? "SuccessSoftBgAlt" : "DangerSoftBgFaint3";
    public string ToggleButtonBorderHex => IsDisabled ? "SuccessSoftBorder" : "DangerSoftBorderAlt";
    public string ToggleButtonFgHex => IsDisabled ? "SuccessBase" : "DangerBase";

    [ObservableProperty]
    public partial bool IsResettingPassword { get; set; }

    [ObservableProperty]
    public partial string NewPasswordText { get; set; } = string.Empty;

    // Şifre kutusu bir DataTemplate içinde olduğu için her satırda tekrar ediyor;
    // x:Name ile tek bir uyarı öğesine erişilemiyor. Uyarı bu yüzden satırın kendi
    // durumundan besleniyor.
    [ObservableProperty]
    public partial bool ShowCapsWarning { get; set; }

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
        OnPropertyChanged(nameof(ToggleButtonBorderHex));
        OnPropertyChanged(nameof(ToggleButtonFgHex));
    }
}

// Giriş ekranından gelen "şifremi unuttum" taleplerinden bir satır.
public class ResetRequestRowViewModel
{
    private readonly PasswordResetRequest _request;

    public ResetRequestRowViewModel(PasswordResetRequest request, string? matchedFullName)
    {
        _request = request;
        MatchedFullName = matchedFullName;
    }

    public int Id => _request.Id;
    public string Username => _request.Username;
    public int? UserId => _request.UserId;

    // Kullanıcı adı sistemde bulunamadıysa null olur — muhtemelen yazım hatası.
    public string? MatchedFullName { get; }

    public string DisplayText => MatchedFullName is null
        ? Strings.T("Usr.NotFound", Username)
        : $"{MatchedFullName} ({Username})";

    public string TimeText => _request.RequestedAt.ToString("dd.MM.yyyy HH:mm");
    public bool IsUnknownUser => MatchedFullName is null;
}

// Açılır listede etiket görünsün diye rol değeri sarmalanıyor; enum'u doğrudan
// bağlamak "SYB", "Mudur" gibi ham adları gösteriyordu.
//
// Sınıf içinde iç içe tip olarak değil ad alanı seviyesinde duruyor: XAML'de iç içe
// tiplere başvurmak ("Sınıf+Tip") derleyiciye göre değişken davranıyor.
public sealed record RoleOption(UserRole Value, string Label);

public partial class UserManagementViewModel : ViewModelBase
{
    private readonly UserManagementService _userManagementService;
    private readonly User _currentUser;

    public RoleOption[] RoleOptions { get; } =
        Enum.GetValues<UserRole>().Select(r => new RoleOption(r, UserRoleHelper.ToLabel(r))).ToArray();

    // Filtre listesi: rollere ek olarak "Tüm roller".
    // Seçeneğin DEĞERİ rolün kendisi (enum adı), ETİKETİ çevrilmiş metin.
    // Eskiden filtre, satırdaki çevrilmiş rol etiketiyle karşılaştırılıyordu;
    // etiket çevrilebilir olduğu an bu karşılaştırma kırılganlaşır.
    public FilterOption[] RoleFilterOptions { get; } =
        new[] { AllRolesOption() }
            .Concat(Enum.GetValues<UserRole>().Select(r => new FilterOption(r.ToString(), UserRoleHelper.ToLabel(r))))
            .ToArray();

    private static FilterOption AllRolesOption() => new(null, Strings.T("Usr.AllRoles"));
    private static FilterOption AllStatusOption() => new(null, Strings.T("Common.All"));

    // Açılır listede görünen metin AYNI ZAMANDA "filtre yok" işareti; sabit
    // olamaz çünkü dile bağlı. Dil değişince sayfa baştan kurulduğu için işaret
    // ve liste birlikte yenileniyor.


    // Alan değil ÖZELLİK: seçenekler o anki dilden kuruluyor. Sabit dizi olsaydı
    // uygulama açılışındaki dile kilitlenirdi.
    public FilterOption[] StatusFilterOptions =>
    [
        AllStatusOption(),
        new("active", Strings.T("Usr.Active")),
        new("disabled", Strings.T("Usr.Disabled")),
    ];

    // Kaynak liste; ekranda gösterilen Users bunun filtrelenmiş hâli.
    private List<UserRowViewModel> _allUsers = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(CountText))]
    public partial ObservableCollection<UserRowViewModel> Users { get; set; } = new();

    // Kullanıcı sayısı arttıkça listede birini bulmak zorlaşıyordu; arama ve iki
    // filtre eklendi. Liste tamamı bellekte olduğu için süzme yerel yapılıyor.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    public partial FilterOption SelectedRoleFilter { get; set; } = AllRolesOption();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    public partial FilterOption SelectedStatusFilter { get; set; } = AllStatusOption();

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedRoleFilterChanged(FilterOption value) => ApplyFilters();
    partial void OnSelectedStatusFilterChanged(FilterOption value) => ApplyFilters();

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchText)
        || SelectedRoleFilter is { IsAll: false }
        || SelectedStatusFilter is { IsAll: false };

    public bool IsEmpty => !IsLoading && Users.Count == 0;

    public string CountText => _allUsers.Count == Users.Count
        ? Strings.T("Usr.CountAll", Users.Count)
        : Strings.T("Usr.CountFiltered", Users.Count, _allUsers.Count);

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedRoleFilter = RoleFilterOptions[0];
        SelectedStatusFilter = StatusFilterOptions[0];
    }

    private void ApplyFilters()
    {
        IEnumerable<UserRowViewModel> query = _allUsers;

        var term = SearchText?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            query = query.Where(u =>
                u.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || u.Username.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (u.Department ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase));

        if (SelectedRoleFilter is { Value: { } roleName })
            query = query.Where(u => u.Role.ToString() == roleName);

        if (SelectedStatusFilter?.Value == "active")
            query = query.Where(u => !u.IsDisabled);
        else if (SelectedStatusFilter?.Value == "disabled")
            query = query.Where(u => u.IsDisabled);

        Users = new ObservableCollection<UserRowViewModel>(query);
        OnPropertyChanged(nameof(CountText));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; set; } = true;

    // Giriş ekranından gelen, henüz karşılanmamış şifre sıfırlama talepleri.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResetRequests))]
    [NotifyPropertyChangedFor(nameof(ResetRequestHeader))]
    public partial ObservableCollection<ResetRequestRowViewModel> ResetRequests { get; set; } = new();

    public bool HasResetRequests => ResetRequests.Count > 0;
    public string ResetRequestHeader => Strings.T("Usr.PendingResets", ResetRequests.Count);

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
    public partial RoleOption? NewRoleOption { get; set; }

    [ObservableProperty]
    public partial string NewPassword { get; set; } = string.Empty;

    // Yeni kullanıcı formundaki geçici şifre alanı için Caps Lock uyarısı.
    [ObservableProperty]
    public partial bool ShowNewUserCapsWarning { get; set; }

    // Aynı anda birden fazla işlemin (oluşturma/sıfırlama/devre dışı bırakma) tetiklenmesini
    // engeller — diğer ekranlardaki IsBusy koruma desenizle aynı.
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public UserManagementViewModel() : this(null!, new User()) { } // tasarımcı önizlemesi için

    public UserManagementViewModel(UserManagementService userManagementService, User currentUser)
    {
        _userManagementService = userManagementService;
        _currentUser = currentUser;
        NewRoleOption = RoleOptions.FirstOrDefault(r => r.Value == UserRole.Personel);
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var users = await _userManagementService.GetAllUsersAsync(_currentUser);
            _allUsers = users
                .Select(u => new UserRowViewModel(u) { IsSelf = u.Id == _currentUser.Id })
                .ToList();
            ApplyFilters();

            await LoadResetRequestsAsync(users);
        }
        catch (Exception ex)
        {
            ErrorMessage = Strings.T("Usr.LoadFailed", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Talepler kullanıcı listesiyle birlikte yükleniyor; talepteki kullanıcı adı
    // listeyle eşleştirilip Admin'e ad soyad gösteriliyor, eşleşmeyenler ise
    // "sistemde bulunamadı" olarak işaretleniyor (muhtemelen yazım hatası).
    private async Task LoadResetRequestsAsync(List<User> users)
    {
        try
        {
            var requests = await _userManagementService.GetPendingResetRequestsAsync(_currentUser);
            var byId = users.ToDictionary(u => u.Id, u => u.FullName);

            ResetRequests = new ObservableCollection<ResetRequestRowViewModel>(
                requests.Select(r => new ResetRequestRowViewModel(
                    r,
                    r.UserId is not null && byId.TryGetValue(r.UserId.Value, out var name) ? name : null)));
        }
        catch
        {
            // Talepler yüklenemezse ekranın geri kalanı çalışmaya devam etmeli.
            ResetRequests = new ObservableCollection<ResetRequestRowViewModel>();
        }
    }

    // Talebi karşılamadan kapatır (örn. kullanıcı adı hatalı girilmiş).
    [RelayCommand]
    private async Task DismissResetRequest(ResetRequestRowViewModel row)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await _userManagementService.DismissResetRequestAsync(_currentUser, row.Id);
            ResetRequests.Remove(row);
            OnPropertyChanged(nameof(HasResetRequests));
            OnPropertyChanged(nameof(ResetRequestHeader));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
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
        NewRoleOption = RoleOptions.FirstOrDefault(r => r.Value == UserRole.Personel);
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
                ErrorMessage = Strings.T("Usr.FieldsRequired");
                return;
            }

            // Kural PasswordPolicy'den geliyor; servis de aynı kaynağa bakıyor.
            // Ekranda kabul edilip serviste reddedilen bir şifre olmasın.
            if (PasswordPolicy.Validate(NewPassword) is { } policyError)
            {
                ErrorMessage = ErrorText.Of(policyError);
                return;
            }

            try
            {
                var created = await _userManagementService.CreateUserAsync(
                    _currentUser, NewUsername.Trim(), NewFullName.Trim(),
                    NewRoleOption?.Value ?? UserRole.Personel,
                    string.IsNullOrWhiteSpace(NewDepartment) ? null : NewDepartment.Trim(), NewPassword);

                // Yeni kullanıcı kaynak listeye eklenip filtreler yeniden uygulanıyor;
                // doğrudan Users'a eklenirse aktif bir filtre varken liste tutarsız kalırdı.
                _allUsers.Add(new UserRowViewModel(created) { IsSelf = false });
                ApplyFilters();

                SuccessMessage = Strings.T("Usr.Created", created.FullName);
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

            if (PasswordPolicy.Validate(row.NewPasswordText) is { } policyError)
            {
                ErrorMessage = ErrorText.Of(policyError);
                return;
            }

            try
            {
                await _userManagementService.ResetPasswordAsync(_currentUser, row.Id, row.NewPasswordText);

                // Kullanıcının bunu ilk girişinde değiştireceğini yöneticinin bilmesi
                // gerekiyor: aksi halde "şifreyi verdim ama çalışmıyor" diye geri döner.
                SuccessMessage = Strings.T("Usr.PasswordReset", row.FullName) +
                                 Strings.T("Usr.FirstLoginNotice");
                row.IsResettingPassword = false;
                row.NewPasswordText = string.Empty;

                // Servis, bu kullanıcının bekleyen taleplerini karşılanmış olarak
                // işaretledi; listeden de kaldırıp sayacı güncelliyoruz.
                foreach (var handled in ResetRequests.Where(r => r.UserId == row.Id).ToList())
                    ResetRequests.Remove(handled);
                OnPropertyChanged(nameof(HasResetRequests));
                OnPropertyChanged(nameof(ResetRequestHeader));
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

                // Durum filtresi açıksa satır artık listeye uymuyor olabilir.
                ApplyFilters();

                SuccessMessage = updated.IsDisabled
                    ? Strings.T("Usr.Disabled2", updated.FullName)
                    : Strings.T("Usr.Enabled2", updated.FullName);
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