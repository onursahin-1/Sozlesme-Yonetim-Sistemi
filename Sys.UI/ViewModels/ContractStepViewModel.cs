using System;
using System.Collections.Generic;
using System.Linq;
using Sys.Domain;

namespace Sys.UI.ViewModels;

public enum ContractStepState
{
    Completed,  // gerçekleşmiş ve olumlu sonuçlanmış adım
    Current,    // sözleşmenin şu anki durumu
    Pending,    // henüz sırası gelmemiş adım
    Rejected    // reddedilmiş / olumsuz sonuçlanmış adım
}

// Sözleşme detayındaki tek bir süreç adımı.
//
// Bu liste eskiden iki ayrı bölümdü: "Süreç Adımları" (Status/Stage alanlarından
// TÜRETİLEN bir tahmin) ve "Aşama Geçmişi" (ApprovalLog kayıtlarının düz listesi).
// İkisi aynı onay zincirini anlatıyor ama farklı kaynaklardan beslendiği için
// çelişebiliyorlardı — örneğin reddedilmiş bir fesih talebi geçmişte görünüyor,
// türetilmiş adımlarda hiç görünmüyordu. Artık tek bir zaman çizelgesi var ve
// tamamı GERÇEK kayıtlardan besleniyor.
public class ContractStepViewModel
{
    public ContractStepViewModel(string label, ContractStepState state, DateTime? date = null, string? note = null)
    {
        Label = label;
        State = state;
        Date = date;
        Note = note;
    }

    public string Label { get; }
    public ContractStepState State { get; }
    public DateTime? Date { get; }
    public string? Note { get; }

    public bool HasNote => !string.IsNullOrWhiteSpace(Note);
    public bool HasDate => Date.HasValue;
    public string DateText => Date?.ToString("dd.MM.yyyy HH:mm") ?? string.Empty;

    public string Icon => State switch
    {
        ContractStepState.Completed => "✓",
        ContractStepState.Current => "●",
        ContractStepState.Rejected => "✕",
        _ => "○"
    };

    public string IconColorHex => State == ContractStepState.Pending ? "TextDisabledAlt" : "TextOnAccent";

    public string IconBgHex => State switch
    {
        ContractStepState.Completed => "SuccessBrightSolid",
        ContractStepState.Current => "AccentSolid",
        ContractStepState.Rejected => "DangerSolid",
        // Bekleyen adım: daire zeminle kaynaşsın, yalnızca çerçevesi görünsün.
        _ => "SurfaceCard"
    };

    public string IconBorderHex => State switch
    {
        ContractStepState.Completed => "SuccessBrightSolid",
        ContractStepState.Current => "AccentSolid",
        ContractStepState.Rejected => "DangerSolid",
        _ => "TextDisabledAlt"
    };

    // Geçmiş adımlar soluk, içinde bulunulan adım koyu ve kalın, sıradakiler gri.
    public string LabelColorHex => State switch
    {
        ContractStepState.Completed => "TextBodyAlt",
        ContractStepState.Current => "TextPrimary",
        ContractStepState.Rejected => "DangerBase",
        _ => "TextFaint"
    };

    public string LabelWeight => State == ContractStepState.Current ? "Bold" : "Normal";

    // Adımlar arası dikey bağlayıcı; gerçekleşmiş adımlarda renkli kalır.
    public string ConnectorColorHex => State switch
    {
        ContractStepState.Completed => "SuccessBright",
        ContractStepState.Rejected => "DangerBase",
        _ => "BorderButton"
    };

    // --- Zaman çizelgesinin kurulması ---
    //
    // 1) "Talep oluşturuldu"          — sözleşmenin CreatedAt tarihinden
    // 2) Her ApprovalLog kaydı        — adım adı, onay/red, tarih, not
    // 3) Sözleşmenin bugünkü durumu   — "Yürürlükte", "Onay bekliyor" vb.
    public static List<ContractStepViewModel> Build(Contract contract)
    {
        var steps = new List<ContractStepViewModel>
        {
            new("Talep oluşturuldu", ContractStepState.Completed, contract.CreatedAt)
        };

        foreach (var log in contract.ApprovalLogs.OrderBy(l => l.ActionDate).ThenBy(l => l.Id))
        {
            var approved = log.Decision == ApprovalDecision.Onay;
            steps.Add(new ContractStepViewModel(
                $"{log.StepName} — {(approved ? "Onaylandı" : "Reddedildi")}",
                approved ? ContractStepState.Completed : ContractStepState.Rejected,
                log.ActionDate,
                log.Note));
        }

        steps.Add(BuildCurrentStateStep(contract));
        return steps;
    }

