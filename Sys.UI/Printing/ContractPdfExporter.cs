using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Sys.Domain;
using Sys.UI.ViewModels;

namespace Sys.UI.Printing;

// Sözleşme künyesini A4 PDF olarak üretir — "PDF Kaydet" işleminin çıktısı.
//
// Ekrandan yazdırma artık buradan geçmiyor: Windows'un varsayılan PDF uygulaması
// (çoğunlukla Edge) kabuk "print" fiilini kaydetmediği için PDF üzerinden yazdırma
// başlatılamıyordu. Yazdırma yolu ContractPrintDocument (HTML + window.print()).
// Bu sınıf arşivlenebilir/paylaşılabilir dosya üretmeye devam ediyor.
public static class ContractPdfExporter
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    // A4 (595 x 842 punto) için kenar boşlukları
    private const double MarginLeft = 45;
    private const double MarginRight = 45;
    private const double MarginTop = 45;
    private const double MarginBottom = 50;

    private static bool _fontsInitialized;
    private static string? _family;

    private static void EnsureFonts()
    {
        if (_fontsInitialized) return;
        // PDFsharp'ın Core sürümü varsayılan olarak sistem fontlarını kullanmaz.
        // Bu ayar olmadan font adı çözümlenemez ve hata alınır.
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        _fontsInitialized = true;
    }

    // Belge yazı tipi. Arial yerine uygulamanın arayüzüyle aynı aileyi kullanıyoruz;
    // Segoe UI'ın harf yüksekliği ve boşlukları basılı metinde belirgin şekilde daha
    // okunaklı. Sistemde yoksa sırayla Calibri ve Arial'a düşülür — Arial her Windows
    // kurulumunda bulunduğu için zincir garanti sonlanır.
    private static string FontFamily
    {
        get
        {
            if (_family is not null) return _family;

            foreach (var candidate in new[] { "Segoe UI", "Calibri", "Arial" })
            {
                try
                {
                    // XFont oluşturmak tipografiyi çözümlemeye zorlar; bulunamazsa
                    // burada hata verir ve sıradaki adaya geçilir.
                    _ = new XFont(candidate, 10);
                    _family = candidate;
                    return _family;
                }
                catch
                {
                    // sıradaki adayı dene
                }
            }

            _family = "Arial";
            return _family;
        }
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
        w.DrawProcessSteps(contract);
        w.DrawViolations(contract);
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

        // Puntolar bir tık büyütüldü ve satır aralıkları açıldı: 9 punto Arial ekranda
        // idare ediyordu ama A4 çıktıda sıkışık ve soluk duruyordu.
        private readonly XFont _fontTitle = new(FontFamily, 19, XFontStyleEx.Bold);
        private readonly XFont _fontDocTitle = new(FontFamily, 13, XFontStyleEx.Bold);
        private readonly XFont _fontHeading = new(FontFamily, 10.5, XFontStyleEx.Bold);
        private readonly XFont _fontLabel = new(FontFamily, 9.5, XFontStyleEx.Bold);
        private readonly XFont _fontKey = new(FontFamily, 9.5, XFontStyleEx.Regular);
        private readonly XFont _fontBody = new(FontFamily, 9.5, XFontStyleEx.Regular);
        private readonly XFont _fontSmall = new(FontFamily, 8, XFontStyleEx.Regular);

        // Süreç adımları için ayrı puntolar: etiketler dar sütunlara sığmalı.
        private readonly XFont _fontStep = new(FontFamily, 7.5, XFontStyleEx.Bold);
        private readonly XFont _fontTiny = new(FontFamily, 6.5, XFontStyleEx.Regular);
        private const double StepLineHeight = 9.5;

        // Satır yüksekliği tek yerden: gövde punto 9,5 için 13 rahat bir aralık verir.
        private const double LineHeight = 13;

        private static readonly XSolidBrush BrushMuted = new(XColor.FromArgb(107, 118, 134));
        private static readonly XSolidBrush BrushAccent = new(XColor.FromArgb(45, 110, 168));
        private static readonly XSolidBrush BrushInk = new(XColor.FromArgb(22, 35, 58));
        private static readonly XSolidBrush BrushBand = new(XColor.FromArgb(244, 247, 251));
        private static readonly XSolidBrush BrushZebra = new(XColor.FromArgb(250, 251, 253));
        private static readonly XPen PenLine = new(XColor.FromArgb(216, 223, 233), 0.7);
        private static readonly XPen PenHair = new(XColor.FromArgb(238, 242, 247), 0.5);
        private static readonly XPen PenAccent = new(XColor.FromArgb(45, 110, 168), 1.6);

        // Süreç adımları
        private static readonly XSolidBrush BrushSuccess = new(XColor.FromArgb(22, 163, 74));
        private static readonly XSolidBrush BrushDanger = new(XColor.FromArgb(185, 45, 45));
        private static readonly XPen PenSuccess = new(XColor.FromArgb(22, 163, 74), 1.1);
        private static readonly XPen PenDanger = new(XColor.FromArgb(185, 45, 45), 1.1);
        private static readonly XPen PenMark = new(XColor.FromArgb(255, 255, 255), 1.1);

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
                new XRect(MarginLeft, _y, ContentWidth, 25), XStringFormats.TopLeft);
            _y += 24;

            _gfx.DrawString($"Oluşturma: {DateTime.Now.ToString("dd.MM.yyyy HH:mm", Tr)}", _fontSmall, BrushMuted,
                new XRect(MarginLeft, _y, ContentWidth, 12), XStringFormats.TopLeft);
            _y += 18;

            // Sözleşmenin başlığı ve kimliği: belgenin en üstünde, künye tablosunu
            // taramadan da hangi sözleşme olduğu anlaşılsın.
            var titleHeight = DrawWrapped(contract.Title, _fontDocTitle, BrushInk, MarginLeft, ContentWidth, 16);
            _y += titleHeight + 3;

            var no = string.IsNullOrWhiteSpace(contract.ContractNo) ? contract.RequestRefNo : contract.ContractNo!;
            _gfx.DrawString($"{no}   ·   {contract.CompanyName}", _fontBody, BrushMuted,
                new XRect(MarginLeft, _y, ContentWidth, 13), XStringFormats.TopLeft);
            _y += 16;

            _gfx.DrawString(ContractStatusHelper.ToLabel(contract.Status), _fontLabel, BrushAccent,
                new XRect(MarginLeft, _y, ContentWidth, 13), XStringFormats.TopLeft);
            _y += 18;

            // İnce çizgi yerine vurgu rengiyle kalın bir ayraç: başlık bloğunu gövdeden
            // net biçimde ayırıyor.
            _gfx.DrawLine(PenAccent, MarginLeft, _y, MarginLeft + ContentWidth, _y);
            _y += 16;
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
                ("Toplam Bedel", CurrencyHelper.Format(contract.TotalAmount, contract.Currency)),
            };

            // Etiket gri ve normal, değer koyu ve yarı kalın: göz önce değerleri tarıyor.
            // Her satırın altında saç teli inceliğinde bir ayraç var; eski sürümde
            // satırlar birbirine yapışık, kalın siyah etiketlerle daha gürültülüydü.
            const double labelWidth = 150;
            foreach (var (label, value) in rows)
            {
                var valueHeight = Math.Max(LineHeight, MeasureWrapped(value, _fontBody, ContentWidth - labelWidth, LineHeight));
                EnsureSpace(valueHeight + 8);

                _gfx.DrawString(label, _fontKey, BrushMuted,
                    new XRect(MarginLeft, _y, labelWidth, LineHeight), XStringFormats.TopLeft);

                DrawWrapped(value, _fontLabel, BrushInk,
                    MarginLeft + labelWidth, ContentWidth - labelWidth, LineHeight);

                _y += valueHeight + 4;
                _gfx.DrawLine(PenHair, MarginLeft, _y, MarginLeft + ContentWidth, _y);
                _y += 4;
            }

            Gap(8);
        }

        public void DrawDescription(Contract contract)
        {
            if (string.IsNullOrWhiteSpace(contract.Description)) return;

            DrawSectionHeading("Kapsam");
            var height = DrawWrapped(contract.Description, _fontBody, BrushInk, MarginLeft, ContentWidth, LineHeight);
            _y += height + 12;
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
            var zebra = false;

            foreach (var item in contract.Items)
            {
                var lineTotal = item.LineTotal;
                grandTotal += lineTotal;

                // Miktarın yanında birim de gösterilir (örn. "10 adet").
                var qtyText = string.IsNullOrWhiteSpace(item.Unit)
                    ? item.Quantity.ToString(Tr)
                    : $"{item.Quantity.ToString(Tr)} {item.Unit}";

                var aciklama = string.IsNullOrWhiteSpace(item.Description) ? "-" : item.Description;
                var descHeight = MeasureWrapped(aciklama, _fontBody, colDesc - 8, LineHeight);
                var rowHeight = Math.Max(LineHeight, descHeight) + 9;
                EnsureSpace(rowHeight);

                var rowTop = _y;

                // Zebra: uzun listelerde satırların hangi tutara ait olduğu şaşmasın.
                if (zebra)
                    _gfx.DrawRectangle(BrushZebra, MarginLeft, rowTop, ContentWidth, rowHeight);
                zebra = !zebra;

                _y = rowTop + 4;
                DrawWrapped(aciklama, _fontBody, BrushInk, MarginLeft + 4, colDesc - 8, LineHeight);
                DrawRight(qtyText, _fontBody, MarginLeft + colDesc, colQty - 8);
                DrawRight(item.UnitPrice.ToString("N2", Tr), _fontBody, MarginLeft + colDesc + colQty, colUnit - 8);
                DrawRight(lineTotal.ToString("N2", Tr), _fontLabel, MarginLeft + colDesc + colQty + colUnit, colTotal - 8);

                _y = rowTop + rowHeight;
                _gfx.DrawLine(PenHair, MarginLeft, _y, MarginLeft + ContentWidth, _y);
            }

            // Toplam satırı: zeminli ve üstünde kalın çizgi — kalemlerden ayrışıyor.
            EnsureSpace(24);
            _gfx.DrawRectangle(BrushBand, MarginLeft, _y, ContentWidth, 22);
            _gfx.DrawLine(PenLine, MarginLeft, _y, MarginLeft + ContentWidth, _y);
            _y += 5;
            _gfx.DrawString("TOPLAM", _fontLabel, BrushInk,
                new XRect(MarginLeft + colDesc, _y, colQty + colUnit - 8, LineHeight), XStringFormats.TopRight);
            DrawRight(CurrencyHelper.Format(grandTotal, contract.Currency), _fontLabel,
                MarginLeft + colDesc + colQty + colUnit, colTotal - 8, BrushInk);
            _y += 25;
        }

        private void DrawItemsHeaderRow(double colDesc, double colQty, double colUnit, double colTotal)
        {
            EnsureSpace(24);

            // Başlık satırı zeminli: tablo sınırları çizgi kalabalığı olmadan belli oluyor.
            _gfx.DrawRectangle(BrushBand, MarginLeft, _y, ContentWidth, 19);
            _y += 4;

            _gfx.DrawString("AÇIKLAMA", _fontSmall, BrushMuted,
                new XRect(MarginLeft + 4, _y, colDesc, LineHeight), XStringFormats.TopLeft);
            DrawRight("MİKTAR", _fontSmall, MarginLeft + colDesc, colQty - 8, BrushMuted);
            DrawRight("BİRİM FİYAT", _fontSmall, MarginLeft + colDesc + colQty, colUnit - 8, BrushMuted);
            DrawRight("TUTAR", _fontSmall, MarginLeft + colDesc + colQty + colUnit, colTotal - 8, BrushMuted);

            _y += 15;
            _gfx.DrawLine(PenLine, MarginLeft, _y, MarginLeft + ContentWidth, _y);
        }

        // SÜREÇ ADIMLARI — yatay zaman çizelgesi.
        //
        // Bu bölüm eskiden "Aşama Geçmişi" adıyla alt alta metin bloklarıydı ve tek
        // başına bir A4 sayfasını doldurabiliyordu; altı adımlık sıradan bir sözleşme
        // üç sayfaya çıkıyordu. Oysa bilgi kısa: bir etiket, bir tarih, bir sonuç.
        // Dikey yığın sayfayı yüksekliğine harcıyordu, genişliğini boş bırakarak.
        //
        // Adımlar ekrandakiyle AYNI kaynaktan geliyor (ContractStepViewModel.Build):
        // "Talep oluşturuldu" ile başlıyor, onay kayıtlarıyla devam ediyor, sözleşmenin
        // bugünkü durumuyla bitiyor. İki yerde ayrı kurulsaydı zamanla ayrışırlardı —
        // bu projede birkaç kez yaşandı.
        public void DrawProcessSteps(Contract contract)
        {
            var steps = ContractStepViewModel.Build(contract);
            if (steps.Count == 0) return;

            DrawSectionHeading("Süreç Adımları");

            // Satır başına dört adım: A4 içerik genişliğinde (505 punto) her sütuna
            // ~126 punto düşüyor ve "Müdür (YK) Onayı — Reddedildi" gibi en uzun
            // etiket iki satıra sığıyor. Daha fazla sütun etiketleri kırpardı.
            const int columns = 4;
            const double circle = 11;
            const double gutter = 8;

            var colWidth = ContentWidth / columns;
            var textWidth = colWidth - gutter * 2;

            for (var start = 0; start < steps.Count; start += columns)
            {
                var row = steps.Skip(start).Take(columns).ToList();

                // Satırın yüksekliği en uzun etiketine göre belirlenir; sütunlar
                // aynı hizada başlar, aksi halde daireler kayardı.
                double textHeight = 0;
                foreach (var step in row)
                    textHeight = Math.Max(textHeight, MeasureStepText(step, textWidth));

                var rowHeight = circle + 6 + textHeight + 10;
                EnsureSpace(rowHeight);

                var circleCenterY = _y + circle / 2;

                // Bağlayıcı çizgiler dairelerin ARKASINA çiziliyor; sonra daireler
                // üstlerine geliyor ve çizgi dairenin içinden geçmiş gibi durmuyor.
                for (var i = 0; i < row.Count - 1; i++)
                {
                    var fromX = MarginLeft + colWidth * i + colWidth / 2 + circle / 2 + 2;
                    var toX = MarginLeft + colWidth * (i + 1) + colWidth / 2 - circle / 2 - 2;
                    _gfx.DrawLine(ConnectorPen(row[i].State), fromX, circleCenterY, toX, circleCenterY);
                }

                for (var i = 0; i < row.Count; i++)
                {
                    var centerX = MarginLeft + colWidth * i + colWidth / 2;
                    DrawStepMarker(row[i].State, centerX, circleCenterY, circle);
                }

                var textTop = _y + circle + 6;
                for (var i = 0; i < row.Count; i++)
                {
                    var x = MarginLeft + colWidth * i + gutter;
                    DrawStepText(row[i], x, textTop, textWidth);
                }

                _y += rowHeight;
            }

            Gap(6);
        }

        private double MeasureStepText(ContractStepViewModel step, double width)
        {
            var height = WrapLines(step.Label, _fontStep, width).Count * StepLineHeight;
            if (step.HasDate) height += 10;
            if (step.HasNote) height += WrapLines(step.Note!, _fontSmall, width).Count * 9;
            return height;
        }

        private void DrawStepText(ContractStepViewModel step, double x, double top, double width)
        {
            var brush = step.State == ContractStepState.Rejected ? BrushDanger : BrushInk;
            var y = top;

            foreach (var line in WrapLines(step.Label, _fontStep, width))
            {
                _gfx.DrawString(line, _fontStep, brush,
                    new XRect(x, y, width, StepLineHeight), XStringFormats.TopCenter);
                y += StepLineHeight;
            }

            if (step.HasDate)
            {
                _gfx.DrawString(step.DateText, _fontTiny, BrushMuted,
                    new XRect(x, y, width, 10), XStringFormats.TopCenter);
                y += 10;
            }

            // Red gerekçesi adımın altında duruyor: kararın kendisiyle birlikte
            // okunmazsa ayrı bir bölüme bakmak gerekiyordu.
            if (step.HasNote)
            {
                foreach (var line in WrapLines(step.Note!, _fontSmall, width))
                {
                    _gfx.DrawString(line, _fontSmall, BrushMuted,
                        new XRect(x, y, width, 9), XStringFormats.TopCenter);
                    y += 9;
                }
            }
        }

        // İşaretler yazı tipi karakteriyle değil ÇİZGİYLE çiziliyor. "✓" ve "✕" her
        // yazı tipinde bulunmuyor; bulunmadığında sessizce boş kutu basılırdı.
        private void DrawStepMarker(ContractStepState state, double cx, double cy, double size)
        {
            var r = size / 2;
            var box = new XRect(cx - r, cy - r, size, size);

            switch (state)
            {
                case ContractStepState.Completed:
                    _gfx.DrawEllipse(BrushSuccess, box);
                    DrawCheck(cx, cy, size);
                    break;

                case ContractStepState.Rejected:
                    _gfx.DrawEllipse(BrushDanger, box);
                    DrawCross(cx, cy, size);
                    break;

                case ContractStepState.Current:
                    _gfx.DrawEllipse(BrushAccent, box);
                    break;

                default:
                    _gfx.DrawEllipse(PenLine, box);
                    break;
            }
        }

        private void DrawCheck(double cx, double cy, double size)
        {
            var s = size * 0.28;
            _gfx.DrawLine(PenMark, cx - s, cy, cx - s * 0.2, cy + s * 0.8);
            _gfx.DrawLine(PenMark, cx - s * 0.2, cy + s * 0.8, cx + s, cy - s * 0.7);
        }

        private void DrawCross(double cx, double cy, double size)
        {
            var s = size * 0.24;
            _gfx.DrawLine(PenMark, cx - s, cy - s, cx + s, cy + s);
            _gfx.DrawLine(PenMark, cx + s, cy - s, cx - s, cy + s);
        }

        // Bağlayıcı, ÖNCEKİ adımın sonucunu taşır: yeşil zincirin nerede kırıldığı
        // tek bakışta görünsün.
        private static XPen ConnectorPen(ContractStepState state) => state switch
        {
            ContractStepState.Completed => PenSuccess,
            ContractStepState.Rejected => PenDanger,
            _ => PenLine
        };

        // İhlal kayıtları hiçbir çıktıda yer almıyordu.
        public void DrawViolations(Contract contract)
        {
            if (contract.Violations.Count == 0) return;

            DrawSectionHeading("İhlal Geçmişi");
            foreach (var violation in contract.Violations.OrderBy(v => v.ViolationDate).ThenBy(v => v.Id))
            {
                var detay = $"İhlal tarihi: {violation.ViolationDate.ToString("dd.MM.yyyy", Tr)}";

                // Giderilen ihlaller açık olanlardan ayrılmalı; aksi halde geçmiş,
                // hepsi hâlâ sürüyormuş gibi okunur.
                if (violation.IsResolved)
                    detay += $"\nGiderildi ({violation.ResolvedAt:dd.MM.yyyy HH:mm}): {violation.ResolutionNote}";

                DrawEntry(
                    $"{violation.ViolationType} — {(violation.IsResolved ? "Giderildi" : "Açık")}",
                    string.IsNullOrWhiteSpace(violation.Description) ? detay : violation.Description + "\n" + detay,
                    violation.ReportedAt.ToString("dd.MM.yyyy HH:mm", Tr));
            }
            Gap(6);
        }

        public void DrawRevisions(Contract contract)
        {
            if (contract.Revisions.Count == 0) return;

            DrawSectionHeading("Revizyon Geçmişi");
            foreach (var revision in contract.Revisions.OrderBy(r => r.ChangedAt))
            {
                var detay = $"Önceki bedel: {CurrencyHelper.Format(revision.PreviousTotalAmount, contract.Currency)}";
                if (revision.PreviousEndDate.HasValue)
                    detay += $" · Önceki bitiş: {revision.PreviousEndDate.Value.ToString("dd.MM.yyyy", Tr)}";

                // Sonuç başlığa yazılıyor: bu kayıtlar birer talep, reddedilmiş olabilir.
                DrawEntry(
                    $"{revision.ChangeType} — {OutcomeText(revision.IsApproved)}",
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
                    $"{termination.TerminationType} — {OutcomeText(termination.IsApproved)}",
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
                var width = _pages[i].Width.Point - MarginLeft - MarginRight;
                var footY = _pages[i].Height.Point - MarginBottom + 14;

                // Alt bilgiyi gövdeden ayıran ince çizgi
                gfx.DrawLine(PenHair, MarginLeft, footY - 4, MarginLeft + width, footY - 4);

                gfx.DrawString($"Sayfa {i + 1} / {_pages.Count}", _fontSmall, BrushMuted,
                    new XRect(MarginLeft, footY, width, 12), XStringFormats.TopRight);
                gfx.DrawString("SYS — Sözleşme Yönetim Sistemi", _fontSmall, BrushMuted,
                    new XRect(MarginLeft, footY, width, 12), XStringFormats.TopLeft);
            }
        }

        // Bölüm başlığı büyük harfe alındı ve vurgu rengine geçti; altındaki ince çizgi
        // bölümü açıkça başlatıyor. Başlığın sayfa sonunda tek başına kalmaması için
        // altında en az bir satırlık yer aranıyor.
        private void DrawSectionHeading(string text)
        {
            EnsureSpace(46);
            _gfx.DrawString(text.ToUpper(Tr), _fontHeading, BrushAccent,
                new XRect(MarginLeft, _y, ContentWidth, 15), XStringFormats.TopLeft);
            _y += 16;
            _gfx.DrawLine(PenLine, MarginLeft, _y, MarginLeft + ContentWidth, _y);
            _y += 9;
        }

        // Başlık + (opsiyonel) açıklama + tarih üçlüsünden oluşan tek bir geçmiş kaydı.
        // Solunda dikey bir işaret çizgisi var: kayıtlar birbirinden görsel olarak
        // ayrılıyor, eskiden hepsi tek bir metin yığını gibi duruyordu.
        private void DrawEntry(string title, string? body, string timestamp)
        {
            const double indent = 12;
            var width = ContentWidth - indent;

            var titleHeight = MeasureWrapped(title, _fontLabel, width, LineHeight);
            var bodyHeight = string.IsNullOrWhiteSpace(body) ? 0 : MeasureWrapped(body!, _fontBody, width, LineHeight);
            var total = titleHeight + bodyHeight + 14;
            EnsureSpace(total + 8);

            var top = _y;
            var x = MarginLeft + indent;

            DrawWrapped(title, _fontLabel, BrushInk, x, width, LineHeight);
            _y += titleHeight;

            if (!string.IsNullOrWhiteSpace(body))
            {
                DrawWrapped(body!, _fontBody, BrushInk, x, width, LineHeight);
                _y += bodyHeight;
            }

            _gfx.DrawString(timestamp, _fontSmall, BrushMuted,
                new XRect(x, _y, width, 12), XStringFormats.TopLeft);
            _y += 13;

            // İşaret çizgisi ancak kaydın yüksekliği bilindikten sonra çizilebiliyor.
            _gfx.DrawLine(PenLine, MarginLeft + 2, top, MarginLeft + 2, _y - 2);
            _y += 7;
        }

        // Revizyon/fesih kaydının sonucu. null, sonuç alanları eklenmeden önce
        // oluşmuş eski kayıtları temsil eder; "onaylandı" varsaymak yanlış olur.
        private static string OutcomeText(bool? isApproved) => isApproved switch
        {
            true => "Onaylandı",
            false => "Reddedildi",
            _ => "Sonuç bekliyor"
        };

        private void DrawRight(string text, XFont font, double x, double width, XBrush? brush = null)
        {
            _gfx.DrawString(text, font, brush ?? BrushInk,
                new XRect(x, _y, width, LineHeight), XStringFormats.TopRight);
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
