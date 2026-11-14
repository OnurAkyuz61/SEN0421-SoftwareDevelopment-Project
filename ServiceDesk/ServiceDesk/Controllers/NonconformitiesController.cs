using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;
using ServiceDesk.Services.Notifications;

namespace ServiceDesk.Controllers;

// YÖNETİM: Uygunsuzluk ve düzeltici faaliyet kayıtları (ISO 9001 md. 10.2)
//
// Kapanış kuralları NonconformityClosure'da (saf sınıf, birim testi yazılabilir):
// kök neden + düzeltici faaliyet + etkinlik doğrulaması + kanıt dosyası.
[AuthorizeRole(UserRole.Agent, UserRole.Admin)]
public class NonconformitiesController : Controller
{
    private readonly AppDbContext _db;
    private readonly CurrentUser _current;
    private readonly FileStorage _files;
    private readonly AttachmentQuota _quota;
    private readonly NotificationService _notify;
    private readonly QualitySettings _cfg;

    public NonconformitiesController(AppDbContext db, CurrentUser current,
                                     FileStorage files, AttachmentQuota quota,
                                     NotificationService notify, IOptions<QualitySettings> cfg)
    {
        _db = db;
        _current = current;
        _files = files;
        _quota = quota;
        _notify = notify;
        _cfg = cfg.Value;
    }

    public async Task<IActionResult> Index(NonconformityStatus? status)
    {
        var q = _db.Nonconformities
            .AsNoTracking()
            .Include(n => n.Ticket)
            .Include(n => n.Responsible)
            .Include(n => n.VerifiedBy)
            .Include(n => n.Article)
            // Kanıt listesi her kayıtta gösteriliyor (kaldırılanlar global
            // query filter ile düşüyor)
            .Include(n => n.Attachments.OrderBy(a => a.UploadedAt))
                .ThenInclude(a => a.UploadedBy)
            .AsQueryable();

        if (status != null) q = q.Where(n => n.Status == status);

        ViewBag.Status = status;
        ViewBag.OpenCount = await _db.Nonconformities.CountAsync(n => n.Status != NonconformityStatus.Closed);
        ViewBag.Staff = await _db.Users.AsNoTracking()
            .Where(u => u.Role != UserRole.User && u.IsActive)
            .OrderBy(u => u.FirstName).ToListAsync();

        return View(await q
            .OrderBy(n => n.Status)
            .ThenBy(n => n.DueAt)
            .ToListAsync());
    }

