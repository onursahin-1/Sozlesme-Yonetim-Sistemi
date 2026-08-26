using Sys.Domain;

namespace Sys.Services;

// Gösterge panelinin ihtiyaç duyduğu özet veri tipleri.
// Panel tek bir çağrıyla dolduruluyor; her kutu için ayrı ayrı servis çağrısı
// yapmak hem yavaş hem de ekranın parça parça dolmasına yol açardı.

// "Sizi bekleyen işler" listesindeki bir satır. Panelin asıl işlevi bu:
// kullanıcının kendi aksiyonunu bekleyen işleri tek yerde göstermek.
public class PendingWorkItem
{
    public PendingWorkItem(string title, string subtitle, int count, string navKey, string colorHex)
    {
        Title = title;
        Subtitle = subtitle;
        Count = count;
        NavKey = navKey;
        ColorHex = colorHex;
    }

    public string Title { get; }
    public string Subtitle { get; }
    public int Count { get; }

    // En eski bekleyen işin kaç gündür beklediği. Adetten daha anlamlı bir metrik:
    // "12 iş bekliyor" ile "12 iş bekliyor, en eskisi 21 gündür" arasında dağlar
    // kadar fark var. Bekleyen yoksa ya da tarih okunamadıysa null.
    public int? OldestWaitingDays { get; init; }

    // Tıklanınca gidilecek sol menü öğesi.
    public string NavKey { get; }
    public string ColorHex { get; }
}

// Para birimi başına toplam. Kur dönüşümü yapılmadığı için tutarlar
// toplanamaz; her para birimi ayrı satır olarak gösterilir.
public class CurrencyTotal
{
    public CurrencyTotal(string currency, decimal amount, int contractCount)
    {
        Currency = currency;
        Amount = amount;
        ContractCount = contractCount;
    }

    public string Currency { get; }
    public decimal Amount { get; }
    public int ContractCount { get; }
}

// Bu ayın hareketleri.
public class MonthlyStats
{
    public int NewRequests { get; set; }
    public int Activated { get; set; }
    public int Terminated { get; set; }
}

// NOT: Burada EndingCalendar (30/60/90 günlük bitiş dilimleri) vardı. Panelde
// "Bitiş Uyarısı" kartı ve "Yaklaşan Bitişler" listesi zaten aynı bilgiyi
// veriyordu; takvim üçüncü tekrardı. 60/90 günlük dilimler de bugün yapılacak
// bir işe karşılık gelmediği için kaldırıldı.

public class TypeCount
{
    public TypeCount(string type, int count)
    {
        Type = type;
        Count = count;
    }

    public string Type { get; }
    public int Count { get; }
}

// "Yaklaşan Bitişler" listesindeki bir sözleşme ve o sözleşmenin yenilenip
// yenilenmediği. Sözleşmenin kendisi yeterli değildi: yenileme bağlantısı ters
// yönde tutuluyor (yeni kayıt eskisini işaret ediyor), bu yüzden ayrıca sorgulanıp
// burada birleştiriliyor.
public class UpcomingEndingItem
{
    public UpcomingEndingItem(Contract contract, bool isRenewed)
    {
        Contract = contract;
        IsRenewed = isRenewed;
    }

    public Contract Contract { get; }
    public bool IsRenewed { get; }
}

public class DashboardSummary
{
    public int Aktif { get; set; }
    public int OnayBekliyor { get; set; }
    public int Uyari { get; set; }
    public int Ihlal { get; set; }

    public List<PendingWorkItem> PendingWork { get; set; } = new();
    public List<CurrencyTotal> ActiveValue { get; set; } = new();
    public MonthlyStats ThisMonth { get; set; } = new();
    public List<TypeCount> TypeBreakdown { get; set; } = new();

    // Açık ihlal ADEDİ (sözleşme sayısı değil).
    public int OpenViolations { get; set; }

    public List<UpcomingEndingItem> UpcomingEndings { get; set; } = new();
    public List<AuditLog> RecentActivity { get; set; } = new();
}
