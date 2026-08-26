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

    public DashboardUpcomingRowViewModel(UpcomingEndingItem item)
    {
        _contract = item.Contract;
        IsRenewed = item.IsRenewed;
    }

    // Bu sözleşme için zaten bir yenileme talebi açılmış mı? Eskiden liste bunu
    // söylemiyordu; SYB aynı sözleşmeyi her gün görüp "bunu yenilemiş miydik"
    // diye tek tek kontrol etmek zorundaydı.
    // Rozet yalnızca yenilenmişlerde gösteriliyor; "Yenilenmedi" etiketi listedeki
    // her satırda belirip gürültü yapıyordu. Rozetin yokluğu zaten yeterli.
    public bool IsRenewed { get; }

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

// "Sizi bekleyen işler" listesindeki bir satır.
public class PendingWorkRowViewModel
{
    private readonly PendingWorkItem _item;

    public PendingWorkRowViewModel(PendingWorkItem item) => _item = item;

    public string Title => _item.Title;
    public string Subtitle => _item.Subtitle;
    public string CountText => _item.Count.ToString();
    public string NavKey => _item.NavKey;
    public string ColorHex => _item.ColorHex;

    // "En eski 21 gündür bekliyor". Bilgi yoksa hiç gösterilmez — uydurulmuş bir
    // "0 gün" yazmaktansa satırı boş bırakmak doğru.
    public bool HasWaitingInfo => _item.OldestWaitingDays is not null;

    public string WaitingText => _item.OldestWaitingDays switch
    {
        null => string.Empty,
        0 => "Bugün geldi",
        1 => "En eskisi 1 gündür bekliyor",
        var d => $"En eskisi {d} gündür bekliyor"
    };

    // Bir haftayı aşan bekleme dikkat çekmeli; altındakiler nötr kalır.
    public string WaitingColorHex => _item.OldestWaitingDays >= 7 ? "#A32D2D" : "#8A94A6";
}

// Para birimi başına toplam değer satırı.
public class CurrencyTotalRowViewModel
{
    public CurrencyTotalRowViewModel(CurrencyTotal total)
    {
        AmountText = CurrencyHelper.Format(total.Amount, total.Currency);
        Label = CurrencyHelper.Label(total.Currency);
        CountText = $"{total.ContractCount} sözleşme";
    }

    public string AmountText { get; }
    public string Label { get; }
    public string CountText { get; }
}

// Tür dağılımındaki bir satır. Çubuk genişliği en yüksek değere göre oranlanır.
public class TypeBreakdownRowViewModel
{
    public TypeBreakdownRowViewModel(TypeCount item, int maxCount)
    {
        Type = item.Type;
        CountText = item.Count.ToString();
        // 0'a bölünmeyi önlemek için en az 1; oran 0..1 aralığında tutulur.
        BarRatio = maxCount <= 0 ? 0 : (double)item.Count / maxCount;
    }

    public string Type { get; }
    public string CountText { get; }
    public double BarRatio { get; }
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
    [NotifyPropertyChangedFor(nameof(IhlalSubtitle))]
    public partial int Ihlal { get; set; }

    // Kart artık sözleşme değil AÇIK İHLAL sayısını gösteriyor: bir sözleşmede
    // birden fazla açık ihlal olabilir ve kart "1" derken üç iş bekliyor olabilir.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IhlalSubtitle))]
    public partial int OpenViolations { get; set; }

    // Alt satır, KARTTAKİ sayının neyi saydığını açıkça yazıyor. "3 ihlal" ile
    // "3 sözleşmede ihlal" farklı şeyler; kart ihlal adedini gösterdiği için
    // sözleşme sayısı burada belirtiliyor.
    public string IhlalSubtitle => OpenViolations == 0
        ? "açık ihlal yok"
        : $"{Ihlal} sözleşmede, giderilmeyi bekliyor";

