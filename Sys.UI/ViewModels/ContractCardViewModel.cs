using System;
using Sys.Domain;
using Sys.Services;
using Sys.UI.Localization;

namespace Sys.UI.ViewModels;

public class ContractCardViewModel
{
    private readonly Contract _contract;
    private readonly bool _isSyb;

    // Kart, kullanıcının KENDİSİNİ alır; rol ve kimlik ayrı ayrı geçilmez.
    //
    // Eskiden liste ekranı buraya "editableUserId" adında bir sayı geçiyordu ve bu
    // sayı Personel dışındaki rollerde 0'dı — çünkü tek işi "listeden düzenleyebilir
    // mi" sorusuydu. Sonradan aynı alana "bu talep bana mı ait" sorusu da bağlanınca
    // SYB için cevap her zaman "hayır" çıktı: kendi talebi "başkasının talebi" gibi
    // görünüyordu. Bir tanımlayıcıya ikinci bir rol yüklemenin bilinen sonucu.
    public ContractCardViewModel(Contract contract, User? currentUser = null)
    {
        _contract = contract;
        _isSyb = currentUser?.Role == UserRole.SYB;

        IsOwnRequest = currentUser is not null && contract.CreatedByUserId == currentUser.Id;

        // Listeden talep düzenleme yalnızca Personel'e açık. SYB'nin kendi talebinde
        // karşılığı "Sözleşme Yarat" akışı; oradaki sihirbaz zaten tüm alanları alıyor.
        IsEditable = IsOwnRequest
            && currentUser!.Role == UserRole.Personel
            && contract.Status == ContractStatus.Talep;

        CanRenew = currentUser is not null && ContractService.CanRenew(contract, currentUser);
    }

    public Contract RawContract => _contract;
    public bool IsEditable { get; }

    // Yürürlükteki veya süresi dolmuş bir sözleşmeden yeni dönem talebi açılabilir.
    // Feshedilen sözleşme yenilenemez: fesih, tarafların ilişkiyi sürdürmeme kararıdır;
    // yeniden çalışılacaksa bu sıfırdan değerlendirilmesi gereken yeni bir karardır.
    //
    // Yalnızca DETAY ekranlarında kullanılır. Liste kartlarında bilinçli olarak yok:
    // yenileme, önceki dönemin kalemlerine ve koşullarına bakılarak verilen bir karar;
    // listede tek satır bilgiyle başlatılması doğru olmaz. Ayrıca kart üzerindeki
    // buton sayısını da artırırdı.
    public bool CanRenew { get; }

    // SYB rolündeki kullanıcı için: bu kart "Sözleşme Yarat" işlemini mi bekliyor?
    public bool NeedsContractCreation => _isSyb && Status == ContractStatus.Talep;

    // Sözleşmeye dönüşmemiş bir talep, SYB tarafından doğrudan reddedilebilir.
    // Eskiden tek yol talebi önce sözleşmeye çevirip Son Kontrol'de reddetmekti;
    // bu hem gereksiz veri girişi hem de boşa yanan bir sözleşme numarası demekti.
    public bool CanRejectRequest => _isSyb && Status == ContractStatus.Talep;

    // Aynı işlem, talep kime aitse ona göre farklı bir eylemdir: başkasının talebini
    // reddetmek bir KARAR, kendi talebini geri çekmek bir VAZGEÇME. SYB kendi talebini
    // de işleyebildiği için ikisi tek butonda toplanıyordu ve ekranda "kendi talebini
    // reddet" gibi tuhaf bir iş görünüyordu.
    public bool IsOwnRequest { get; }
    public string RejectRequestLabel => Strings.T(IsOwnRequest ? "Card.WithdrawRequest" : "Card.RejectRequest");

    // SYB rolündeki kullanıcı için: bu kart "Son Kontrol" (aşama 1 onayı) işlemini mi bekliyor?
    public bool NeedsSybSonKontrol => _isSyb && Status == ContractStatus.OnayBekliyor && Stage == 1;

    public int Id => _contract.Id;
    public string Title => _contract.Title;
    public string CompanyName => _contract.CompanyName;
    public string RequestRefNo => _contract.RequestRefNo;
    public string RefNoText => string.IsNullOrEmpty(_contract.ContractNo) ? _contract.RequestRefNo : _contract.ContractNo!;
    public string Type => _contract.Type;
    public ContractStatus Status => _contract.Status;

    public int Stage => _contract.Stage;

    public string StatusLabel => IsReturned
        ? ReturnBadgeText
        : Status switch
        {
            ContractStatus.Talep => Strings.T("Card.StatusRequest"),
            ContractStatus.OnayBekliyor => Strings.T("Status.PendingApproval") + StageDetail,
            ContractStatus.Aktif => Strings.T("Status.Live"),
            ContractStatus.Uyari => Strings.T("Card.StatusExpiring"),
            ContractStatus.Ihlal => Strings.T("Card.StatusInViolation"),
            ContractStatus.Tamamlandi => Strings.T("Card.StatusCompleted"),
            ContractStatus.Feshedildi => Strings.T("Card.StatusTerminated"),
            ContractStatus.Reddedildi => Strings.T("Card.StatusRejected"),
            _ => Status.ToString()
        };

    // İade: talep sahibine geri döndü, düzeltilip yeniden gönderilebilir.
    public bool IsReturned => Status == ContractStatus.Talep && _contract.WasRejected;

