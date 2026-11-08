namespace ServiceDesk.Services.Messaging;

// appsettings.json → "Messaging" bölümü. SMS, WhatsApp ve e-posta
// sağlayıcılarının tüm ayarları burada; gizli değerler (ApiKey, SMTP şifresi)
// canlıda ortam değişkeni ya da user-secrets ile verilir:
//   Messaging__Sms__ApiKey=...   Messaging__Email__Smtp__Password=...
public class MessagingSettings
{
    public const string Section = "Messaging";

    public SmsSettings Sms { get; set; } = new();
    public WhatsAppSettings WhatsApp { get; set; } = new();
    public EmailSettings Email { get; set; } = new();
    public QueueSettings Queue { get; set; } = new();
}

public class SmsSettings
{
    // false ise SMS kanalı kapalıdır; OTP ekranında seçenek gizlenir
    public bool Enabled { get; set; } = true;

    // "Logging" = gönderim yalnızca loga yazılır (stub). Gerçek sağlayıcı
    // (Netgsm, İleti Merkezi, Twilio …) eklendiğinde adı buraya yazılır.
    public string Provider { get; set; } = "Logging";

    // Mesajın gönderen başlığı (SMS originator / alfanumerik başlık)
    public string SenderId { get; set; } = "SERVICEDSK";

    public string? ApiUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }

    // OTP metni; {code} ve {minutes} yer tutucuları doldurulur
    public string OtpTemplate { get; set; } =
        "ServiceDesk giriş kodunuz: {code}. Kod {minutes} dakika geçerlidir. Kimseyle paylaşmayın.";

    public int TimeoutSeconds { get; set; } = 10;
}

public class WhatsAppSettings
{
    public bool Enabled { get; set; } = true;
    public string Provider { get; set; } = "Logging";

    // WhatsApp Business API: gönderen numara kimliği ve onaylı şablon adı
    public string? PhoneNumberId { get; set; }
    public string? ApiUrl { get; set; }
    public string? AccessToken { get; set; }
    public string OtpTemplateName { get; set; } = "servicedesk_otp";
    public string LanguageCode { get; set; } = "tr";

    public int TimeoutSeconds { get; set; } = 10;
}

public class EmailSettings
{
    // "Logging" = e-posta loga yazılır, "Smtp" = gerçek SMTP sunucusu
    public string Mode { get; set; } = "Logging";

    public string FromAddress { get; set; } = "servicedesk@localhost";
    public string FromName { get; set; } = "ServiceDesk";

    public SmtpSettings Smtp { get; set; } = new();

    // Bildirim e-postalarındaki "talebi aç" bağlantısının kökü
    public string? PublicBaseUrl { get; set; }
}

public class SmtpSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
}

// E-posta / SMS kuyruk worker'ı ve yeniden deneme politikası
public class QueueSettings
{
    // Worker kaç saniyede bir kuyruğa bakar (sinyal gelirse beklemeden çalışır)
    public int PollSeconds { get; set; } = 15;

    // Bir turda en fazla kaç mesaj
    public int BatchSize { get; set; } = 50;

    // Başarısız gönderim en fazla kaç kez denenir
    public int MaxAttempts { get; set; } = 5;

    // İlk yeniden deneme gecikmesi; her denemede iki katına çıkar
    public int RetryBaseSeconds { get; set; } = 60;

    // Gecikmenin üst sınırı
    public int RetryMaxSeconds { get; set; } = 3600;
}
