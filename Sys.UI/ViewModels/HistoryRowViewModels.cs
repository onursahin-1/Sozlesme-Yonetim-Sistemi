using System;
using Sys.Domain;

namespace Sys.UI.ViewModels;

// Revizyon ve fesih kayıtlarını, SONUCU görünür olacak şekilde saran satır
// görünüm modelleri.
//
// Bu kayıtlar birer TALEP; onay zincirinden geçtikten sonra gerçekleşebilir ya da
// reddedilebilirler. Sonuç alanı eklenmeden önce ekranda ayrım yoktu: reddedilmiş
// bir bedel değişikliği de "Revizyon Geçmişi"nde, reddedilmiş bir fesih talebi de
// "Fesih Geçmişi"nde gerçekleşmiş gibi duruyordu.

// Üç durumun ortak görünüm kuralları tek yerde.
internal static class OutcomeStyle
{
    public static string Label(bool? isApproved) => isApproved switch
    {
        true => "Onaylandı",
        false => "Reddedildi",
        // Bu alanlar eklenmeden önce oluşmuş kayıtlarda sonuç bilinmiyor; "Onay
        // bekliyor" demek yanıltıcı olurdu, açıkça belirsiz olduğunu yazıyoruz.
        _ => "Sonuç bekliyor"
    };

    public static string ColorHex(bool? isApproved) => isApproved switch
    {
        true => "#1A6B2A",
        false => "#A32D2D",
        _ => "#B06A00"
    };

    public static string BgHex(bool? isApproved) => isApproved switch
    {
        true => "#E6F4E7",
        false => "#FDECEA",
        _ => "#FFF3CD"
    };

    // Kayıt kartının zemini: reddedilenler soluk kırmızı, onaylananlar nötr.
    // Amaç, listeyi tararken gerçekleşmemiş kayıtların hemen ayırt edilmesi.
    public static string CardBgHex(bool? isApproved) => isApproved switch
    {
        false => "#FDF6F5",
        _ => "#FBFCFE"
    };

    public static string CardBorderHex(bool? isApproved) => isApproved switch
    {
        false => "#F2DAD5",
        _ => "#E9EEF5"
    };
}

public class RevisionRowViewModel
{
    private readonly ContractRevision _revision;
    private readonly string _currencySuffix;

    public RevisionRowViewModel(ContractRevision revision, string currencySuffix)
    {
        _revision = revision;
        _currencySuffix = currencySuffix;
    }

    public string ChangeType => _revision.ChangeType;
    public string Reason => _revision.Reason;
    public bool HasReason => !string.IsNullOrWhiteSpace(_revision.Reason);

    public string OutcomeLabel => OutcomeStyle.Label(_revision.IsApproved);
    public string OutcomeColorHex => OutcomeStyle.ColorHex(_revision.IsApproved);
    public string OutcomeBgHex => OutcomeStyle.BgHex(_revision.IsApproved);
    public string CardBgHex => OutcomeStyle.CardBgHex(_revision.IsApproved);
    public string CardBorderHex => OutcomeStyle.CardBorderHex(_revision.IsApproved);

    public string ChangedAtText => _revision.ChangedAt.ToString("dd.MM.yyyy HH:mm");

    public string PreviousAmountText =>
        "Önceki bedel: " + _revision.PreviousTotalAmount.ToString("N2") + _currencySuffix;

    public bool HasPreviousEndDate => _revision.PreviousEndDate.HasValue;
    public string PreviousEndDateText =>
        "Önceki bitiş: " + (_revision.PreviousEndDate?.ToString("dd.MM.yyyy") ?? "-");

    // Kaydedilen ama hiçbir ekranda gösterilmeyen alanlar artık burada.
    public bool HasPreviousCompany => !string.IsNullOrWhiteSpace(_revision.PreviousCompanyName);
    public string PreviousCompanyText => "Önceki firma: " + _revision.PreviousCompanyName;

