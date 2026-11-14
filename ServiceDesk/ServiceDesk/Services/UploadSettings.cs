namespace ServiceDesk.Services;

// appsettings.json → "Uploads" bölümü.
//
// Bu sınırlar daha önce FileStorage içinde const olarak duruyordu; kurumun
// dosya boyutunu veya kotayı değiştirmesi için projenin yeniden derlenmesi
// gerekiyordu. Artık yapılandırmadan okunuyor:
//   Uploads__TicketMaxBytes=20971520   (ortam değişkeni ile de verilebilir)
//
// Sunucunun istek gövdesi sınırı ayrıdır ([RequestSizeLimit] 60 MB);
// buradaki değerler onun altında kalmalıdır.
public class UploadSettings
{
    public const string Section = "Uploads";

    // Tek seferde gönderilebilecek dosya sayısı
    public int MaxFiles { get; set; } = 5;

    // Dosya başına boyut, ekin nereye yüklendiğine göre AYRI:
    //   • Talep eki : ekran görüntüsü, kayıt dosyası, kurulum paketi — geniş
    //   • Yorum eki : yazışma içinde hızlı paylaşım — daha dar tutulur ki
    //                 uzun bir yazışma tek başına diski doldurmasın
    //   • Uygunsuzluk kanıtı: denetim kaydı (ölçüm çıktısı, imzalı form,
    //                 ekran görüntüsü) — talep ekiyle aynı genişlikte
    public long TicketMaxBytes { get; set; } = 10 * 1024 * 1024;   // 10 MB
    public long CommentMaxBytes { get; set; } = 5 * 1024 * 1024;   // 5 MB
    public long NonconformityMaxBytes { get; set; } = 10 * 1024 * 1024;   // 10 MB

    // Toplam depolama kotası (AttachmentQuota). 0 = sınırsız.
    public long TicketQuotaBytes { get; set; } = 50 * 1024 * 1024;    // talep başına
    public long UserQuotaBytes { get; set; } = 500 * 1024 * 1024;     // kullanıcı başına
    public long NonconformityQuotaBytes { get; set; } = 50 * 1024 * 1024;  // uygunsuzluk başına

    public ScannerSettings Scanner { get; set; } = new();

    // Yumuşak silinmiş eklerin kalıcı temizliği (AttachmentRetentionService)
    public RetentionSettings Retention { get; set; } = new();

    // Uploads/ içindeki sahipsiz dosya taraması (OrphanAttachmentScanService)
    public OrphanScanSettings OrphanScan { get; set; } = new();

    public long MaxBytesFor(UploadKind kind) => kind switch
    {
        UploadKind.Comment => CommentMaxBytes,
        UploadKind.Nonconformity => NonconformityMaxBytes,
        _ => TicketMaxBytes
    };
}

// Ekin nereye yüklendiği: dosya başına boyut sınırı buna göre seçilir
public enum UploadKind
{
    Ticket,         // talebin kendi ekleri (talep açma + sonradan dosya ekleme)
    Comment,        // yazışma mesajına eklenen dosyalar
    Nonconformity   // uygunsuzluğun kapanış kanıtı (md. 10.2.2)
}

// Antivirüs tarama ayarları. Messaging:Sms bölümüyle aynı desende:
// Enabled + Provider + sağlayıcı bilgileri.
public class ScannerSettings
{
    // false ise tarama hiç yapılmaz; dosyalar "tarama kapsamı dışı" (Skipped)
    // işaretlenir ve indirilebilir kalır. Canlıda gerçek sağlayıcı bağlanınca
    // true kalmalı — kapatmak taramayı atlamak demektir.
    public bool Enabled { get; set; } = true;

    // "Logging" = tarama yalnızca loga yazılır (stub). Gerçek sağlayıcı
    // (ClamAV, Windows Defender, bulut tarama servisi) eklendiğinde adı buraya
    // yazılır; IAttachmentScanner arayüzü sabit kalır.
    public string Provider { get; set; } = "Logging";

    public string? ApiUrl { get; set; }
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
}

// Kaldırılan ekin ne kadar süre diskte bekletileceği: "Uploads:Retention".
//
// Ek kaldırıldığında kayıt silinmez, işaretlenir (Attachment.IsDeleted) ve
// dosya diskte durur — yanlışlıkla silmenin geri alınabilmesi ve denetimde
// "ne kaldırıldı" sorusunun cevaplanabilmesi için. Bu süre dolduğunda dosya
// artık savunulabilir bir kayıt değil, yalnızca yer tutuyor demektir:
// AttachmentRetentionService diskten ve tablodan kalıcı olarak siler
// (ISO 9001 md. 7.5.3: kayıtların elden çıkarılması da tanımlı olmalıdır).
public class RetentionSettings
{
    // false ise temizlik job'ı hiç çalışmaz; kaldırılan dosyalar süresiz birikir
    public bool Enabled { get; set; } = true;

    // Kaldırılmasından kaç gün sonra kalıcı olarak silinsin
    public int Days { get; set; } = 90;

    // Taramanın tekrar aralığı (dakika). Varsayılan: günde bir
    public int SweepMinutes { get; set; } = 1440;

    // Bir turda en fazla kaç ek temizlensin. Birikmiş eski kayıtlar tek
    // hamlede değil, turlara bölünerek silinir ki uzun süren bir işlem
    // veritabanını ve diski meşgul etmesin.
    public int BatchSize { get; set; } = 200;
}

// Sahipsiz dosya taraması: "Uploads:OrphanScan".
//
// Uploads/ klasöründe Attachments tablosunda karşılığı olmayan dosyalar
// birikebilir: yükleme sırasında isteğin yarıda kesilmesi, elle kopyalanan
// dosyalar, veritabanının geri yüklenip diskin olduğu gibi kalması. Bunlar
// hiçbir ekrandan görünmez ama diski doldurur.
public class OrphanScanSettings
{
    public bool Enabled { get; set; } = true;

    // Taramanın tekrar aralığı (dakika). Varsayılan: günde bir
    public int ScanMinutes { get; set; } = 1440;

    // Dosya bu yaşa gelmeden sahipsiz sayılmaz. Dosya diske, ek kaydından
    // ÖNCE yazılır (FileStorage.SaveAsync); süren bir yüklemeyi sahipsiz
    // sanmamak için yeni dosyalara dokunulmaz.
    public int MinAgeMinutes { get; set; } = 60;

    // Varsayılan KAPALI: ilk sürüm yalnızca rapor eder. Açılmadan önce
    // raporun doğruluğu loglardan doğrulanmalıdır — burada yanlış pozitif,
    // kullanıcının gerçek dosyasını silmek demektir.
    public bool DeleteFiles { get; set; }

    // Rapor satırında en fazla kaç dosya adı yazılsın (log şişmesin)
    public int MaxReported { get; set; } = 20;
}
