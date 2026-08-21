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

// Önümüzdeki dönemde biten sözleşme sayıları. Kümülatif değil, aralık başına:
// 0-30, 31-60, 61-90 gün.
public class EndingCalendar
{
    public int Within30 { get; set; }
    public int Within60 { get; set; }
    public int Within90 { get; set; }
    public int Total => Within30 + Within60 + Within90;
}

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

public class DashboardSummary
{
    public int Aktif { get; set; }
    public int OnayBekliyor { get; set; }
    public int Uyari { get; set; }
    public int Ihlal { get; set; }

    public List<PendingWorkItem> PendingWork { get; set; } = new();
    public List<CurrencyTotal> ActiveValue { get; set; } = new();
    public MonthlyStats ThisMonth { get; set; } = new();
    public EndingCalendar Endings { get; set; } = new();
    public List<TypeCount> TypeBreakdown { get; set; } = new();

    public List<Contract> UpcomingEndings { get; set; } = new();
    public List<AuditLog> RecentActivity { get; set; } = new();
}
