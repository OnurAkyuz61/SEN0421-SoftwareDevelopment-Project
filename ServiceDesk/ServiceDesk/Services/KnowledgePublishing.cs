using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Bilgi bankası yayın yönetişimi: bir makaleyi kim görebilir, kim onaya
// gönderebilir, kim onaylayabilir ve her kaydetmede hangi sürüm yazılır.
//
// ISO 9001 md. 7.5.2 dokümante edilmiş bilginin yayımlanmadan önce gözden
// geçirilip onaylanmasını, md. 7.5.3.2 ise değişikliklerinin kontrol
// edilmesini ister. Kural controller'ın içinde dağınık dursa makale başka
// bir yoldan (API, içe aktarma, arka plan işi) güncellendiğinde onay adımı
// atlanabilirdi; tek giriş noktası burası.
//
// Saf sınıf: veritabanına ve HTTP'ye bağımlı değil, bu yüzden birim testi
// yazılabiliyor (bkz. ServiceDesk.Tests).
public static class KnowledgePublishing
{
    public record TransitionResult(bool Allowed, string? Error = null);

    private static readonly TransitionResult Ok = new(true);

    // ───── Görünürlük ────────────────────────────────────────────────────
    //
    // Son kullanıcı yalnızca yayındaki makaleyi görür: taslak bir prosedüre
    // göre işlem yapması, onaylanmamış bilgiyi kurum bilgisi sanması demek.
    // Destek personeli buna ek olarak KENDİ makalelerini (taslağını, onay
    // bekleyenini, reddedilenini) görür — başkasının yarım taslağını değil.
    // Yönetici onay merciidir; kuyruğu görebilmesi için hepsini görür.

    public static bool CanView(KnowledgeArticle article, AppUser? user) =>
        article.Status == KnowledgeArticleStatus.Published
        || user?.Role == UserRole.Admin
        || (user?.Role == UserRole.Agent && article.AuthorId == user.Id);

    // Aynı kuralın sorgu tarafı. Görünürlük iki yerde ayrı ayrı yazılırsa
    // zamanla birbirinden ayrı düşer; ikisi de bu sınıfta dursun.
    public static IQueryable<KnowledgeArticle> VisibleTo(IQueryable<KnowledgeArticle> query, AppUser? user)
    {
        if (user?.Role == UserRole.Admin) return query;

        if (user?.Role == UserRole.Agent)
        {
            var me = user.Id;
            return query.Where(a => a.Status == KnowledgeArticleStatus.Published || a.AuthorId == me);
        }

        return query.Where(a => a.Status == KnowledgeArticleStatus.Published);
    }

    // Makale son kullanıcıya açık mı (chatbot, talebin çözüm makalesi listesi)
    public static bool IsPublic(KnowledgeArticle article) =>
        article.Status == KnowledgeArticleStatus.Published;

    // ───── Yazma yetkisi ─────────────────────────────────────────────────
    //
    // Destek personeli kendi makalesini düzenler, yönetici hepsini.
    // Yayındaki makalede bu kısıt gevşer: yayımlanmış prosedür artık kurum
    // belgesidir, destek ekibinin tamamı güncelleyebilir — değişiklik yine
    // onaydan geçecek.
    public static TransitionResult CanEdit(KnowledgeArticle article, AppUser? user)
    {
        if (user == null || user.Role == UserRole.User)
            return new TransitionResult(false, "Makale düzenleme yetkiniz yok.");

        if (user.Role == UserRole.Admin
            || article.AuthorId == user.Id
            || article.Status == KnowledgeArticleStatus.Published)
            return Ok;

        return new TransitionResult(false, "Bu makale başka bir personelin taslağı; yalnızca sahibi düzenleyebilir.");
    }

    // ───── Onaya gönderme ────────────────────────────────────────────────

