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
    public long TicketMaxBytes { get; set; } = 10 * 1024 * 1024;   // 10 MB
    public long CommentMaxBytes { get; set; } = 5 * 1024 * 1024;   // 5 MB

    // Toplam depolama kotası (AttachmentQuota). 0 = sınırsız.
    public long TicketQuotaBytes { get; set; } = 50 * 1024 * 1024;    // talep başına
    public long UserQuotaBytes { get; set; } = 500 * 1024 * 1024;     // kullanıcı başına

    public ScannerSettings Scanner { get; set; } = new();

    public long MaxBytesFor(UploadKind kind) =>
        kind == UploadKind.Comment ? CommentMaxBytes : TicketMaxBytes;
}

// Ekin nereye yüklendiği: dosya başına boyut sınırı buna göre seçilir
public enum UploadKind
{
    Ticket,   // talebin kendi ekleri (talep açma + sonradan dosya ekleme)
    Comment   // yazışma mesajına eklenen dosyalar
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
