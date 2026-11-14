using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Ek dosyayı kim açabilir / indirebilir.
//
// Önceki kontrol yalnızca "talep benim mi" diye bakıyordu; bu yüzden talep
// sahibi, kendi talebine destek ekibinin yazdığı İÇ NOTA eklenmiş dosyayı
// doğrudan /Attachments/Get/{id} adresinden indirebiliyordu. Oysa iç notlar
// portalda hiç gösterilmiyor. Kural artık ekrandaki görünürlükle aynı.
//
// Saf sınıf: birim testi yazılabiliyor (bkz. ServiceDesk.Tests).
public static class AttachmentPolicy
{
    // Dosyanın bağlı olduğu talep (mesaja eklendiyse mesajın kendisi de) yüklenmiş olmalı:
    // Include(a => a.Ticket) ve Include(a => a.Comment)
    public static bool CanView(Attachment file, AppUser user)
    {
        if (file.Ticket == null) return false;

        // Arşivlenmiş talep: yalnızca destek / yönetici (arşiv ekranı); son kullanıcıya kapalı
        if (file.Ticket.IsDeleted && user.Role == UserRole.User) return false;

        // Destek ve yönetici her eke erişir (iç notlar da onların işi)
        if (user.Role != UserRole.User) return true;

        // Son kullanıcı yalnızca kendi talebinin eklerine
        if (file.Ticket.RequesterId != user.Id) return false;

        // ... ve iç nota eklenmiş dosyaya değil
        return file.Comment == null || !file.Comment.IsInternal;
    }

    // Ek dosyayı kim KALDIRABİLİR (yumuşak silme).
    //
    // Görme hakkı ön koşul: göremediği dosyayı kimse silemez. Üstüne:
    //   • Zaten silinmiş ek yeniden silinmez.
    //   • Arşivlenmiş talebin eki dondurulur — arşiv denetim kaydıdır,
    //     içeriği sonradan değiştirilmez (ISO 9001 md. 7.5).
    //   • Destek ve yönetici talebin her ekini kaldırabilir (yanlış yüklenen
    //     ya da kişisel veri içeren dosyanın temizlenmesi onların işi).
    //   • Son kullanıcı yalnızca KENDİ yüklediği dosyayı ve yalnızca talep
    //     kapanmadan önce kaldırabilir; kapanan talebin ekleri kayıt olur.
    //
    // Dosyanın bağlı olduğu talep (ve mesaja eklendiyse mesaj) yüklenmiş olmalı.
    public static bool CanDelete(Attachment file, AppUser user)
    {
        if (!CanView(file, user)) return false;

        if (file.IsDeleted) return false;
        if (file.Ticket!.IsDeleted) return false;

        if (user.Role != UserRole.User) return true;

        if (file.UploadedById != user.Id) return false;
        return file.Ticket.Status != TicketStatus.Closed;
    }

    // Dosya indirilmeye hazır mı: antivirüs taraması temiz sonuçlandı mı?
    //
    // Yetkiden ayrı bir kapı — "kim görebilir" ile "ne servis edilir" farklı
    // sorular. Taranmamış (Pending), şüpheli (Suspicious) ve taranamamış
    // (Failed) dosya güvenli sayılmaz. Skipped yalnızca tarama yapılandırmada
    // kapatıldığında oluşur; bu bilinçli bir karardır, dosya servis edilir.
    public static bool IsServable(Attachment file) =>
        file.ScanStatus is AttachmentScanStatus.Clean or AttachmentScanStatus.Skipped;
}