    public static TransitionResult CanSubmit(KnowledgeArticle article, AppUser? user)
    {
        var edit = CanEdit(article, user);
        if (!edit.Allowed) return edit;

        return article.Status switch
        {
            KnowledgeArticleStatus.Draft or KnowledgeArticleStatus.Rejected => Ok,
            KnowledgeArticleStatus.PendingApproval => new TransitionResult(false, "Makale zaten onay bekliyor."),
            _ => new TransitionResult(false, "Makale yayında; onaya göndermek için önce içeriğini değiştirin.")
        };
    }

    public static void Submit(KnowledgeArticle article, AppUser user, DateTime nowUtc)
    {
        article.Status = KnowledgeArticleStatus.PendingApproval;
        article.SubmittedAt = nowUtc;
        article.SubmittedById = user.Id;

        // Önceki turun red gerekçesi yeni gönderimde kalmaz: yazar düzeltip
        // gönderdi, gerekçe artık geçerli değil.
        article.RejectionNote = null;
    }

    // ───── Onay / red ────────────────────────────────────────────────────
    //
    // Onay mercii yöneticidir. Makalenin yazarı kendi makalesini onaylayamaz:
    // gözden geçirmenin anlamı ikinci bir göz olmasıdır (md. 7.5.2). Tek
    // yöneticili bir kurulumda yöneticinin yazdığı makaleyi başka bir
    // yöneticinin onaylaması gerekir; kural bilinçli olarak böyle.
    public static TransitionResult CanDecide(KnowledgeArticle article, AppUser? user)
    {
        if (user?.Role != UserRole.Admin)
            return new TransitionResult(false, "Makaleyi yalnızca yönetici onaylayabilir veya reddedebilir.");

        if (article.Status != KnowledgeArticleStatus.PendingApproval)
            return new TransitionResult(false, "Makale onay bekliyor durumunda değil.");

        if (article.AuthorId == user.Id)
            return new TransitionResult(false, "Kendi yazdığınız makaleyi onaylayamazsınız; başka bir yönetici gözden geçirmeli.");

        return Ok;
    }

    public static void Approve(KnowledgeArticle article, AppUser user, DateTime nowUtc)
    {
        article.Status = KnowledgeArticleStatus.Published;
        article.ApprovedAt = nowUtc;
        article.ApprovedById = user.Id;
        article.RejectionNote = null;
    }

    // Red gerekçesi zorunlu: neyi düzeltmesi gerektiğini bilmeyen yazar
    // makaleyi aynı haliyle yeniden onaya gönderir.
    public static TransitionResult CanReject(KnowledgeArticle article, AppUser? user, string? note)
    {
        var decide = CanDecide(article, user);
        if (!decide.Allowed) return decide;

        if (string.IsNullOrWhiteSpace(note))
            return new TransitionResult(false, "Red gerekçesi yazmanız gerekiyor.");

        return Ok;
    }

    public static void Reject(KnowledgeArticle article, AppUser user, string note, DateTime nowUtc)
    {
        article.Status = KnowledgeArticleStatus.Rejected;
        article.RejectionNote = note.Trim();

        // Kararın kendisi de onay kaydıdır: "kim, ne zaman karar verdi".
        // Durum Rejected olduğu için bu alanlar yayın onayı anlamına gelmez.
        article.ApprovedAt = nowUtc;
        article.ApprovedById = user.Id;
    }

