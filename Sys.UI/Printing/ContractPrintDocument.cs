using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using Sys.Domain;

namespace Sys.UI.Printing;

// Yazdırma için sözleşme künyesini HTML olarak üretir.
//
// NEDEN HTML, PDF DEĞİL?
// Windows'ta varsayılan PDF uygulaması çoğunlukla Edge oluyor ve Edge ".pdf" için
// kabuk "print" fiilini KAYDETMİYOR. Dolayısıyla PDF'i yazdırmaya göndermek
// çalışmıyor, dosya yalnızca bir sekmede açılıyordu.
//
// HTML'de ise sayfanın kendisi yüklenir yüklenmez window.print() çağırıyor: tarayıcı
// yazıcı seçimi, sayfa düzeni ve önizleme içeren kendi yazdırma penceresini açıyor.
// Ek bağımlılık gerektirmeden gerçek yazdırma diyaloğunu veren tek yol bu.
//
// PDF üretimi (ContractPdfExporter) kaldırılmadı; "PDF Kaydet" için kullanılmaya
// devam ediyor. Bu dosya yalnızca ekrandan yazdırma yolunu besliyor.
public static class ContractPrintDocument
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static string BuildHtml(Contract contract)
    {
        var sb = new StringBuilder();

        sb.Append("""
<!DOCTYPE html>
<html lang="tr">
<head>
<meta charset="utf-8">
<title>Sözleşme Künyesi</title>
<style>
  @page { size: A4; margin: 16mm 15mm; }

  :root {
    --ink:      #16233a;
    --muted:    #6b7686;
    --line:     #d8dfe9;
    --accent:   #2d6ea8;
    --band:     #f4f7fb;
  }

  * { box-sizing: border-box; }

  body {
    font-family: "Segoe UI", "Segoe UI Variable Text", Calibri, Arial, sans-serif;
    font-size: 10pt;
    line-height: 1.5;
    color: var(--ink);
    margin: 0;
    -webkit-print-color-adjust: exact;
    print-color-adjust: exact;
  }

  h1 {
    font-size: 17pt;
    font-weight: 700;
    letter-spacing: .5pt;
    color: var(--accent);
    margin: 0 0 2pt;
  }

  h2 {
    font-size: 10.5pt;
    font-weight: 700;
    letter-spacing: .8pt;
    text-transform: uppercase;
    color: var(--accent);
    margin: 20pt 0 7pt;
    padding-bottom: 4pt;
    border-bottom: 1pt solid var(--line);
    /* Bölüm başlığı sayfa sonunda tek başına kalmasın */
    break-after: avoid;
    page-break-after: avoid;
  }

  .head        { border-bottom: 2pt solid var(--accent); padding-bottom: 9pt; margin-bottom: 14pt; }
  .head .meta  { font-size: 8pt; color: var(--muted); }
  .head .title { font-size: 13pt; font-weight: 600; margin-top: 9pt; }
  .head .sub   { font-size: 9pt; color: var(--muted); margin-top: 1pt; }

  .pill {
    display: inline-block;
    font-size: 8pt;
    font-weight: 700;
    letter-spacing: .4pt;
    padding: 2pt 8pt;
    border-radius: 9pt;
    border: .7pt solid currentColor;
    margin-top: 7pt;
  }

  table { width: 100%; border-collapse: collapse; }

  /* Künye: etiket / değer ikilisi */
  .info td { padding: 4.5pt 0; vertical-align: top; border-bottom: .5pt solid #eef2f7; }
  .info td.k { width: 34%; color: var(--muted); font-size: 9pt; }
  .info td.v { font-weight: 600; }

  /* Kalemler tablosu */
  .items th {
    font-size: 8pt;
    font-weight: 700;
    letter-spacing: .5pt;
    text-transform: uppercase;
    color: var(--muted);
    background: var(--band);
    padding: 5pt 7pt;
    text-align: right;
    border-bottom: .7pt solid var(--line);
  }
  .items th:first-child, .items td:first-child { text-align: left; }
  .items td { padding: 5.5pt 7pt; border-bottom: .5pt solid #eef2f7; text-align: right; }
  .items tbody tr:nth-child(even) td { background: #fafbfd; }
  .items tfoot td {
    padding: 7pt;
    font-weight: 700;
    background: var(--band);
    border-top: .8pt solid var(--line);
    border-bottom: none;
  }
  .num { font-variant-numeric: tabular-nums; white-space: nowrap; }

  /* Geçmiş kayıtları */
  .entry {
    padding: 7pt 0 7pt 10pt;
    border-left: 2pt solid var(--line);
    margin-bottom: 7pt;
    break-inside: avoid;
    page-break-inside: avoid;
  }
  .entry.ok  { border-left-color: #2f9e4f; }
  .entry.no  { border-left-color: #b5443c; }
  .entry .t  { font-weight: 600; }
  .entry .b  { color: #46536a; margin-top: 1pt; }
  .entry .d  { font-size: 8pt; color: var(--muted); margin-top: 2pt; }

  .scope { text-align: justify; }
  .foot  { margin-top: 22pt; padding-top: 7pt; border-top: .5pt solid var(--line);
           font-size: 8pt; color: var(--muted); display: flex; justify-content: space-between; }
</style>
</head>
<body>
""");

        var no = string.IsNullOrWhiteSpace(contract.ContractNo) ? contract.RequestRefNo : contract.ContractNo!;
        var statusColor = StatusColor(contract.Status);

        sb.Append("<div class=\"head\">");
        sb.Append("<h1>SÖZLEŞME KÜNYESİ</h1>");
        sb.Append($"<div class=\"meta\">Oluşturma: {E(DateTime.Now.ToString("dd.MM.yyyy HH:mm", Tr))}</div>");
        sb.Append($"<div class=\"title\">{E(contract.Title)}</div>");
        sb.Append($"<div class=\"sub\">{E(no)} &nbsp;·&nbsp; {E(contract.CompanyName)}</div>");
        sb.Append($"<div class=\"pill\" style=\"color:{statusColor}\">{E(ContractStatusHelper.ToLabel(contract.Status))}</div>");
        sb.Append("</div>");

        // --- Künye ---
        var rows = new List<(string K, string V)>
        {
            ("Sözleşme No", string.IsNullOrWhiteSpace(contract.ContractNo) ? "-" : contract.ContractNo!),
            ("Talep Referans No", Dash(contract.RequestRefNo)),
            ("Tür", Dash(contract.Type)),
            ("Firma", Dash(contract.CompanyName)),
            ("Vergi No", Dash(contract.TaxNo)),
            ("SAP Cari Kodu", Dash(contract.SapCariKodu)),
            ("Talep Eden", contract.CreatedByUser is null
                ? "-"
                : contract.CreatedByUser.FullName +
                  (string.IsNullOrWhiteSpace(contract.CreatedByUser.Department) ? "" : $" ({contract.CreatedByUser.Department})")),
            ("Başlangıç Tarihi", contract.StartDate?.ToString("dd.MM.yyyy", Tr) ?? "-"),
            ("Bitiş Tarihi", contract.EndDate?.ToString("dd.MM.yyyy", Tr) ?? "-"),
            ("Ödeme Periyodu", Dash(contract.PaymentPeriod)),
            ("Toplam Bedel", CurrencyHelper.Format(contract.TotalAmount, contract.Currency)),
        };

        sb.Append("<table class=\"info\">");
        foreach (var (k, v) in rows)
            sb.Append($"<tr><td class=\"k\">{E(k)}</td><td class=\"v\">{E(v)}</td></tr>");
        sb.Append("</table>");

        // --- Kapsam ---
        if (!string.IsNullOrWhiteSpace(contract.Description))
        {
            sb.Append("<h2>Kapsam</h2>");
            sb.Append($"<div class=\"scope\">{E(contract.Description!).Replace("\n", "<br>")}</div>");
        }

        // --- Kalemler ---
        if (contract.Items.Count > 0)
        {
            sb.Append("<h2>Kalemler</h2>");
            sb.Append("<table class=\"items\"><thead><tr>");
            sb.Append("<th>Açıklama</th><th>Miktar</th><th>Birim Fiyat</th><th>Tutar</th>");
            sb.Append("</tr></thead><tbody>");

            decimal total = 0;
            foreach (var item in contract.Items)
            {
                total += item.LineTotal;
                var qty = string.IsNullOrWhiteSpace(item.Unit)
                    ? item.Quantity.ToString(Tr)
                    : $"{item.Quantity.ToString(Tr)} {item.Unit}";

                sb.Append("<tr>");
                sb.Append($"<td>{E(Dash(item.Description))}</td>");
                sb.Append($"<td class=\"num\">{E(qty)}</td>");
                sb.Append($"<td class=\"num\">{E(item.UnitPrice.ToString("N2", Tr))}</td>");
                sb.Append($"<td class=\"num\">{E(item.LineTotal.ToString("N2", Tr))}</td>");
                sb.Append("</tr>");
            }

            sb.Append("</tbody><tfoot><tr>");
            sb.Append("<td colspan=\"3\" style=\"text-align:right\">Toplam</td>");
            sb.Append($"<td class=\"num\">{E(CurrencyHelper.Format(total, contract.Currency))}</td>");
            sb.Append("</tr></tfoot></table>");
        }

        // --- Aşama geçmişi ---
        if (contract.ApprovalLogs.Count > 0)
        {
            sb.Append("<h2>Aşama Geçmişi</h2>");
            foreach (var log in contract.ApprovalLogs.OrderBy(l => l.ActionDate).ThenBy(l => l.Id))
            {
                var onay = log.Decision == ApprovalDecision.Onay;
                sb.Append($"<div class=\"entry {(onay ? "ok" : "no")}\">");
                sb.Append($"<div class=\"t\">{E(log.StepName)} — {(onay ? "Onaylandı" : "Reddedildi")}</div>");
                if (!string.IsNullOrWhiteSpace(log.Note))
                    sb.Append($"<div class=\"b\">{E(log.Note!)}</div>");
                sb.Append($"<div class=\"d\">{E(log.ActionDate.ToString("dd.MM.yyyy HH:mm", Tr))}</div>");
                sb.Append("</div>");
            }
        }

        // --- Revizyon geçmişi ---
        if (contract.Revisions.Count > 0)
        {
            sb.Append("<h2>Revizyon Geçmişi</h2>");
            foreach (var rev in contract.Revisions.OrderBy(r => r.ChangedAt))
            {
                var detay = $"Önceki bedel: {CurrencyHelper.Format(rev.PreviousTotalAmount, contract.Currency)}";
                if (rev.PreviousEndDate.HasValue)
                    detay += $" · Önceki bitiş: {rev.PreviousEndDate.Value.ToString("dd.MM.yyyy", Tr)}";

                sb.Append("<div class=\"entry\">");
                sb.Append($"<div class=\"t\">{E(rev.ChangeType)}</div>");
                if (!string.IsNullOrWhiteSpace(rev.Reason))
                    sb.Append($"<div class=\"b\">{E(rev.Reason)}</div>");
                sb.Append($"<div class=\"b\">{E(detay)}</div>");
                sb.Append($"<div class=\"d\">{E(rev.ChangedAt.ToString("dd.MM.yyyy HH:mm", Tr))}</div>");
                sb.Append("</div>");
            }
        }

        // --- Fesih geçmişi ---
        if (contract.Terminations.Count > 0)
        {
            sb.Append("<h2>Fesih Geçmişi</h2>");
            foreach (var term in contract.Terminations.OrderBy(t => t.RequestedAt))
            {
                var detay = $"Fesih tarihi: {term.TerminationDate.ToString("dd.MM.yyyy", Tr)}";
                if (term.CompensationAmount.HasValue)
                    detay += $" · Tazminat: {term.CompensationAmount.Value.ToString("N2", Tr)} TL ({term.CompensationDirection})";

                sb.Append("<div class=\"entry no\">");
                sb.Append($"<div class=\"t\">{E(term.TerminationType)}</div>");
                if (!string.IsNullOrWhiteSpace(term.Reason))
                    sb.Append($"<div class=\"b\">{E(term.Reason)}</div>");
                sb.Append($"<div class=\"b\">{E(detay)}</div>");
                sb.Append($"<div class=\"d\">{E(term.RequestedAt.ToString("dd.MM.yyyy HH:mm", Tr))}</div>");
                sb.Append("</div>");
            }
        }

        sb.Append("<div class=\"foot\"><span>SYS — Sözleşme Yönetim Sistemi</span>");
        sb.Append($"<span>{E(no)}</span></div>");

        // Sayfa açılır açılmaz tarayıcının yazdırma penceresini aç. Bu satır olmadan
        // kullanıcı yalnızca belgeyi görür, yazdırmayı elle başlatması gerekirdi.
        sb.Append("""
<script>
  window.addEventListener('load', function () { window.print(); });
</script>
</body>
</html>
""");

        return sb.ToString();
    }

    private static string Dash(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value!;

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string StatusColor(ContractStatus status) => status switch
    {
        ContractStatus.Aktif => "#1a6b2a",
        ContractStatus.OnayBekliyor => "#2d6ea8",
        ContractStatus.Uyari => "#b06a00",
        ContractStatus.Ihlal => "#a32d2d",
        ContractStatus.Feshedildi => "#a32d2d",
        ContractStatus.Reddedildi => "#a32d2d",
        ContractStatus.Tamamlandi => "#6b7686",
        _ => "#46536a"
    };
}