    // Kök neden, düzeltici faaliyet, sorumlu, etkinlik doğrulaması ve kanıt dosyası
    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> Update(
        int id, string? rootCause, string? correctiveAction, int? responsibleId,
        DateTime? dueAt, NonconformityStatus status, bool? effectivenessVerified,
        string? verificationNote, List<IFormFile>? files)
    {
        var n = await _db.Nonconformities
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (n == null) return NotFound();

        // Baştaki / sondaki boşluklar zorunluluk kontrolünü atlatmasın:
        // yalnızca boşluktan oluşan metin boş kabul edilir
        rootCause = string.IsNullOrWhiteSpace(rootCause) ? null : rootCause.Trim();
        correctiveAction = string.IsNullOrWhiteSpace(correctiveAction) ? null : correctiveAction.Trim();

        // Sorumlu seçildiyse gerçekten yetkili (destek/yönetici) ve aktif olmalı
        if (responsibleId != null &&
            !await _db.Users.AnyAsync(u => u.Id == responsibleId && u.IsActive && u.Role != UserRole.User))
        {
            TempData["Err"] = "Seçilen kişi uygunsuzluğa sorumlu atanamaz.";
            return RedirectToAction(nameof(Index));
        }

        // Kayıtlı doğrulamayla çelişen sorumlu ataması reddedilir: doğrulamayı
        // yapan kişi sonradan sorumlu seçilirse "ayrı göz" kuralı bozulurdu
        if (NonconformityClosure.CheckResponsible(n, responsibleId) is { } sorumluHatasi)
        {
            TempData["Err"] = sorumluHatasi;
            return RedirectToAction(nameof(Index));
        }

        var me = _current.User!;
        var now = AppTime.UtcNow;

        // Sorumlu DEĞİŞTİ mi: bildirim yalnızca gerçek değişiklikte gider.
        // Her kaydetmede göndermek, aynı kişiye aynı kaydı onlarca kez
        // duyurup bildirimi okunmaz hale getirirdi.
        var sorumluDegisti = n.ResponsibleId != responsibleId;

        // ── Kanıt dosyaları ─────────────────────────────────────────────
        // Yükleme talep ekleriyle aynı katmandan geçer: UploadPolicy (uzantı +
        // bildirilen tür + imza), antivirüs taraması ve kota. Dosya hatası
        // kaydetmeyi iptal etmez — kullanıcının yazdığı metin kaybolmasın —
        // ama dosya da yazılmaz ve kayıt kanıtsız kapanmaz.
        string? dosyaHatasi = null;
        if (files is { Count: > 0 })
        {
            dosyaHatasi = _files.Validate(files, UploadKind.Nonconformity)
                          ?? await _quota.CheckNonconformityAsync(me.Id, n.Id, files);

            if (dosyaHatasi == null)
                foreach (var kanit in await _files.SaveEvidenceAsync(files))
                {
                    kanit.NonconformityId = n.Id;
                    kanit.UploadedById = me.Id;
                    kanit.UploadedAt = now;
                    n.Attachments.Add(kanit);
                }
        }

        n.RootCause = rootCause;
        n.CorrectiveAction = correctiveAction;
        n.ResponsibleId = responsibleId;
        n.DueAt = dueAt == null ? null : AppTime.ToUtc(dueAt.Value);

        // ── Etkinlik doğrulaması ────────────────────────────────────────
        // Doğrulayan kişi, zamanı ve notu kayda yazılır (md. 10.2.2);
        // doğrulayan SORUMLUDAN farklı olmak zorunda.
        var dogrulama = NonconformityClosure.ApplyVerification(
            n, effectivenessVerified == true, me.Id, responsibleId, verificationNote, now);

        // Kapatmak için kök neden, düzeltici faaliyet, etkinlik doğrulaması ve
        // en az bir KANIT dosyası zorunlu (ISO 9001 md. 10.2).
        // Eksikse DURUM HİÇ DEĞİŞMEZ: kayıt ne kapanır, ne de kapalıyken
        // sessizce "İşlemde"ye düşürülür. Yazılan diğer alanlar (ve yüklenen
        // dosyalar) korunur ki kullanıcı formu baştan doldurmak zorunda kalmasın.
        var kapatiliyor = status == NonconformityStatus.Closed;

        // Kanıt = kaldırılmamış ve taramadan temiz geçmiş dosya: indirilemeyen
        // bir dosya denetimde gösterilemez
        var kanitlar = n.Attachments.Where(a => !a.IsDeleted).ToList();
        var gecerliKanit = kanitlar.Count(a => a.IsServable);

        var eksikAlanlar = kapatiliyor
            ? NonconformityClosure.MissingForClose(n, gecerliKanit)
            : new List<string>();

        // Durum yalnızca KAPANIŞ şartları eksikken donar. Dosya ya da doğrulama
        // hatası tek başına "Açık → İşlemde" gibi bir geçişi engellemez:
        // kullanıcı hatayı görür, kaydının geri kalanı işlenir.
        var durumDondu = eksikAlanlar.Count > 0;
        if (!durumDondu)
        {
            n.Status = status;
            n.ClosedAt = kapatiliyor ? (n.ClosedAt ?? now) : null;
        }

        // Sorumluluk devri bildirimi: kaydın yeni sahibi haberdar olmalı
        // (md. 10.2 — düzeltici faaliyetin takibi). Kendine atayan kişiye
        // bildirim gitmez; zaten ekrandadır.
        if (sorumluDegisti && responsibleId is { } yeniSorumlu && yeniSorumlu != me.Id)
            QueueResponsibleNotice(n, yeniSorumlu, me, devir: true);

        await _db.SaveChangesAsync();
        if (sorumluDegisti) _notify.SignalDelivery();

        var mesaj = new List<string>();

        if (durumDondu)
            mesaj.Add($"Uygunsuzluk #{n.Id} kapatılamadı: " +
                      $"{NonconformityClosure.Join(eksikAlanlar)} zorunlu. " +
                      $"Kayıt \"{Ui.NonconformityStatusText(n.Status)}\" durumunda bırakıldı; " +
                      "girdiğiniz diğer bilgiler kaydedildi.");

        if (dogrulama.Error != null) mesaj.Add(dogrulama.Error);
        if (dosyaHatasi != null) mesaj.Add(dosyaHatasi);

        // Dosya yüklenmiş ama hiçbiri taramadan temiz geçmemişse "kanıt dosyası
        // yok" mesajı yanıltıcı olurdu: nedenini yazıyoruz
        if (kapatiliyor && gecerliKanit == 0 && kanitlar.Count > 0)
            mesaj.Add("Yüklenen dosyalar güvenlik taramasından temiz geçmedi; kanıt sayılmıyor.");

        if (mesaj.Count > 0)
        {
            TempData["Err"] = string.Join(" ", mesaj);
            return RedirectToAction(nameof(Index));
        }

        TempData["Ok"] = kapatiliyor
            ? $"Uygunsuzluk #{n.Id} kapatıldı."
            : $"Uygunsuzluk #{n.Id} güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    // Denetimde veya gözlemle tespit edilen uygunsuzluk elle açılır.
    // Sorumlu seçilebilir; boş bırakılırsa kaydı açan kişi sorumlu olur
    // (sahipsiz kalite kaydı hiç işlenmez).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string? description, string? ticketNo, int? responsibleId)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            TempData["Err"] = "Uygunsuzluk açıklaması boş olamaz.";
            return RedirectToAction(nameof(Index));
        }