    public bool HasPreviousPaymentPeriod => !string.IsNullOrWhiteSpace(_revision.PreviousPaymentPeriod);
    public string PreviousPaymentPeriodText => "Önceki ödeme koşulu: " + _revision.PreviousPaymentPeriod;
}

// İhlal kayıtları veritabanına yazılıyordu ama HİÇBİR ekranda gösterilmiyordu:
// sözleşme "İhlal Mevcut" durumuna geçiyor, ancak ihlalin ne olduğu, ne zaman
// yaşandığı ve nasıl açıklandığı hiçbir yerden okunamıyordu.
//
// Revizyon/fesih kayıtlarının aksine ihlalin onay süreci yok — bildirim anında
// yürürlüğe girer, dolayısıyla sonuç alanı da yok.
public class ViolationRowViewModel
{
    private readonly Violation _violation;

    public ViolationRowViewModel(Violation violation, bool canResolve = false)
    {
        _violation = violation;
        CanResolve = canResolve && !violation.IsResolved;
    }

    public Violation RawViolation => _violation;

    public string ViolationType => _violation.ViolationType;
    public string Description => _violation.Description;
    public bool HasDescription => !string.IsNullOrWhiteSpace(_violation.Description);

    public string ViolationDateText => "İhlal tarihi: " + _violation.ViolationDate.ToString("dd.MM.yyyy");
    public string ReportedAtText => _violation.ReportedAt.ToString("dd.MM.yyyy HH:mm");

    // --- Çözüm durumu ---

    public bool IsResolved => _violation.IsResolved;

    // "Giderildi olarak işaretle" butonu yalnızca SYB'ye ve yalnızca açık ihlallerde.
    public bool CanResolve { get; }

    public string StatusLabel => IsResolved ? "Giderildi" : "Açık";
    public string StatusColorHex => IsResolved ? "#1A6B2A" : "#A32D2D";
    public string StatusBgHex => IsResolved ? "#E6F4E7" : "#FDECEA";

    // Giderilmiş ihlaller nötr zeminde; açık olanlar kırmızımsı kalıp dikkat çeker.
    public string CardBgHex => IsResolved ? "#FBFCFE" : "#FDF6F5";
    public string CardBorderHex => IsResolved ? "#E9EEF5" : "#F2DAD5";
    public string TitleColorHex => IsResolved ? "#1A2E4A" : "#8C3220";

    public bool HasResolution => IsResolved && !string.IsNullOrWhiteSpace(_violation.ResolutionNote);

    public string ResolutionText =>
        $"Giderildi ({_violation.ResolvedAt:dd.MM.yyyy HH:mm}): {_violation.ResolutionNote}";
}

public class TerminationRowViewModel
{
    private readonly ContractTermination _termination;

    public TerminationRowViewModel(ContractTermination termination) => _termination = termination;

    public string TerminationType => _termination.TerminationType;
    public string Reason => _termination.Reason;
    public bool HasReason => !string.IsNullOrWhiteSpace(_termination.Reason);

    public string OutcomeLabel => OutcomeStyle.Label(_termination.IsApproved);
    public string OutcomeColorHex => OutcomeStyle.ColorHex(_termination.IsApproved);
    public string OutcomeBgHex => OutcomeStyle.BgHex(_termination.IsApproved);
    public string CardBgHex => OutcomeStyle.CardBgHex(_termination.IsApproved);
    public string CardBorderHex => OutcomeStyle.CardBorderHex(_termination.IsApproved);

    public string RequestedAtText => _termination.RequestedAt.ToString("dd.MM.yyyy HH:mm");
    public string TerminationDateText => "Fesih tarihi: " + _termination.TerminationDate.ToString("dd.MM.yyyy");

    public bool HasCompensation => _termination.CompensationAmount.HasValue;
    public string CompensationText =>
        "Tazminat: " + (_termination.CompensationAmount?.ToString("N2") ?? "-") +
        " TL — " + _termination.CompensationDirection;
}
