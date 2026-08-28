// Bu dosya artık kullanılmıyor; silinebilir.
//
// Burada IHoldsUserInput adında bir arayüz vardı: tema/dil değişiminde veri girişi
// ekranlarının yeniden kurulmasını engelliyordu. Geri alındı — dayandığı varsayım
// yanlıştı ve o ekranlar dil değiştiğinde hiç güncellenmiyordu.
//
// Yerini IHasUnsavedInput aldı: ekran yine yeniden kuruluyor ama girilmiş veri
// varsa önce kullanıcıya soruluyor.