    // Aynı görünüm (Talep + reddedilmiş) ÜÇ farklı olaydan doğabiliyor: SYB'nin
    // talebi sahibine iade etmesi, Son Kontrol'ü atlanmış bir sözleşmeyi Müdür'ün
    // geri göndermesi, ya da kişinin kendi talebini geri çekmesi. Kullanıcıya
    // söylenecek cümle üçünde de farklı.
    //
    // Ayrımı LastRejectedStage yapıyor. Bunu önce FinalCheckSkipped'tan çıkarmaya
    // çalışmıştım — yanlıştı: o alan "Son Kontrol atlandı mı" sorusuna ait, "reddi
    // kim verdi" sorusuna değil. Kendi talebini geri çeken SYB'ye "Yönetim onayından
    // döndü" yazıyordu.
    public bool IsReturnedFromManagement => IsReturned && _contract.LastRejectedStage == 2;

    // Kişinin kendi geri çekmesi: red aşama 0'dan geldi ve talep zaten onun.
    public bool IsSelfWithdrawn => IsReturned && _contract.LastRejectedStage == 0 && IsOwnRequest;

    public string ReturnBadgeText => Strings.T(
        IsReturnedFromManagement ? "Card.BadgeFromManagement"
        : IsSelfWithdrawn ? "Card.BadgeWithdrawn"
        : "Card.BadgeReturned");

    // Kapatma: talep nihai olarak reddedildi, yeniden gönderilemez.
    public bool IsClosedRejected => Status == ContractStatus.Reddedildi;

    // Müdür (YK) reddi: sözleşme onay zincirinden çıkmadı, SYB Son Kontrol'e geri
    // döndü. Kart durumu "Onay Bekliyor" olarak kalır ama SYB'nin bunun ikinci bir
    // inceleme olduğunu ve neden geri geldiğini görmesi gerekir.
    public bool IsSentBackToSyb =>
        Status == ContractStatus.OnayBekliyor && Stage == 1 && _contract.WasRejected;

    // Gerekçe kutusu üç durumda da gösterilir.
    public bool IsRejected => IsReturned || IsClosedRejected || IsSentBackToSyb;

    public string RejectionNoteText
    {
        get
        {
            var note = string.IsNullOrWhiteSpace(_contract.LastRejectionNote)
                ? Strings.T("Card.NoReason")
                : _contract.LastRejectionNote;

            if (IsClosedRejected)
                return Strings.T("Card.NoteClosed", note);

            // Aynı olay, bakan kişiye göre farklı bir şey söyler. SYB için bu bir
            // İŞ ("Son Kontrol tekrar yapılmalı"); talebi açan Personel için bir
            // HABER — onun yapacağı bir şey yok, sözleşme hâlâ SYB'nin elinde.
            // Herkese aynı cümleyi göstermek, Personel'e üstlenemeyeceği bir görev
            // veriyordu.
            if (IsSentBackToSyb)
                return Strings.T(_isSyb ? "Card.NoteBackToFinalCheck" : "Card.NoteBackInfo", note);

            if (IsReturnedFromManagement)
                return Strings.T("Card.NoteFixAndResend", note);

            if (IsSelfWithdrawn)
                return Strings.T("Card.NoteWithdrawn", note);

            return Strings.T("Card.NoteReturned", note);
        }
    }

    private string StageDetail
    {
        get
        {
            var who = Stage switch
            {
                1 => Strings.T("Card.StageFinalCheck"),
                2 => Strings.T("Card.StageManagement"),
                _ => null
            };

            if (who is null) return string.Empty;

            return _contract.PendingTermination
                ? $" ({who} — {Strings.T("Card.StageTermination")})"
                : $" ({who})";
        }
    }

    public string StatusColorHex => IsRejected ? "DangerBase" : Status switch
    {
        ContractStatus.Aktif => "SuccessBase",
        ContractStatus.OnayBekliyor => "AccentBase",
        ContractStatus.Uyari => "WarningBase",
        ContractStatus.Ihlal => "DangerBase",
        ContractStatus.Tamamlandi => "TextFaint",
        ContractStatus.Feshedildi => "DangerBase",
        ContractStatus.Reddedildi => "DangerBase",
        _ => "TextLabel"
    };

    public string StatusBgHex => IsRejected ? "DangerSoftBg" : Status switch
    {
        ContractStatus.Aktif => "SuccessSoftBg",
        ContractStatus.OnayBekliyor => "AccentSoftBorder",
        ContractStatus.Uyari => "WarningSoftBg",
        ContractStatus.Ihlal => "DangerSoftBg",
        ContractStatus.Tamamlandi => "SurfaceDivider",
        ContractStatus.Feshedildi => "DangerSoftBg",
        ContractStatus.Reddedildi => "DangerSoftBg",
        _ => "SurfaceDivider"
    };

    public string BedelText => CurrencyHelper.Format(_contract.TotalAmount, _contract.Currency);

    // Arşiv listesinde her kaydın YANINDA hangi tarihte kapandığı yazsın diye:
    // sona eren/feshedilen sözleşmede bitiş tarihi, reddedilen talepte red tarihi
    // anlamlı olan bilgidir (reddedilen talebin bitiş tarihi genelde hiç girilmemiştir).
    public string ArchiveDateText
    {
        get
        {
            if (Status == ContractStatus.Reddedildi)
                return _contract.LastRejectedAt is { } rejectedAt
                    ? Strings.T("Card.RejectedOn") + rejectedAt.ToString("dd.MM.yyyy")
                    : Strings.T("Card.StatusRejected");

            return _contract.EndDate is { } endDate
                ? Strings.T("Card.EndsOn") + endDate.ToString("dd.MM.yyyy")
                : Strings.T("Card.NoDate");
        }
    }

    public string GunKalanText
    {
        get
        {
            if (_contract.EndDate is null) return "-";
            var days = (_contract.EndDate.Value.Date - DateTime.Today).Days;
            return days > 0 ? Strings.T("Card.DaysLeft", days) : Strings.T("Term.Ended");
        }
    }
}