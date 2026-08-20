using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Sys.Domain;

namespace Sys.UI.Printing;

// Sözleşme künyesini A4 PDF olarak üretir.
//
// Neden PDF? Avalonia'nın yerleşik bir yazdırma desteği yok. Yaygın çözüm, içeriği PDF'e
// dönüştürüp işletim sisteminin varsayılan görüntüleyicisinde açmak; kullanıcı oradan
// yazdırıyor. Yan faydası: çıktı aynı zamanda arşivlenebilir/paylaşılabilir bir dosya.
public static class ContractPdfExporter
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    // A4 (595 x 842 punto) için kenar boşlukları
    private const double MarginLeft = 45;
    private const double MarginRight = 45;
    private const double MarginTop = 45;
    private const double MarginBottom = 50;

    private static bool _fontsInitialized;

    private static void EnsureFonts()
    {
        if (_fontsInitialized) return;
        // PDFsharp'ın Core sürümü varsayılan olarak sistem fontlarını kullanmaz.
        // Bu ayar olmadan "Arial" çözümlenemez ve font hatası alınır.
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        _fontsInitialized = true;
    }

    public static void Export(Contract contract, string filePath)
    {
        EnsureFonts();

        using var document = new PdfDocument();
        document.Info.Title = string.IsNullOrWhiteSpace(contract.Title) ? "Sözleşme" : contract.Title;
        document.Info.Subject = "Sözleşme Künyesi";
        document.Info.Creator = "SYS — Sözleşme Yönetim Sistemi";

        var w = new PdfWriter(document);

        w.DrawDocumentHeader(contract);
        w.DrawInfoSection(contract);
        w.DrawDescription(contract);
        w.DrawItems(contract);
        w.DrawApprovalLogs(contract);
        w.DrawRevisions(contract);
        w.DrawTerminations(contract);
        w.FinishPageNumbers();

        document.Save(filePath);
    }

    // İmleç konumunu ve sayfa taşmasını yöneten küçük bir yardımcı. PDFsharp düşük
    // seviyeli bir kitaplık olduğu için metin akışı/sayfa kırılımı elle yapılıyor.
    private sealed class PdfWriter
    {
        private readonly PdfDocument _document;
        private PdfPage _page = null!;
        private XGraphics _gfx = null!;
        private double _y;

        private readonly XFont _fontTitle = new("Arial", 16, XFontStyleEx.Bold);
        private readonly XFont _fontHeading = new("Arial", 11, XFontStyleEx.Bold);
        private readonly XFont _fontLabel = new("Arial", 9, XFontStyleEx.Bold);
        private readonly XFont _fontBody = new("Arial", 9, XFontStyleEx.Regular);
        private readonly XFont _fontSmall = new("Arial", 7.5, XFontStyleEx.Regular);

        private static readonly XSolidBrush BrushMuted = new(XColor.FromArgb(110, 120, 135));
        private static readonly XSolidBrush BrushAccent = new(XColor.FromArgb(26, 46, 74));
        private static readonly XPen PenLine = new(XColor.FromArgb(205, 214, 226), 0.7);

        private readonly List<PdfPage> _pages = new();

        public PdfWriter(PdfDocument document)
        {
            _document = document;
            NewPage();
        }

        private double ContentWidth => _page.Width.Point - MarginLeft - MarginRight;
        private double PageBottom => _page.Height.Point - MarginBottom;

        private void NewPage()
        {
            _gfx?.Dispose();
            _page = _document.AddPage();
            _page.Size = PdfSharp.PageSize.A4;
            _pages.Add(_page);
            _gfx = XGraphics.FromPdfPage(_page);
            _y = MarginTop;
        }

        // İstenen yükseklik sayfaya sığmıyorsa yeni sayfaya geçer.
        private void EnsureSpace(double needed)
        {
            if (_y + needed <= PageBottom) return;
            NewPage();
        }

        private void Gap(double amount) => _y += amount;

        public void DrawDocumentHeader(Contract contract)
        {
            _gfx.DrawString("SÖZLEŞME KÜNYESİ", _fontTitle, BrushAccent,
                new XRect(MarginLeft, _y, ContentWidth, 22), XStringFormats.TopLeft);
            _y += 22;

            _gfx.DrawString($"Oluşturma: {DateTime.Now.ToString("dd.MM.yyyy HH:mm", Tr)}", _fontSmall, BrushMuted,
                new XRect(MarginLeft, _y, ContentWidth, 12), XStringFormats.TopLeft);
            _y += 16;

            _gfx.DrawLine(PenLine, MarginLeft, _y, MarginLeft + ContentWidth, _y);
            _y += 14;

            var titleHeight = DrawWrapped(contract.Title, _fontHeading, XBrushes.Black, MarginLeft, ContentWidth, 14);
            _y += titleHeight + 10;
        }

        public void DrawInfoSection(Contract contract)
        {
            var rows = new List<(string Label, string Value)>
            {
                ("Sözleşme No", string.IsNullOrWhiteSpace(contract.ContractNo) ? "-" : contract.ContractNo!),
                ("Talep Referans No", contract.RequestRefNo),
                ("Durum", ContractStatusHelper.ToLabel(contract.Status)),
                ("Tür", string.IsNullOrWhiteSpace(contract.Type) ? "-" : contract.Type),
                ("Firma", contract.CompanyName),
                ("Vergi No", string.IsNullOrWhiteSpace(contract.TaxNo) ? "-" : contract.TaxNo),
                ("Talep Eden", contract.CreatedByUser is null
                    ? "-"
                    : contract.CreatedByUser.FullName +
                      (string.IsNullOrWhiteSpace(contract.CreatedByUser.Department) ? "" : $" ({contract.CreatedByUser.Department})")),
                ("Başlangıç Tarihi", contract.StartDate?.ToString("dd.MM.yyyy", Tr) ?? "-"),
                ("Bitiş Tarihi", contract.EndDate?.ToString("dd.MM.yyyy", Tr) ?? "-"),
                ("Ödeme Periyodu", string.IsNullOrWhiteSpace(contract.PaymentPeriod) ? "-" : contract.PaymentPeriod!),
                ("Toplam Bedel", contract.TotalAmount.ToString("N2", Tr) + " TL"),
            };

            const double labelWidth = 130;
            foreach (var (label, value) in rows)
            {
                EnsureSpace(16);
                _gfx.DrawString(label, _fontLabel, XBrushes.Black,
                    new XRect(MarginLeft, _y, labelWidth, 13), XStringFormats.TopLeft);

                var valueHeight = DrawWrapped(value, _fontBody, XBrushes.Black,
                    MarginLeft + labelWidth, ContentWidth - labelWidth, 12);

                _y += Math.Max(13, valueHeight) + 3;
            }

            Gap(6);
        }

        public void DrawDescription(Contract contract)
        {
            if (string.IsNullOrWhiteSpace(contract.Description)) return;

            DrawSectionHeading("Kapsam");
            var height = DrawWrapped(contract.Description, _fontBody, XBrushes.Black, MarginLeft, ContentWidth, 12);
            _y += height + 10;
        }

        public void DrawItems(Contract contract)
        {
            if (contract.Items.Count == 0) return;

            DrawSectionHeading("Kalemler");

            // Sütunlar: Açıklama | Miktar | Birim Fiyat | Tutar
            double colQty = 60, colUnit = 90, colTotal = 90;
            double colDesc = ContentWidth - colQty - colUnit - colTotal;

            DrawItemsHeaderRow(colDesc, colQty, colUnit, colTotal);

            decimal grandTotal = 0;
            foreach (var item in contract.Items)
            {
                var lineTotal = item.LineTotal;
                grandTotal += lineTotal;

                // Miktarın yanında birim de gösterilir (örn. "10 adet").
                var qtyText = string.IsNullOrWhiteSpace(item.Unit)
                    ? item.Quantity.ToString(Tr)
                    : $"{item.Quantity.ToString(Tr)} {item.Unit}";

                var aciklama = string.IsNullOrWhiteSpace(item.Description) ? "-" : item.Description;
                var descHeight = MeasureWrapped(aciklama, _fontBody, colDesc - 6, 12);
                EnsureSpace(descHeight + 8);

                var rowTop = _y;
                DrawWrapped(aciklama, _fontBody, XBrushes.Black, MarginLeft, colDesc - 6, 12);

                _y = rowTop;
                DrawRight(qtyText, _fontBody, MarginLeft + colDesc, colQty - 6);
                DrawRight(item.UnitPrice.ToString("N2", Tr), _fontBody, MarginLeft + colDesc + colQty, colUnit - 6);
                DrawRight(lineTotal.ToString("N2", Tr), _fontBody, MarginLeft + colDesc + colQty + colUnit, colTotal - 6);

                _y = rowTop + descHeight + 4;
                _gfx.DrawLine(PenLine, MarginLeft, _y, MarginLeft + ContentWidth, _y);
                _y += 4;
            }

            EnsureSpace(20);
            _gfx.DrawString("Toplam", _fontLabel, XBrushes.Black,
                new XRect(MarginLeft + colDesc, _y, colQty + colUnit - 6, 13), XStringFormats.TopRight);
            DrawRight(grandTotal.ToString("N2", Tr) + " TL", _fontLabel, MarginLeft + colDesc + colQty + colUnit, colTotal - 6);
            _y += 20;
        }

        private void DrawItemsHeaderRow(double colDesc, double colQty, double colUnit, double colTotal)
        {
            EnsureSpace(20);
            _gfx.DrawString("Açıklama", _fontLabel, BrushMuted,
                new XRect(MarginLeft, _y, colDesc, 13), XStringFormats.TopLeft);
            DrawRight("Miktar", _fontLabel, MarginLeft + colDesc, colQty - 6, BrushMuted);
            DrawRight("Birim Fiyat", _fontLabel, MarginLeft + colDesc + colQty, colUnit - 6, BrushMuted);
            DrawRight("Tutar", _fontLabel, MarginLeft + colDesc + colQty + colUnit, colTotal - 6, BrushMuted);
            _y += 15;
            _gfx.DrawLine(PenLine, MarginLeft, _y, MarginLeft + ContentWidth, _y);
            _y += 5;
        }

        public void DrawApprovalLogs(Contract contract)
        {
            if (contract.ApprovalLogs.Count == 0) return;

            DrawSectionHeading("Aşama Geçmişi");
            foreach (var log in contract.ApprovalLogs.OrderBy(l => l.ActionDate))
            {
                var karar = log.Decision == ApprovalDecision.Onay ? "Onaylandı" : "Reddedildi";
                DrawEntry(
                    $"{log.StepNumber}. Adım — {log.StepName} — {karar}",
                    log.Note,
                    log.ActionDate.ToString("dd.MM.yyyy HH:mm", Tr));
            }
            Gap(6);
        }

        public void DrawRevisions(Contract contract)
        {
            if (contract.Revisions.Count == 0) return;

            DrawSectionHeading("Revizyon Geçmişi");
            foreach (var revision in contract.Revisions.OrderBy(r => r.ChangedAt))
            {
                var detay = $"Önceki bedel: {revision.PreviousTotalAmount.ToString("N2", Tr)} TL";
                if (revision.PreviousEndDate.HasValue)
                    detay += $" · Önceki bitiş: {revision.PreviousEndDate.Value.ToString("dd.MM.yyyy", Tr)}";

                DrawEntry(
                    revision.ChangeType,
                    string.IsNullOrWhiteSpace(revision.Reason) ? detay : revision.Reason + "\n" + detay,
                    revision.ChangedAt.ToString("dd.MM.yyyy HH:mm", Tr));
            }
            Gap(6);
        }

        public void DrawTerminations(Contract contract)
        {
            if (contract.Terminations.Count == 0) return;

            DrawSectionHeading("Fesih Geçmişi");
            foreach (var termination in contract.Terminations.OrderBy(t => t.RequestedAt))
            {
                var detay = $"Fesih tarihi: {termination.TerminationDate.ToString("dd.MM.yyyy", Tr)}";
                if (termination.CompensationAmount.HasValue)
                    detay += $" · Tazminat: {termination.CompensationAmount.Value.ToString("N2", Tr)} TL ({termination.CompensationDirection})";

                DrawEntry(
                    termination.TerminationType,
                    string.IsNullOrWhiteSpace(termination.Reason) ? detay : termination.Reason + "\n" + detay,
                    termination.RequestedAt.ToString("dd.MM.yyyy HH:mm", Tr));
            }
            Gap(6);
        }

        // Sayfa numaraları ancak toplam sayfa sayısı bilindiğinde yazılabildiği için
        // en sonda, tüm sayfalar dolaşılarak eklenir.
        public void FinishPageNumbers()
        {
            _gfx.Dispose();

            for (int i = 0; i < _pages.Count; i++)
            {
                using var gfx = XGraphics.FromPdfPage(_pages[i]);
                var text = $"Sayfa {i + 1} / {_pages.Count}";
                var width = _pages[i].Width.Point - MarginLeft - MarginRight;
                gfx.DrawString(text, _fontSmall, BrushMuted,
                    new XRect(MarginLeft, _pages[i].Height.Point - MarginBottom + 16, width, 12),
                    XStringFormats.TopRight);
                gfx.DrawString("SYS — Sözleşme Yönetim Sistemi", _fontSmall, BrushMuted,
                    new XRect(MarginLeft, _pages[i].Height.Point - MarginBottom + 16, width, 12),
                    XStringFormats.TopLeft);
            }
        }

        private void DrawSectionHeading(string text)
        {
            EnsureSpace(28);
            _gfx.DrawString(text, _fontHeading, BrushAccent,
                new XRect(MarginLeft, _y, ContentWidth, 15), XStringFormats.TopLeft);
            _y += 16;
            _gfx.DrawLine(PenLine, MarginLeft, _y, MarginLeft + ContentWidth, _y);
            _y += 7;
        }

        // Başlık + (opsiyonel) açıklama + tarih üçlüsünden oluşan tek bir geçmiş kaydı.
        private void DrawEntry(string title, string? body, string timestamp)
        {
            var titleHeight = MeasureWrapped(title, _fontLabel, ContentWidth, 12);
            var bodyHeight = string.IsNullOrWhiteSpace(body) ? 0 : MeasureWrapped(body!, _fontBody, ContentWidth, 12);
            EnsureSpace(titleHeight + bodyHeight + 20);

            DrawWrapped(title, _fontLabel, XBrushes.Black, MarginLeft, ContentWidth, 12);
            _y += titleHeight;

            if (!string.IsNullOrWhiteSpace(body))
            {
                DrawWrapped(body!, _fontBody, XBrushes.Black, MarginLeft, ContentWidth, 12);
                _y += bodyHeight;
            }

            _gfx.DrawString(timestamp, _fontSmall, BrushMuted,
                new XRect(MarginLeft, _y, ContentWidth, 11), XStringFormats.TopLeft);
            _y += 15;
        }

        private void DrawRight(string text, XFont font, double x, double width, XBrush? brush = null)
        {
            _gfx.DrawString(text, font, brush ?? XBrushes.Black,
                new XRect(x, _y, width, 13), XStringFormats.TopRight);
        }

        // Metni verilen genişliğe göre satırlara böler, çizer ve kapladığı yüksekliği döner.
        private double DrawWrapped(string text, XFont font, XBrush brush, double x, double width, double lineHeight)
        {
            var lines = WrapLines(text, font, width);
            var startY = _y;
            foreach (var line in lines)
            {
                if (_y + lineHeight > PageBottom)
                {
                    NewPage();
                    startY = _y;
                }
                _gfx.DrawString(line, font, brush, new XRect(x, _y, width, lineHeight), XStringFormats.TopLeft);
                _y += lineHeight;
            }
            var height = _y - startY;
            _y = startY; // imleci çağıran ayarlasın
            return height;
        }

        private double MeasureWrapped(string text, XFont font, double width, double lineHeight)
            => WrapLines(text, font, width).Count * lineHeight;

        private List<string> WrapLines(string text, XFont font, double width)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) { result.Add(string.Empty); return result; }

            foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
            {
                if (paragraph.Length == 0) { result.Add(string.Empty); continue; }

                var current = string.Empty;
                foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var candidate = current.Length == 0 ? word : current + " " + word;
                    if (_gfx.MeasureString(candidate, font).Width <= width)
                    {
                        current = candidate;
                        continue;
                    }

                    if (current.Length > 0) result.Add(current);

                    // Tek bir kelime bile satıra sığmıyorsa (uzun kod/no) karakter bazında bölünür.
                    current = word;
                    while (_gfx.MeasureString(current, font).Width > width && current.Length > 1)
                    {
                        int cut = current.Length - 1;
                        while (cut > 1 && _gfx.MeasureString(current[..cut], font).Width > width) cut--;
                        result.Add(current[..cut]);
                        current = current[cut..];
                    }
                }
                result.Add(current);
            }

            return result;
        }
    }
}