    // Zaman çizelgesi kırmızı bir "Reddedildi" adımıyla bitip hemen altında
    // "Yürürlükte" yazınca çelişki gibi okunuyordu. Oysa reddedilen şey SÖZLEŞME değil,
    // onun üzerinde açılan düzenleme/fesih talebi; sözleşme talep öncesi haliyle
    // yürürlükte kalmaya devam ediyor. Bu satır o bağlantıyı açıkça kuruyor.
    private static string? LastRejectionExplanation(Contract contract)
    {
        var lastLog = contract.ApprovalLogs
            .OrderByDescending(l => l.ActionDate)
            .ThenByDescending(l => l.Id)
            .FirstOrDefault();

        if (lastLog is null || lastLog.Decision != ApprovalDecision.Red) return null;

        if (lastLog.StepName.Contains("Düzenleme", StringComparison.OrdinalIgnoreCase))
            return "Düzenleme talebi reddedildi; sözleşme değişiklik öncesi değerleriyle yürürlükte kaldı.";

        if (lastLog.StepName.Contains("Fesih", StringComparison.OrdinalIgnoreCase))
            return "Fesih talebi reddedildi; sözleşme yürürlükte kalmaya devam ediyor.";

        return null;
    }

    // Son düğüm: sözleşmenin şu anki hali. Onay sürecindeyse kimin onayının
    // beklendiğini de yazar ki kullanıcı topu kimde olduğunu görsün.
    private static ContractStepViewModel BuildCurrentStateStep(Contract contract)
    {
        switch (contract.Status)
        {
            case ContractStatus.Talep:
                return contract.WasRejected
                    ? new ContractStepViewModel("Düzeltme bekleniyor", ContractStepState.Current, null, contract.LastRejectionNote)
                    : new ContractStepViewModel("Sözleşme hazırlanmayı bekliyor (SYB)", ContractStepState.Current);

            case ContractStatus.OnayBekliyor:
                var bekleyen = contract.Stage switch
                {
                    1 => "SYB son kontrolü bekleniyor",
                    2 => "Yönetim (YK) onayı bekleniyor",
                    _ => "Onay bekleniyor"
                };
                var ek = contract.PendingTermination ? " (fesih talebi)"
                    : contract.PendingEdit ? " (düzenleme talebi)"
                    : string.Empty;
                return new ContractStepViewModel(bekleyen + ek, ContractStepState.Current);

            case ContractStatus.Aktif:
                return new ContractStepViewModel("Yürürlükte", ContractStepState.Current, null, LastRejectionExplanation(contract));

            case ContractStatus.Uyari:
                return new ContractStepViewModel("Yürürlükte — bitiş tarihi yaklaşıyor", ContractStepState.Current,
                    null, LastRejectionExplanation(contract));

            case ContractStatus.Ihlal:
                return new ContractStepViewModel("Yürürlükte — ihlal bildirimi var", ContractStepState.Rejected,
                    null, LastRejectionExplanation(contract));

            case ContractStatus.Tamamlandi:
                return new ContractStepViewModel("Süresi doldu — tamamlandı", ContractStepState.Completed);

            case ContractStatus.Feshedildi:
                return new ContractStepViewModel("Feshedildi", ContractStepState.Rejected);

            // Talep hiç sözleşmeye dönüşmeden kapatıldı; süreç burada bitiyor.
            case ContractStatus.Reddedildi:
                return new ContractStepViewModel("Talep reddedildi — kapatıldı",
                    ContractStepState.Rejected, contract.LastRejectedAt, contract.LastRejectionNote);

            default:
                return new ContractStepViewModel(contract.Status.ToString(), ContractStepState.Current);
        }
    }
}