    // ───── Kaydetmenin durum üzerindeki etkisi ───────────────────────────
    //
    // Yayındaki bir makalenin içeriği değişirse makale onaya GERİ DÖNER
    // (md. 7.5.3.2). Aksi halde onay kapısı anlamsızlaşırdı: makale bir kez
    // onaylatılıp sonra serbestçe değiştirilebilirdi. Bedeli, düzeltme
    // onaylanana kadar makalenin son kullanıcıya görünmemesidir; bu yüzden
    // arayüzde kaydetmeden önce uyarılır.
    //
    // Onay bekleyen makalenin içeriği değişirse makale kuyruktan ÇIKAR ve
    // taslağa döner: onaylayan kişi önüne gelen metnin değişmediğinden emin
    // olmalı. Yazar düzeltmeyi bitirince yeniden gönderir.
    public static KnowledgeArticleStatus StatusAfterEdit(KnowledgeArticleStatus current, bool contentChanged)
    {
        if (!contentChanged) return current;

        return current switch
        {
            KnowledgeArticleStatus.Published => KnowledgeArticleStatus.PendingApproval,
            KnowledgeArticleStatus.PendingApproval => KnowledgeArticleStatus.Draft,
            KnowledgeArticleStatus.Rejected => KnowledgeArticleStatus.Draft,
            _ => current
        };
    }

    // Kaydetmeyi durum alanlarına uygular. Makalenin METNİ çağıran tarafından
    // zaten yazılmış olur; burada yalnızca yayın durumu yönetilir.
    public static void ApplyEdit(KnowledgeArticle article, AppUser editor, bool contentChanged, DateTime nowUtc)
    {
        var next = StatusAfterEdit(article.Status, contentChanged);
        if (next == article.Status) return;

        if (article.Status == KnowledgeArticleStatus.Published)
        {
            // Yayından onaya dönen makalenin onay kaydı düşer: yayına çıkacak
            // yeni metni henüz kimse onaylamadı. Değişikliği yapan kişi aynı
            // zamanda onaya gönderendir.
            article.ApprovedAt = null;
            article.ApprovedById = null;
            article.SubmittedAt = nowUtc;
            article.SubmittedById = editor.Id;
        }

        if (next == KnowledgeArticleStatus.Draft)
        {
            article.SubmittedAt = null;
            article.SubmittedById = null;
            article.RejectionNote = null;
        }

        article.Status = next;
    }

    // ───── Sürüm geçmişi ─────────────────────────────────────────────────

    // Yeni sürümün numarası: makaleye özel, 1'den başlayan sayaç.
    // Sürümün hiç olmadığı (henüz kaydedilmemiş ya da geçişten önce yazılmış)
    // makalede 1'den başlar.
    public static int NextVersionNo(int? currentMax) => (currentMax ?? 0) + 1;

    public static int NextVersionNo(IEnumerable<KnowledgeArticleRevision> existing) =>
        NextVersionNo(existing.Any() ? existing.Max(r => r.VersionNo) : null);

    // Makalenin o anki halinin anlık görüntüsü
    public static KnowledgeArticleRevision Snapshot(KnowledgeArticle article, int versionNo,
                                                    int createdById, DateTime nowUtc, string? note = null) =>
        new()
        {
            ArticleId = article.Id,
            VersionNo = versionNo,
            Title = article.Title,
            Body = article.Body,
            CategoryId = article.CategoryId,
            CreatedById = createdById,
            CreatedAt = nowUtc,
            Note = note
        };

    // Sürümün içeriği makalenin şu anki halinden farklı mı? Aynıysa kaydetmek
    // (ya da o sürüme geri dönmek) yeni bir sürüm üretmez: geçmiş aynı metnin
    // kopyalarıyla şişerse okunmaz hale gelir.
    public static bool DiffersFrom(KnowledgeArticleRevision revision, KnowledgeArticle article) =>
        revision.Title != article.Title
        || revision.Body != article.Body
        || revision.CategoryId != article.CategoryId;

    // Seçilen sürümün içeriğini makaleye geri yazar. Geri dönüş de bir
    // değişikliktir: makale eski bir metinle kendiliğinden yayında kalmaz,
    // ApplyEdit ile yeniden onaya düşer.
    public static void Restore(KnowledgeArticle article, KnowledgeArticleRevision revision)
    {
        article.Title = revision.Title;
        article.Body = revision.Body;
        article.CategoryId = revision.CategoryId;
    }
}