        // Talep no opsiyonel; yazıldıysa böyle bir talep gerçekten olmalı.
        // "INC-2026-00042" gibi numara ya da "42" / "#42" gibi kayıt Id'si kabul edilir.
        int? ticketId = null;
        if (!string.IsNullOrWhiteSpace(ticketNo))
        {
            var no = ticketNo.Trim();
            int.TryParse(no.TrimStart('#'), out var idNo);

            var ticket = await _db.Tickets.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Number == no || t.Id == idNo);
            if (ticket == null)
            {
                TempData["Err"] = $"\"{no}\" numaralı talep bulunamadı.";
                return RedirectToAction(nameof(Index));
            }
            ticketId = ticket.Id;
        }

        var me = _current.User!;

        // Sorumlu seçildiyse gerçekten yetkili (destek/yönetici) ve aktif olmalı
        if (responsibleId != null &&
            !await _db.Users.AnyAsync(u => u.Id == responsibleId && u.IsActive && u.Role != UserRole.User))
        {
            TempData["Err"] = "Seçilen kişi uygunsuzluğa sorumlu atanamaz.";
            return RedirectToAction(nameof(Index));
        }

        var sorumluId = responsibleId ?? me.Id;
        var now = AppTime.UtcNow;

        var n = new Nonconformity
        {
            TicketId = ticketId,
            Source = NonconformitySource.Manual,
            Status = NonconformityStatus.Open,
            Description = description.Trim(),
            ResponsibleId = sorumluId,
            DetectedAt = now,
            DueAt = now.AddDays(Math.Max(1, _cfg.CorrectiveActionDueDays))
        };
        _db.Nonconformities.Add(n);

        // Bildirim kaydın numarasını yazıyor; numara ancak kaydedildikten
        // sonra bilinir, bu yüzden önce kayıt yazılır
        await _db.SaveChangesAsync();

        // Sorumlu BAŞKASI ise bildirilir: kaydı kendine açan kişiye bildirim
        // gitmez (her kaydetmede değil, sorumluluk gerçekten devredildiğinde).
        var devredildi = sorumluId != me.Id;
        if (devredildi)
        {
            QueueResponsibleNotice(n, sorumluId, me, devir: false);
            await _db.SaveChangesAsync();
            _notify.SignalDelivery();
        }

        TempData["Ok"] = devredildi
            ? "Uygunsuzluk kaydı açıldı ve sorumluya bildirildi."
            : "Uygunsuzluk kaydı açıldı.";
        return RedirectToAction(nameof(Index));
    }

    // Sorumluluk bildirimi. SaveChanges çağırmaz: kayıt ile bildirim aynı
    // işlemde yazılır — ya ikisi birlikte olur ya hiçbiri.
    //
    // Uygunsuzluk talebe bağlı olmadan da açılabildiği için TicketId boş
    // geçebilir; bildirimde talep bağlantısı o zaman görünmez.
    private void QueueResponsibleNotice(Nonconformity n, int recipientId, AppUser actor, bool devir)
    {
        var termin = n.DueAt == null
            ? ""
            : $" Termin: {AppTime.ToLocal(n.DueAt.Value):dd.MM.yyyy HH:mm}.";

        _notify.Queue(recipientId, NotificationType.NonconformityOpened,
            devir
                ? $"Uygunsuzluk sorumluluğu size atandı: #{n.Id}"
                : $"Adınıza uygunsuzluk kaydı açıldı: #{n.Id}",
            $"{actor.FullName}, \"{NonconformityPolicy.Cut(n.Description, 200)}\" kaydının "
            + $"düzeltici faaliyet sorumlusu olarak sizi belirledi."
            + $"{termin} Kök neden ve düzeltici faaliyet bekleniyor.",
            n.TicketId, EmailTemplates.NonconformityOpened);
    }

    // ───── Kanıt dosyaları ───────────────────────────────────────────────

    // Kanıt dosyasını açar / indirir: GET /Nonconformities/Evidence/5
    //
    // Güvenli servis kuralları talep ekleriyle aynı (FileServing): içerik türü
    // diskteki uzantıdan türetilir, yalnızca imzası doğrulanmış görseller
    // tarayıcıda açılır, taramadan geçmemiş dosya hiç servis edilmez.
    public async Task<IActionResult> Evidence(int id, bool download = false)
    {
        var e = await _db.NonconformityAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);
        if (e == null) return NotFound();

        if (!e.IsServable)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Content(e.ScanStatus == AttachmentScanStatus.Suspicious
                ? "Bu dosya güvenlik taramasında şüpheli bulundu; indirilemez."
                : "Bu dosya henüz güvenlik taramasından geçmedi. Lütfen birkaç dakika sonra yeniden deneyin.",
                "text/plain; charset=utf-8");
        }

        // StoredFileName guid + uzantıdır; yine de Uploads dışına çıkan bir yol reddedilir
        if (!FileStorage.IsSafeStoredName(e.StoredFileName)) return NotFound();

        var path = _files.PathOf(e.StoredFileName);
        if (!System.IO.File.Exists(path)) return NotFound();

        var contentType = UploadPolicy.ContentTypeFor(e.StoredFileName);
        FileServing.ApplySafeHeaders(Response);

        if (!download && FileServing.CanShowInline(path, e.StoredFileName))
        {
            FileServing.SetInlineFileName(Response, e.FileName);
            return PhysicalFile(path, contentType);
        }

        return PhysicalFile(path, FileServing.DownloadType(contentType), UploadPolicy.SafeFileName(e.FileName));
    }

    // Kanıt dosyasını kaldırır. Kayıt SİLİNMEZ, işaretlenir (md. 7.5): yanlış
    // yüklenen ya da kişisel veri içeren dosya ekranlardan düşer ama kimin ne
    // zaman kaldırdığı kayıtlı kalır. Kapanmış uygunsuzluğun kanıtı dondurulur.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveEvidence(int id)
    {
        var e = await _db.NonconformityAttachments
            .Include(a => a.Nonconformity)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (e?.Nonconformity == null) return NotFound();

        if (NonconformityClosure.CheckEvidenceRemoval(e.Nonconformity, e) is { } hata)
        {
            TempData["Err"] = hata;
            return RedirectToAction(nameof(Index));
        }

        e.IsDeleted = true;
        e.DeletedAt = AppTime.UtcNow;
        e.DeletedById = _current.User!.Id;

        await _db.SaveChangesAsync();

        TempData["Ok"] = $"\"{e.FileName}\" kanıt listesinden kaldırıldı.";
        return RedirectToAction(nameof(Index));
    }

    // ───── Bilgi bankası makalesi ────────────────────────────────────────

    // "Bu uygunsuzluktan makale oluştur": kök neden ve düzeltici faaliyet
    // makale gövdesine ön-doldurulur (md. 10.3 — sürekli iyileştirme: tekrar
    // eden sorunun çözümü kurum bilgisine dönüşsün).
    //
    // Makale TASLAK doğar ve bilgi bankasının kendi onay akışından geçer
    // (KnowledgePublishing): uygunsuzluk kaydı üzerinden yayına kestirme yok.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateArticle(int id, int? categoryId)
    {
        var n = await _db.Nonconformities.FirstOrDefaultAsync(x => x.Id == id);
        if (n == null) return NotFound();

        if (NonconformityClosure.CheckArticleCreation(n) is { } hata)
        {
            TempData["Err"] = hata;
            return n.ArticleId != null
                ? RedirectToAction("Details", "Knowledge", new { id = n.ArticleId })
                : RedirectToAction(nameof(Index));
        }

        // Kategori seçildiyse gerçekten var ve aktif olmalı
        if (categoryId != null &&
            !await _db.Categories.AnyAsync(c => c.Id == categoryId && c.IsActive))
            categoryId = null;

        var me = _current.User!;
        var now = AppTime.UtcNow;

        var article = new KnowledgeArticle
        {
            Title = NonconformityClosure.ArticleTitle(n),
            Body = NonconformityClosure.ArticleBody(n),
            CategoryId = categoryId,
            AuthorId = me.Id,
            CreatedAt = now,
            Status = KnowledgeArticleStatus.Draft
        };

        // İlk sürüm: bilgi bankasındaki her kaydetme gibi iz bırakır (md. 7.5.3.2)
        article.Revisions.Add(KnowledgePublishing.Snapshot(
            article, KnowledgePublishing.NextVersionNo((int?)null), me.Id, now,
            $"Uygunsuzluk #{n.Id} kaydından oluşturuldu"));

        _db.KnowledgeArticles.Add(article);

        // Bağ gezinti özelliğinden kurulur: makale henüz Id almadığı için
        // ArticleId elle yazılamaz, EF kaydederken doldurur
        n.Article = article;

        await _db.SaveChangesAsync();

        TempData["Ok"] = $"Uygunsuzluk #{n.Id} kaydından taslak makale oluşturuldu. " +
                         "Başlık ve içeriği düzenleyip onaya gönderin.";
        return RedirectToAction("Edit", "Knowledge", new { id = article.Id });
    }
}
