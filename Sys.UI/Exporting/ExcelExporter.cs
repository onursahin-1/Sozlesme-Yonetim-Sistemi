using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClosedXML.Excel;
using Sys.Domain;

using Sys.UI.Localization;

namespace Sys.UI.Exporting;

// Liste ekranlarını Excel'e (.xlsx) aktarır.
//
// NEDEN CSV DEĞİL: CSV'de ayraç karakteri Türkçe yerel ayarda noktalı virgüle
// kayıyor, tutar ve tarihler metin olarak açılıyor, sütun genişliği ve başlık
// dondurma taşınamıyor. Mali işlerin ilk yapacağı şey toplam almak; bunun için
// hücrelerin gerçek sayı/tarih tipinde olması gerekiyor.
//
// TUTARLAR: sözleşmeler farklı para birimlerinde olabiliyor ve kur dönüşümü
// yapılmıyor. Bu yüzden tutar ve para birimi AYRI sütunlarda — Excel'de para
// birimine göre gruplayıp toplamak mümkün olsun diye. Tek sütunda "1.000,00 EUR"
// yazılsaydı hücre metin olur, toplanamazdı.
public static class ExcelExporter
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private const string DateFormat = "dd.mm.yyyy";
    private const string DateTimeFormat = "dd.mm.yyyy hh:mm";
    private const string MoneyFormat = "#,##0.00";

    public static void ExportContracts(IEnumerable<Contract> contracts, string filePath, string sheetTitle)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(sheetTitle);

        string[] headers =
        {
            Strings.T("Xls.ContractNo"), Strings.T("Xls.RequestRef"), Strings.T("Xls.Title"),
            Strings.T("Xls.Company"), Strings.T("Xls.TaxNo"), Strings.T("Xls.SapCode"),
            Strings.T("Xls.Type"), Strings.T("Xls.CompanyType"), Strings.T("Xls.Status"),
            Strings.T("Xls.Start"), Strings.T("Xls.End"), Strings.T("Xls.DaysLeft"),
            Strings.T("Xls.Amount"), Strings.T("Xls.Currency"), Strings.T("Xls.PaymentPeriod"),
            Strings.T("Xls.Requester"), Strings.T("Xls.Department"), Strings.T("Xls.CreatedAt")
        };

        WriteHeader(ws, headers);

        var row = 2;
        var today = DateTime.Today;

        foreach (var c in contracts)
        {
            var col = 1;
            ws.Cell(row, col++).Value = c.ContractNo ?? string.Empty;
            ws.Cell(row, col++).Value = c.RequestRefNo;
            ws.Cell(row, col++).Value = c.Title;
            ws.Cell(row, col++).Value = c.CompanyName;
            ws.Cell(row, col++).Value = c.TaxNo;
            ws.Cell(row, col++).Value = c.SapCariKodu ?? string.Empty;
            ws.Cell(row, col++).Value = c.Type;
            ws.Cell(row, col++).Value = c.CompanyType ?? string.Empty;
            ws.Cell(row, col++).Value = ContractStatusHelper.ToLabel(c.Status);

            SetDate(ws.Cell(row, col++), c.StartDate);
            SetDate(ws.Cell(row, col++), c.EndDate);

            // Kalan gün yalnızca yürürlükteki sözleşmelerde anlamlı; kapanmışlarda
            // negatif bir sayı yazmak yanıltıcı olurdu.
            var remaining = ws.Cell(row, col++);
            if (c.EndDate is { } end && !IsClosed(c.Status))
                remaining.Value = (end.Date - today).Days;

            var amount = ws.Cell(row, col++);
            amount.Value = c.TotalAmount;
            amount.Style.NumberFormat.Format = MoneyFormat;

            ws.Cell(row, col++).Value = c.Currency;
            ws.Cell(row, col++).Value = c.PaymentPeriod ?? string.Empty;
            ws.Cell(row, col++).Value = c.CreatedByUser?.FullName ?? string.Empty;
            ws.Cell(row, col++).Value = c.CreatedByUser?.Department ?? string.Empty;

            SetDateTime(ws.Cell(row, col), c.CreatedAt);

            row++;
        }

        Finish(ws, headers.Length, row - 1);
        workbook.SaveAs(filePath);
    }

    public static void ExportAuditLogs(IEnumerable<AuditLog> logs, string filePath)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(Strings.T("Nav.AuditLog"));

        string[] headers =
        {
            Strings.T("Xls.Date"), Strings.T("Xls.User"), Strings.T("Xls.Action"),
            Strings.T("Xls.EntityType"), Strings.T("Xls.EntityId"), Strings.T("Xls.Detail")
        };
        WriteHeader(ws, headers);

        var row = 2;
        foreach (var log in logs)
        {
            SetDateTime(ws.Cell(row, 1), log.ActionDate);
            ws.Cell(row, 2).Value = log.ActingUser?.FullName ?? Strings.T("Audit.UnknownUser", log.ActingUserId);
            ws.Cell(row, 3).Value = AuditActionCatalog.Label(log.Action);
            ws.Cell(row, 4).Value = log.EntityName;
            ws.Cell(row, 5).Value = log.EntityId;
            ws.Cell(row, 6).Value = log.Detail ?? string.Empty;
            row++;
        }

        Finish(ws, headers.Length, row - 1);
        workbook.SaveAs(filePath);
    }

    // --- Ortak biçimlendirme ---

    private static void WriteHeader(IXLWorksheet ws, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var header = ws.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.FromHtml("#1A2E4A");
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF1F6");
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        header.Style.Border.BottomBorderColor = XLColor.FromHtml("#C9D3E0");
        ws.Row(1).Height = 20;
    }

    private static void Finish(IXLWorksheet ws, int columnCount, int lastRow)
    {
        // Başlık satırı dondurulur ve süzgeç açılır: aktarmanın amacı analiz,
        // kullanıcı dosyayı açar açmaz filtreleyip sıralayabilmeli.
        ws.SheetView.FreezeRows(1);
        ws.Range(1, 1, Math.Max(lastRow, 1), columnCount).SetAutoFilter();

        ws.Columns(1, columnCount).AdjustToContents();

        // AdjustToContents uzun açıklamalarda sütunu aşırı genişletiyor; üst sınır
        // koyup metni kaydırıyoruz.
        foreach (var column in ws.Columns(1, columnCount))
        {
            if (column.Width > 50) column.Width = 50;
            if (column.Width < 10) column.Width = 10;
        }
    }

    private static void SetDate(IXLCell cell, DateTime? value)
    {
        if (value is null) return;
        cell.Value = value.Value.Date;
        cell.Style.NumberFormat.Format = DateFormat;
    }

    private static void SetDateTime(IXLCell cell, DateTime value)
    {
        cell.Value = value;
        cell.Style.NumberFormat.Format = DateTimeFormat;
    }

    private static bool IsClosed(ContractStatus status)
        => status is ContractStatus.Tamamlandi or ContractStatus.Feshedildi
                  or ContractStatus.Reddedildi or ContractStatus.Talep;

    // Dosya adı için güvenli metin: "Sozlesmeler_2026-08-24.xlsx" gibi.
    public static string SuggestFileName(string prefix)
    {
        var name = $"{prefix}_{DateTime.Now.ToString("yyyy-MM-dd_HHmm", Tr)}.xlsx";
        foreach (var invalid in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');
        return name;
    }
}
