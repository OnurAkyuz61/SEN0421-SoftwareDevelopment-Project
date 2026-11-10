using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Toplam depolama kotası.
//
// FileStorage.Validate dosya BAŞINA boyutu sınırlıyor; bu tek başına diski
// korumuyordu, çünkü sınırın hemen altında yüzlerce dosya yüklenebiliyordu.
// Bu yüzden yükleme öncesi iki toplam daha kontrol edilir:
//   • Talep başına    : tek talebe yığılan eklerin toplamı
//   • Kullanıcı başına: kişinin yüklediği tüm eklerin toplamı
//
// Sayımda ölçüt DİSKTE YER KAPLAYAN kayıtlar: yumuşak silinmiş ekler
// (IsDeleted) sayılmaz — kullanıcı yer açmak için eski dosyasını silebilsin —
// ama arşivlenmiş talebin ekleri sayılır, çünkü dosya diskte duruyor.
// Global query filter ikisini birden düşürdüğü için sorgular
// IgnoreQueryFilters ile kurulup koşul elle yazılır.
public class AttachmentQuota
{
    private readonly AppDbContext _db;
    private readonly UploadSettings _cfg;

    public AttachmentQuota(AppDbContext db, IOptions<UploadSettings> cfg)
    {
        _db = db;
        _cfg = cfg.Value;
    }

    // Aşım varsa kullanıcıya gösterilecek Türkçe mesajı döner, yoksa null.
    // ticketId yeni talep açılırken null gelir (henüz kayıt yok).
    public async Task<string?> CheckAsync(int userId, int? ticketId, IList<IFormFile>? files)
    {
        if (files == null || files.Count == 0) return null;

        var gelen = files.Sum(f => f.Length);

        if (ticketId != null && _cfg.TicketQuotaBytes > 0)
        {
            var kullanilan = await ToplamAsync(a => a.TicketId == ticketId.Value);
            if (kullanilan + gelen > _cfg.TicketQuotaBytes)
                return Mesaj("Bu talebin dosya alanı", _cfg.TicketQuotaBytes, kullanilan, gelen);
        }

        if (_cfg.UserQuotaBytes > 0)
        {
            var kullanilan = await ToplamAsync(a => a.UploadedById == userId);
            if (kullanilan + gelen > _cfg.UserQuotaBytes)
                return Mesaj("Toplam dosya alanınız", _cfg.UserQuotaBytes, kullanilan, gelen);
        }

        return null;
    }

    private Task<long> ToplamAsync(
        System.Linq.Expressions.Expression<Func<Attachment, bool>> kapsam) =>
        _db.Attachments.IgnoreQueryFilters().Where(a => !a.IsDeleted).Where(kapsam)
           .SumAsync(a => a.Size);

    // Kullanıcı ne olduğunu ve ne yapabileceğini anlasın: sınır, kullanılan,
    // göndermeye çalışılan ve kalan yer birlikte yazılır
    private static string Mesaj(string ne, long sinir, long kullanilan, long gelen)
    {
        var kalan = Math.Max(0, sinir - kullanilan);
        return $"{ne} dolu. Sınır {Ui.FileSize(sinir)}, kullanılan {Ui.FileSize(kullanilan)}, " +
               $"kalan {Ui.FileSize(kalan)}; göndermek istediğiniz dosyalar {Ui.FileSize(gelen)}. " +
               $"Gereksiz ekleri kaldırıp yeniden deneyin.";
    }
}
