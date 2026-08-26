using Sys.Infrastructure;

namespace Sys.Services.Tests;

// Ek dosyası adının diske yazılmadan önce temizlenmesi.
//
// Kullanıcıya gösterilen ad (Attachment.FileName) değişmez; buradaki kurallar
// yalnızca DOSYA SİSTEMİNDEKİ adı ilgilendiriyor.
public class AttachmentFileNameTests
{
    [Fact]
    public void Sanitize_NormalName_Unchanged()
        => Assert.Equal("Sozlesme Metni.pdf",
                        AttachmentFileHelper.SanitizeFileName("Sozlesme Metni.pdf", ".pdf"));

    [Fact]
    public void Sanitize_KeepsTurkishCharacters()
        => Assert.Equal("Şirket Sözleşmesi.pdf",
                        AttachmentFileHelper.SanitizeFileName("Şirket Sözleşmesi.pdf", ".pdf"));

    // Ayırıcı içeren hiçbir ad diskte klasör değiştiremez. Sonucun tam olarak ne
    // olacağı platforma göre değişebilir (Linux'ta ters bölü geçerli bir karakter);
    // güvenlik açısından önemli olan, sonuçta ayırıcı KALMAMASI.
    [Theory]
    [InlineData("../../gizli.pdf")]
    [InlineData("..\\..\\gizli.pdf")]
    [InlineData("klasor/alt/dosya.pdf")]
    public void Sanitize_PathSeparators_Removed(string raw)
    {
        var result = AttachmentFileHelper.SanitizeFileName(raw, ".pdf");

        Assert.DoesNotContain("/", result);
        Assert.DoesNotContain("\\", result);
    }

    // Windows sondaki nokta ve boşluğu sessizce kırpıyor; ad "dosya." iken diske
    // "dosya" olarak yazılıyor ve varlık kontrolü şaşıyordu.
    [Theory]
    [InlineData("dosya.")]
    [InlineData("dosya ")]
    [InlineData("  dosya  ")]
    public void Sanitize_TrailingDotsAndSpaces_Trimmed(string raw)
        => Assert.Equal("dosya.pdf", AttachmentFileHelper.SanitizeFileName(raw + ".pdf", ".pdf"));

    // Ayrılmış aygıt adları: CON.pdf oluşturulamaz ve hata mesajı kullanıcıya
    // hiçbir şey anlatmaz.
    [Theory]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("PRN")]
    [InlineData("COM1")]
    [InlineData("NUL")]
    public void Sanitize_ReservedDeviceNames_Prefixed(string raw)
    {
        var result = AttachmentFileHelper.SanitizeFileName(raw + ".pdf", ".pdf");

        Assert.StartsWith("_", result);
    }

    // Adın tamamı geçersizse ya da boşsa yine kullanılabilir bir ad çıkmalı.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData(null)]
    public void Sanitize_EmptyOrDotsOnly_FallsBackToDefault(string? raw)
        => Assert.Equal("belge.pdf", AttachmentFileHelper.SanitizeFileName(raw, ".pdf"));

    // Uzun adlar yol sınırını aşıp anlaşılmaz bir hata veriyordu. Uzantı korunur.
    [Fact]
    public void Sanitize_VeryLongName_TruncatedButKeepsExtension()
    {
        var raw = new string('a', 400) + ".pdf";

        var result = AttachmentFileHelper.SanitizeFileName(raw, ".pdf");

        Assert.True(result.Length <= 104, $"Ad çok uzun kaldı: {result.Length}");
        Assert.EndsWith(".pdf", result);
    }

    // Uzantı çağırandan geliyor (izin verilen listeden doğrulanmış hâli);
    // addaki uzantı ne olursa olsun sonuç ona göre yazılır.
    [Fact]
    public void Sanitize_UsesProvidedExtension()
        => Assert.Equal("rapor.xlsx", AttachmentFileHelper.SanitizeFileName("rapor.pdf", ".xlsx"));
}
