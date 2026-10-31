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
}
