using System.Net;
using System.Text;
using ServiceDesk.Models;

namespace ServiceDesk.Services.Notifications;

// Olay türüne göre ayrı e-posta şablonları: atama, yorum, SLA (uyarı/ihlal)
// ve eskalasyon seviyesine göre (1, 2, 3+) farklı ton ve renk.
// Şablon, bildirimin TemplateKey alanıyla seçilir; boşsa türün varsayılanı.
public static class EmailTemplates
{
    public sealed record Model(
        string RecipientName,
        string Title,
        string Message,
        string? TicketNo,
        string? TicketUrl);

    public sealed record Rendered(string Subject, string Html, string Text);

    private sealed record Spec(string Heading, string Intro, string Accent, string Icon, string SubjectPrefix, string? Action);

    public const string Assignment = "assignment";
    public const string Comment = "comment";
    public const string SlaWarning = "sla.warning";
    public const string SlaBreach = "sla.breach";
    public const string ApprovalPending = "approval.pending";
    public const string ApprovalDecided = "approval.decided";
    public const string ApprovalReminder = "approval.reminder";
    public const string NewTicket = "ticket.new";
    public const string ChangeImplementation = "change.implementation";
    public const string Digest = "digest";
    public const string SurveyReminder = "survey.reminder";
    public const string NonconformityOpened = "nonconformity.opened";

    // Eskalasyon seviyesine göre şablon anahtarı: 1, 2 ve 3 ve üzeri ayrı
    public static string Escalation(int level) => $"escalation.l{Math.Clamp(level, 1, 3)}";

    public static string DefaultKey(NotificationType type) => type switch
    {
        NotificationType.Assigned => Assignment,
        NotificationType.NewComment => Comment,
        NotificationType.SlaWarning => SlaWarning,
        NotificationType.SlaBreach => SlaBreach,
        NotificationType.Escalation => Escalation(1),
        NotificationType.ApprovalPending => ApprovalPending,
        NotificationType.ApprovalDecided => ApprovalDecided,
        NotificationType.ApprovalReminder => ApprovalReminder,
        NotificationType.NewTicket => NewTicket,
        NotificationType.ChangeImplementation => ChangeImplementation,
        NotificationType.DailyDigest => Digest,
        NotificationType.SurveyReminder => SurveyReminder,
        NotificationType.NonconformityOpened => NonconformityOpened,
        _ => Comment
    };

    private static Spec SpecFor(string key) => key switch
    {
        Assignment => new("Size yeni bir talep atandı",
            "Aşağıdaki talep artık sizin sorumluluğunuzda. SLA hedefini kaçırmamak için en kısa sürede ilk yanıtı verin.",
            "#2563eb", "👤", "Atama", "Talebi aç"),
        Comment => new("Talebe yeni yanıt yazıldı",
            "Takip ettiğiniz talepte yeni bir mesaj var.",
            "#0f766e", "💬", "Yanıt", "Yazışmayı gör"),
        SlaWarning => new("SLA süresi yaklaşıyor",
            "Talebin çözüm süresi dolmak üzere. Süre aşılırsa talep otomatik olarak eskalasyona alınır.",
            "#d97706", "⏳", "SLA uyarısı", "Talebe git"),
        SlaBreach => new("SLA ihlal edildi",
            "Talebin çözüm süresi aşıldı. Kayıt uygunsuzluk olarak işlendi ve eskalasyon başladı.",
            "#dc2626", "⛔", "SLA ihlali", "Hemen müdahale et"),
        "escalation.l1" => new("Eskalasyon · Seviye 1",
            "Süresi aşılan talep ilk eskalasyon seviyesine yükseltildi. Lütfen talebi devralın veya ekibe yönlendirin.",
            "#ea580c", "⬆️", "Eskalasyon S1", "Talebi devral"),
        "escalation.l2" => new("Eskalasyon · Seviye 2 (ekip lideri / yönetim)",
            "Talep birinci seviyede çözülemedi ve yönetim seviyesine taşındı. Önceliği artırıldı; kaynak ataması gerekiyor.",
            "#c2410c", "⚠️", "Eskalasyon S2", "Kaynak ata"),
        "escalation.l3" => new("Eskalasyon · Kritik seviye",
            "Talep en üst eskalasyon seviyesine ulaştı. Hizmet taahhüdü ciddi biçimde aşılıyor; acil karar gerekiyor.",
            "#991b1b", "🚨", "KRİTİK eskalasyon", "Acil incele"),
        ApprovalPending => new("Onayınızı bekleyen değişiklik",
            "Bir değişiklik talebi onay zincirinde sizin kararınızı bekliyor.",
            "#7c3aed", "📝", "Onay bekliyor", "Değişikliği incele"),
        ApprovalReminder => new("Hatırlatma: bekleyen değişiklik onayı",
            "Aşağıdaki değişiklik talebi bir süredir onay bekliyor. Planlanan uygulama penceresi kaçırılmadan karar verilmesi gerekiyor.",
            "#7c3aed", "🔔", "Onay hatırlatma", "Karar ver"),
        ApprovalDecided => new("Değişiklik talebiniz karara bağlandı",
            "Onay zinciri tamamlandı.",
            "#16a34a", "✅", "Onay kararı", "Talebi gör"),
        NewTicket => new("Yeni talep açıldı",
            "Destek kuyruğuna yeni bir talep düştü.",
            "#16a34a", "➕", "Yeni talep", "Talebi aç"),
        ChangeImplementation => new("Değişiklik uygulama durumu güncellendi",
            "Onaylanan değişikliğin uygulama / geri alma durumu değişti.",
            "#0369a1", "🛠️", "Uygulama", "Ayrıntılar"),
        NonconformityOpened => new("Adınıza bir uygunsuzluk kaydı açıldı",
            "Aşağıdaki talepte kalite kaydı doğuran bir durum tespit edildi. Kök neden ve düzeltici faaliyetin termin tarihine kadar girilmesi gerekiyor (ISO 9001 md. 10.2).",
            "#b45309", "📌", "Uygunsuzluk", "Kaydı incele"),
        SurveyReminder => new("Talebinizi değerlendirmeyi unuttunuz mu?",
            "Çözülen talebiniz için kısa bir memnuniyet anketi bekliyor. Birkaç soruluk bu değerlendirme, hizmetin nerede iyileştirilmesi gerektiğini görmemizi sağlıyor.",
            "#0f766e", "⭐", "Anket hatırlatma", "Anketi doldur"),
        Digest => new("Günlük özetiniz",
            "Son 24 saatte size ait talepler ve bildirimler:",
            "#334155", "📋", "Günlük özet", "Panele git"),
        _ => new("ServiceDesk bildirimi", "", "#334155", "ℹ️", "Bildirim", "Aç")
    };

