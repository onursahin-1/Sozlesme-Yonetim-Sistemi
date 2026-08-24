using Sys.Domain;

namespace Sys.UI.ViewModels;

// Bir eki, kategorisi görünür olacak şekilde saran satır görünüm modeli.
//
// Kategori (Sözleşme / Ek / Teminat / Talep …) veritabanında tutuluyordu ama hiçbir
// ekranda gösterilmiyordu. Son Kontrol listesinde "Sözleşme dosyası eksiksiz yüklendi"
// ve "Ek belgeler (teminat, ek dosya) kontrol edildi" diye iki ayrı madde var; ekranda
// yalnızca düz bir dosya adı listesi olduğu için hangi dosyanın hangi kategoriye ait
// olduğu görülemiyor, dolayısıyla bu maddeler doğrulanamıyordu.
public class AttachmentRowViewModel
{
    private readonly Attachment _attachment;

    public AttachmentRowViewModel(Attachment attachment) => _attachment = attachment;

    public Attachment RawAttachment => _attachment;
    public string FileName => _attachment.FileName;

    public string CategoryLabel => _attachment.Category switch
    {
        AttachmentCategory.Talep => "Talep",
        AttachmentCategory.Sozlesme => "Sözleşme",
        AttachmentCategory.Ek => "Ek",
        AttachmentCategory.Teminat => "Teminat",
        AttachmentCategory.Ihlal => "İhlal",
        AttachmentCategory.Fesih => "Fesih",
        _ => _attachment.Category.ToString()
    };

    // Sözleşme metni asıl belge olduğu için vurgulu; teminat parasal yükümlülük
    // taşıdığından ayrı renkte; diğerleri nötr.
    public string CategoryColorHex => _attachment.Category switch
    {
        AttachmentCategory.Sozlesme => "#2D6EA8",
        AttachmentCategory.Teminat => "#B06A00",
        AttachmentCategory.Ihlal => "#A32D2D",
        AttachmentCategory.Fesih => "#A32D2D",
        _ => "#5B6472"
    };

    public string CategoryBgHex => _attachment.Category switch
    {
        AttachmentCategory.Sozlesme => "#E7EEF7",
        AttachmentCategory.Teminat => "#FFF3E0",
        AttachmentCategory.Ihlal => "#FDECEA",
        AttachmentCategory.Fesih => "#FDECEA",
        _ => "#EEF1F6"
    };
}
