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

// "Yaklaşan Bitişler" kutusundaki her satır için hafif bir sarmalayıcı.
public class DashboardUpcomingRowViewModel
{
    private readonly Contract _contract;

    public DashboardUpcomingRowViewModel(Contract contract) => _contract = contract;

    public Contract RawContract => _contract;
    public string Title => _contract.Title;
    public string CompanyName => _contract.CompanyName;
    public string RefNoText => string.IsNullOrEmpty(_contract.ContractNo) ? _contract.RequestRefNo : _contract.ContractNo!;

    public string GunKalanText
    {
        get
        {
            if (_contract.EndDate is null) return "-";
            var days = (_contract.EndDate.Value.Date - DateTime.Today).Days;
            return days == 0 ? "Bugün sona eriyor" : days > 0 ? $"{days} gün kaldı" : "Sona erdi";
        }
    }
}

// "Hızlı Aksiyonlar" satırındaki her buton için.
public class DashboardQuickActionItem
{
    public DashboardQuickActionItem(string label, string navKey)
    {
        Label = label;
        NavKey = navKey;
    }

    public string Label { get; }
    public string NavKey { get; }
}

public partial class DashboardViewModel : ViewModelBase
{
    private readonly ContractService _contractService;
    private readonly User _currentUser;

    [ObservableProperty]
    public partial int Aktif { get; set; }

    [ObservableProperty]
    public partial int OnayBekliyor { get; set; }

    [ObservableProperty]
    public partial int Uyari { get; set; }

    [ObservableProperty]
    public partial int Ihlal { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<DashboardUpcomingRowViewModel> UpcomingEndings { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<AuditLogRowViewModel> RecentActivity { get; set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRecentActivity))]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public ObservableCollection<DashboardQuickActionItem> QuickActions { get; }

    // Müdür rolünde "Sözleşmeler" gibi genel bir liste ekranı olmadığından, Aktif/Uyarı/İhlal
    // kartları Müdür için tıklanabilir değildir — yalnızca "Onay Bekliyor" kartı, zaten var olan
    // "Onay Bekleyenler" ekranına yönlendirebildiği için tıklanabilir kalır.
    public bool AktifCardClickable => _currentUser.Role != UserRole.Mudur;
    public bool UyariCardClickable => _currentUser.Role != UserRole.Mudur;
    public bool IhlalCardClickable => _currentUser.Role != UserRole.Mudur;
    public bool OnayBekliyorCardClickable => true;

    // "Son Aktiviteler", "İşlem Geçmişi" ekranıyla aynı isim-isim işlem kaydını gösterdiği
    // için o ekranla aynı yetki sınırını korur: yalnızca Müdür görür. SYB/Personel'de bu
    // kutu tamamen gizlenir.
    public bool ShowRecentActivity => !IsLoading && _currentUser.Role == UserRole.Mudur;

    // Bir durum kartına tıklandığında (filtre anahtarıyla) fırlatılır; ShellViewModel
    // bunu dinleyip kullanıcıyı ilgili listeye o filtre uygulanmış şekilde yönlendirir.
    public event Action<string>? FilterCardClicked;

    // Bir hızlı aksiyon butonuna tıklandığında (nav anahtarıyla) fırlatılır.
    public event Action<string>? QuickActionClicked;

    // "Yaklaşan Bitişler" listesinden bir sözleşmeye tıklandığında fırlatılır.
    public event Action<Contract>? UpcomingContractClicked;

    public DashboardViewModel() : this(null!, new User()) { } // tasarımcı önizlemesi için

    public DashboardViewModel(ContractService contractService, User currentUser)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        QuickActions = new ObservableCollection<DashboardQuickActionItem>(BuildQuickActions(currentUser.Role));
        _ = LoadAsync();
    }

    private static List<DashboardQuickActionItem> BuildQuickActions(UserRole role) => role switch
    {
        UserRole.Personel => new List<DashboardQuickActionItem>
        {
            new("+ Yeni Sözleşme Talebi", "yeniTalep"),
        },
        UserRole.SYB => new List<DashboardQuickActionItem>
        {
            new("+ Sözleşme Yarat", "sozlesmeYarat"),
            new("Son Kontrol (SYB)", "sozlesmeKontrol"),
        },
        UserRole.Mudur => new List<DashboardQuickActionItem>
        {
            new("Onay Bekleyenler", "onayBekleyen"),
        },
        _ => new List<DashboardQuickActionItem>()
    };

    private async Task LoadAsync()
    {
        try
        {
            var statsTask = _contractService.GetDashboardStatsAsync(_currentUser);
            var upcomingTask = _contractService.GetUpcomingEndingsAsync(_currentUser);
            // Yalnızca Müdür'e gösterileceği için, diğer rollerde gereksiz sorgu atılmasın.
            var activityTask = _currentUser.Role == UserRole.Mudur
                ? _contractService.GetRecentActivityAsync(_currentUser)
                : Task.FromResult(new List<AuditLog>());

            await Task.WhenAll(statsTask, upcomingTask, activityTask);

            var stats = statsTask.Result;
            Aktif = stats.Aktif;
            OnayBekliyor = stats.OnayBekliyor;
            Uyari = stats.Uyari;
            Ihlal = stats.Ihlal;

            UpcomingEndings = new ObservableCollection<DashboardUpcomingRowViewModel>(
                upcomingTask.Result.Select(c => new DashboardUpcomingRowViewModel(c)));

            RecentActivity = new ObservableCollection<AuditLogRowViewModel>(
                activityTask.Result.Select(a => new AuditLogRowViewModel(a)));
        }
        catch (Exception ex)
        {
            ErrorMessage = "Panel yüklenemedi: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void FilterCard(string filterKey) => FilterCardClicked?.Invoke(filterKey);

    [RelayCommand]
    private void QuickAction(string navKey) => QuickActionClicked?.Invoke(navKey);

    [RelayCommand]
    private void OpenUpcoming(DashboardUpcomingRowViewModel row) => UpcomingContractClicked?.Invoke(row.RawContract);
}