    public static Rendered Render(string key, Model m)
    {
        var s = SpecFor(key);
        var e = (string? x) => WebUtility.HtmlEncode(x ?? "");

        var subject = m.TicketNo == null
            ? (m.Title.StartsWith(s.SubjectPrefix, StringComparison.OrdinalIgnoreCase)
                ? $"[ServiceDesk] {m.Title}"
                : $"[ServiceDesk] {s.SubjectPrefix}: {m.Title}")
            : $"[ServiceDesk] {s.SubjectPrefix} · {m.TicketNo}: {StripTicketNo(m.Title, m.TicketNo)}";

        // Mesaj çok satırlı olabilir (günlük özet); satırlar <br> olur
        var messageHtml = e(m.Message).Replace("\n", "<br>");

        var html = new StringBuilder()
            .Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:600px;margin:0 auto;color:#1f2937\">")
            .Append($"<div style=\"border-top:4px solid {s.Accent};padding:20px 24px;background:#ffffff\">")
            .Append($"<h2 style=\"margin:0 0 8px;font-size:18px;color:{s.Accent}\">{s.Icon} {e(s.Heading)}</h2>")
            .Append($"<p style=\"margin:0 0 12px\">Merhaba {e(m.RecipientName)},</p>")
            .Append(string.IsNullOrEmpty(s.Intro) ? "" : $"<p style=\"margin:0 0 12px;color:#4b5563\">{e(s.Intro)}</p>")
            .Append("<div style=\"background:#f9fafb;border:1px solid #e5e7eb;border-radius:6px;padding:12px 16px;margin:12px 0\">")
            .Append($"<div style=\"font-weight:600\">{e(m.Title)}</div>")
            .Append($"<div style=\"margin-top:6px;color:#374151\">{messageHtml}</div>")
            .Append(m.TicketNo == null ? "" : $"<div style=\"margin-top:6px;font-size:12px;color:#6b7280\">Talep no: {e(m.TicketNo)}</div>")
            .Append("</div>")
            .Append(m.TicketUrl == null || s.Action == null ? "" :
                $"<p><a href=\"{e(m.TicketUrl)}\" style=\"display:inline-block;background:{s.Accent};color:#fff;" +
                $"padding:8px 16px;border-radius:6px;text-decoration:none\">{e(s.Action)}</a></p>")
            .Append("<p style=\"color:#9ca3af;font-size:12px;margin-top:20px\">ServiceDesk · ISO 9001 IT Servis Masası. " +
                    "Bildirim tercihlerinizi Profil sayfasından değiştirebilirsiniz.</p>")
            .Append("</div></div>")
            .ToString();

        var text = new StringBuilder()
            .AppendLine($"{s.Heading}")
            .AppendLine()
            .AppendLine($"Merhaba {m.RecipientName},")
            .AppendLine(s.Intro)
            .AppendLine()
            .AppendLine(m.Title)
            .AppendLine(m.Message)
            .AppendLine(m.TicketNo == null ? "" : $"Talep no: {m.TicketNo}")
            .AppendLine(m.TicketUrl ?? "")
            .ToString();

        return new Rendered(subject, html, text);
    }

    // Başlık "Olay: INC-… Konu" biçimindeyse konu satırında yalnızca "Konu" kalır
    // (olay adı ve numara zaten önekte var)
    private static string StripTicketNo(string title, string ticketNo)
    {
        var i = title.IndexOf(ticketNo, StringComparison.Ordinal);
        if (i < 0) return title;
        var rest = title[(i + ticketNo.Length)..].Trim(' ', ':', '·');
        return rest.Length == 0 ? title : rest;
    }

    // SMS için kısa metin (160 karakteri aşmasın)
    public static string Sms(string title, string? ticketNo)
    {
        var text = $"ServiceDesk: {title}";
        if (ticketNo != null && !text.Contains(ticketNo)) text += $" ({ticketNo})";
        return text.Length > 160 ? text[..157] + "..." : text;
    }
}
