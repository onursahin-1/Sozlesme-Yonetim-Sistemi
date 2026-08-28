using System;

namespace Sys.Services;

// Servis katmanının reddetme sebepleri.
//
// NEDEN KOD, METİN DEĞİL?
// İş kuralı dil bilmemeli. Servis "Bu işlemi yapma yetkiniz yok." döndürdüğü sürece
// arayüz o metni ya olduğu gibi basmak ya da içinde kelime aramak zorunda kalıyordu —
// nitekim şifre ekranı tam olarak bunu yapıyordu:
//
//     if (result.ErrorMessage.Contains("Mevcut şifreniz")) ...
//
// Metin çevrildiği an bu koşul sessizce tutmaz olurdu: hata patlamaz, sadece yanlış
// yerde görünürdü. Kod döndürünce karar metne değil kimliğe bağlanıyor.
//
// Değerler int olarak HİÇBİR YERE YAZILMIYOR (veritabanında karşılığı yok), bu yüzden
// araya yeni değer eklemek güvenli.
public enum AppError
{
    // Yetki
    NotAuthorized,
    NoAuditLogAccess,
    NoAttachmentAccess,
    CannotEditRequest,
    CannotDisableOwnAccount,

    // Durum / akış
    ApprovalNotAvailableAtThisStage,
    RequestAlreadyProcessed,
    RequestClosedByRejection,
    RequestNoLongerPending,
    OnlyUnconvertedRequestsCanBeRejected,
    ContractNotLive,
    PendingEditExists,
    PendingTerminationExists,
    ViolationAlreadyResolved,

    // Doğrulama
    RejectionNoteRequired,
    ResolutionNoteRequired,
    DatesRequired,
    EndDateBeforeStart,
    AtLeastOneItemRequired,

    // Kimlik doğrulama
    UserNotFound,
    InvalidCredentials,
    AccountDisabled,
    AccountLocked,
    TooManyAttempts,
    NewPasswordSameAsCurrent,
    CurrentPasswordIncorrect,
    PasswordEmpty,
    PasswordTooShort,
    PasswordNeedsLetter,
    PasswordNeedsDigit,
}

// Servis katmanının fırlattığı iş kuralı hatası.
//
// InvalidOperationException'DAN TÜREMESİ bilinçli: çağıran taraf zaten bu türü
// yakalıyordu, davranış değişmiyor. Yeni olan tek şey, sebebin metin yerine kodla
// taşınması.
//
// Message'ın nasıl metne dönüştüğü ARAYÜZDE belirleniyor (Describe). Servis katmanı
// hangi dilde çalışıldığını bilmiyor ve bilmemeli; sadece "ne oldu"yu söylüyor.
// Describe atanmadıysa (birim testlerde durum bu) kodun adı dönüyor — test çıktısında
// yine anlaşılır bir metin görünüyor.
public sealed class AppException : InvalidOperationException
{
    public static Func<AppError, object?[], string>? Describe;

    public AppError Error { get; }
    public object?[] Args { get; }

    public AppException(AppError error, params object?[] args)
    {
        Error = error;
        Args = args;
    }

    public override string Message => Describe?.Invoke(Error, Args) ?? Error.ToString();
}
