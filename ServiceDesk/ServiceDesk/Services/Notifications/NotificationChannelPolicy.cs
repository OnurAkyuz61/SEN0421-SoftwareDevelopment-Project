using ServiceDesk.Models;

namespace ServiceDesk.Services.Notifications;

// Bildirim türüne göre kanal seçimi: uygulama içi / e-posta / SMS.
//
// Varsayılanlar burada; kullanıcı Profil → Bildirim tercihleri ekranından
// tür bazında değiştirebilir (NotificationPreference). Bazı türlerde
// uygulama içi kanal kapatılamaz (Locked), örn. onay bekleyen değişiklik:
// işin akışı bu kayda bağlı.
//
// Saf sınıf: birim testi yazılabilir.
public static class NotificationChannelPolicy
{
    public sealed record Channels(bool InApp, bool Email, bool Sms);

    public sealed record TypeRule(Channels Default, bool InAppLocked, bool SmsAllowed);

    private static readonly Dictionary<NotificationType, TypeRule> Rules = new()
    {
        [NotificationType.NewTicket]        = new(new(true, true, false),  InAppLocked: false, SmsAllowed: false),
        [NotificationType.Assigned]         = new(new(true, true, false),  InAppLocked: true,  SmsAllowed: true),
        [NotificationType.NewComment]       = new(new(true, true, false),  InAppLocked: false, SmsAllowed: false),
        [NotificationType.SlaWarning]       = new(new(true, true, false),  InAppLocked: false, SmsAllowed: true),
        [NotificationType.SlaBreach]        = new(new(true, true, true),   InAppLocked: true,  SmsAllowed: true),
        [NotificationType.Escalation]       = new(new(true, true, true),   InAppLocked: true,  SmsAllowed: true),
        [NotificationType.ApprovalPending]  = new(new(true, true, false),  InAppLocked: true,  SmsAllowed: true),
        [NotificationType.ApprovalDecided]  = new(new(true, true, false),  InAppLocked: false, SmsAllowed: false),
        [NotificationType.ApprovalReminder] = new(new(true, true, false),  InAppLocked: false, SmsAllowed: true),
        [NotificationType.ChangeImplementation] = new(new(true, true, false), InAppLocked: false, SmsAllowed: false),
        // Uygunsuzluk sorumluluğu: uygulama içi bildirim kapatılamaz
        // (InAppLocked). Kalite kaydının sahibi "görmedim" diyemez — md. 10.2
        // düzeltici faaliyetin takip edilmesini ister.
        [NotificationType.NonconformityOpened] = new(new(true, true, false), InAppLocked: true, SmsAllowed: false),
        // Termin hatırlatması aynı sorumluluğun devamı: uygulama içi kanal
        // yine kapatılamaz. Geciken düzeltici faaliyet denetimde açık bulgu
        // olduğu için SMS'e de izin verilir (varsayılan kapalı; kullanıcı açar).
        [NotificationType.NonconformityDueReminder] = new(new(true, true, false), InAppLocked: true, SmsAllowed: true),
        // Telefonsuz aktif hesap: OTP giriş yapılamaz. Yöneticiye uygulama içi
        // uyarı kilitli; e-posta varsayılan açık, SMS yok (hedefin telefonu yok).
        [NotificationType.MissingPhone] = new(new(true, true, false), InAppLocked: true, SmsAllowed: false),
        // Günlük özet yalnızca e-postadır; tercihi AppUser.DailyDigestEnabled ile yönetilir
        [NotificationType.DailyDigest]      = new(new(false, true, false), InAppLocked: false, SmsAllowed: false),
        // Anket hatırlatma da yalnızca e-postadır (SurveyReminderService
        // NotificationService.QueueEmail kullanır). Talep başına bir kez gider
        // ve anket süresi dolunca hiç gitmez; profilde ayrı bir tercih satırı
        // açmıyoruz — kapatılabilir olması gerekirse Survey:ReminderEnabled
        // yapılandırması tüm kurum için kapatır.
        [NotificationType.SurveyReminder]   = new(new(false, true, false), InAppLocked: false, SmsAllowed: false),
    };

    // Profil ekranında listelenen (kullanıcının düzenleyebildiği) türler.
    // Yalnızca e-posta ile giden ve tercihe bakılmadan gönderilen türler
    // (günlük özet, anket hatırlatma) listede yer almaz: ekranda kapatılabilir
    // görünüp yine gönderilmesi kullanıcıyı yanıltırdı.
    public static IEnumerable<NotificationType> ConfigurableTypes =>
        Rules.Keys.Where(t => t is not (NotificationType.DailyDigest or NotificationType.SurveyReminder));

    public static TypeRule RuleFor(NotificationType type) =>
        Rules.TryGetValue(type, out var r) ? r : new(new(true, false, false), false, false);

    public static Channels Default(NotificationType type) => RuleFor(type).Default;

    // Kullanıcı tercihi + kurallar + kişinin iletişim bilgisi → gerçek kanallar.
    //   • Kilitli türde uygulama içi her zaman açık.
    //   • SMS yalnızca izin verilen türlerde ve telefon tanımlıysa.
    //   • E-posta, kullanıcının adresi yoksa düşer.
    public static Channels Resolve(NotificationType type, NotificationPreference? pref,
                                   bool hasEmail, bool hasPhone)
    {
        var rule = RuleFor(type);
        var inApp = pref?.InApp ?? rule.Default.InApp;
        var email = pref?.Email ?? rule.Default.Email;
        var sms = pref?.Sms ?? rule.Default.Sms;

        if (rule.InAppLocked) inApp = true;
        if (!rule.SmsAllowed || !hasPhone) sms = false;
        if (!hasEmail) email = false;

        return new Channels(inApp, email, sms);
    }
}