    [ObservableProperty]
    public partial ObservableCollection<DashboardUpcomingRowViewModel> UpcomingEndings { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<AuditLogRowViewModel> RecentActivity { get; set; } = new();

    // --- Sizi bekleyen işler ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingWork))]
    public partial ObservableCollection<PendingWorkRowViewModel> PendingWork { get; set; } = new();

    public bool HasPendingWork => PendingWork.Count > 0;

    // --- Toplam sözleşme değeri (para birimi başına) ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveValue))]
    public partial ObservableCollection<CurrencyTotalRowViewModel> ActiveValue { get; set; } = new();

    public bool HasActiveValue => ActiveValue.Count > 0;

    // --- Bu ay ---
    [ObservableProperty]
    public partial string MonthLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int MonthNewRequests { get; set; }

    [ObservableProperty]
    public partial int MonthActivated { get; set; }

    [ObservableProperty]
    public partial int MonthTerminated { get; set; }

    // --- Bitiş takvimi ---
    // NOT: Burada bitiş takvimi (Ending30/60/90) alanları vardı. Panelde
    // "Bitiş Uyarısı" kartı ve "Yaklaşan Bitişler" listesi zaten aynı bilgiyi
    // veriyordu; takvim üçüncü tekrardı.

    // --- Tür dağılımı ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTypeBreakdown))]
    public partial ObservableCollection<TypeBreakdownRowViewModel> TypeBreakdown { get; set; } = new();

    public bool HasTypeBreakdown => TypeBreakdown.Count > 0;

    // Müdür'de genel bir sözleşme listesi ekranı yok, dolayısıyla tıklamanın
    // gideceği bir yer de yok. Durum kartlarındaki desenin aynısı: kutu görünür
    // kalır ama tıklanamaz — tıklanıp hiçbir şey olmaması daha kötü olurdu.
    public bool TypeRowsClickable => _currentUser.Role is UserRole.Personel or UserRole.SYB;

    // --- Karşılama başlığı ---
    // Günün saatine göre selam; küçük bir dokunuş ama panelin "kişisel" hissini veriyor.
    public string GreetingText
    {
        get
        {
            // Ad soyad tam olarak yazılıyor. "Bey/Hanım" hitabı kullanılmıyor: sistemde
            // cinsiyet bilgisi yok, tahmin etmek yanlış hitaba yol açardı.
            var fullName = (_currentUser.FullName ?? string.Empty).Trim();
            var greeting = GreetingForHour(DateTime.Now.Hour);
            return string.IsNullOrWhiteSpace(fullName) ? greeting : $"{greeting}, {fullName}";
        }
    }

    // Türkçedeki yaygın kullanım:
    //   05:00 – 10:59  Günaydın      (sabah)
    //   11:00 – 16:59  İyi günler    (gündüz)
    //   17:00 – 21:59  İyi akşamlar  (akşam)
    //   22:00 – 04:59  İyi geceler   (gece)
    // "Tünaydın" bilinçli olarak kullanılmadı: öğleden sonraya karşılık gelse de
    // günlük dilde neredeyse terk edilmiş, kurumsal bir arayüzde tuhaf duruyor.
    private static string GreetingForHour(int hour) => hour switch
    {
        >= 5 and < 11 => "Günaydın",
        >= 11 and < 17 => "İyi günler",
        >= 17 and < 22 => "İyi akşamlar",
        _ => "İyi geceler"
    };

    public string TodayText => DateTime.Now.ToString("d MMMM yyyy, dddd",
        System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));

    // Değer ve istatistik kutuları yalnızca genel görünürlüğü olan rollerde anlamlı;
    // Personel yalnızca kendi taleplerini gördüğü için "toplam portföy" bilgisi
    // onun için yanıltıcı olurdu.
    public bool ShowPortfolioStats => _currentUser.Role is UserRole.SYB or UserRole.Mudur;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRecentActivity))]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

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

    // Tür dağılımından bir türe tıklandığında sözleşme listesi o türe filtrelenmiş
    // olarak açılır. Eskiden kutu yalnızca sayı gösteriyordu ve hiçbir aksiyona
    // bağlanmıyordu.
    public event Action<string>? TypeClicked;

    public DashboardViewModel() : this(null!, new User()) { } // tasarımcı önizlemesi için

    public DashboardViewModel(ContractService contractService, User currentUser)
    {
        _contractService = contractService;
        _currentUser = currentUser;
        _ = LoadAsync();
    }


    private async Task LoadAsync()
    {
        try
        {
            // Panelin tamamı tek servis çağrısıyla doluyor; kutu başına ayrı sorgu
            // atmak ekranın parça parça dolmasına yol açardı.
            var summary = await _contractService.GetDashboardSummaryAsync(_currentUser);

            Aktif = summary.Aktif;
            OnayBekliyor = summary.OnayBekliyor;
            Uyari = summary.Uyari;
            Ihlal = summary.Ihlal;
            OpenViolations = summary.OpenViolations;

            PendingWork = new ObservableCollection<PendingWorkRowViewModel>(
                summary.PendingWork.Select(p => new PendingWorkRowViewModel(p)));

            ActiveValue = new ObservableCollection<CurrencyTotalRowViewModel>(
                summary.ActiveValue.Select(v => new CurrencyTotalRowViewModel(v)));

            MonthLabel = DateTime.Now.ToString("MMMM yyyy",
                System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
            MonthNewRequests = summary.ThisMonth.NewRequests;
            MonthActivated = summary.ThisMonth.Activated;
            MonthTerminated = summary.ThisMonth.Terminated;

            var maxTypeCount = summary.TypeBreakdown.Count == 0 ? 0 : summary.TypeBreakdown.Max(x => x.Count);
            TypeBreakdown = new ObservableCollection<TypeBreakdownRowViewModel>(
                summary.TypeBreakdown.Take(6).Select(x => new TypeBreakdownRowViewModel(x, maxTypeCount)));

            UpcomingEndings = new ObservableCollection<DashboardUpcomingRowViewModel>(
                summary.UpcomingEndings.Select(c => new DashboardUpcomingRowViewModel(c)));

            RecentActivity = new ObservableCollection<AuditLogRowViewModel>(
                summary.RecentActivity.Select(a => new AuditLogRowViewModel(a)));
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

    [RelayCommand]
    private void OpenType(TypeBreakdownRowViewModel row) => TypeClicked?.Invoke(row.Type);

    // Bekleyen iş satırına tıklandığında ilgili ekrana götürür; hızlı aksiyonlarla
    // aynı yönlendirme mekanizmasını kullanır.
    [RelayCommand]
    private void OpenPendingWork(PendingWorkRowViewModel row) => QuickActionClicked?.Invoke(row.NavKey);
}