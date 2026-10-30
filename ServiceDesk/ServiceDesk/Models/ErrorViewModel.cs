namespace ServiceDesk.Models;

// Hata sayfasının içeriği. Kullanıcıya teknik ayrıntı değil, ne olduğu ve
// ne yapabileceği anlatılır; takip numarası (RequestId) kayıtla eşleştirmek
// için gösterilir.
public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public int StatusCode { get; set; } = 500;

    public string Title { get; set; } = "Beklenmeyen bir hata oluştu";

    public string Message { get; set; } = "";

    // Bootstrap Icons sınıfı
    public string Icon { get; set; } = "bi-exclamation-triangle";

    // Kullanıcının yapabileceği şey (varsa)
    public string? Hint { get; set; }

    // Tüm hata yanıtları (404, 403, 500, yakalanan istisna) aynı yerden
    // metinlendirilir; böylece ekranlar arasında dil ve ton tutarlı kalır.
    public static ErrorViewModel For(int statusCode, string? requestId) => statusCode switch
    {
        400 => new ErrorViewModel
        {
            StatusCode = 400,
            Title = "İstek anlaşılamadı",
            Message = "Gönderilen bilgiler okunamadı. Form uzun süre açık kaldıysa oturum "
                    + "doğrulaması düşmüş olabilir.",
            Hint = "Sayfayı yenileyip işlemi yeniden deneyin.",
            Icon = "bi-exclamation-circle",
            RequestId = requestId
        },
        401 or 403 => new ErrorViewModel
        {
            StatusCode = statusCode,
            Title = "Bu sayfaya erişim yetkiniz yok",
            Message = "Hesabınızın rolü bu ekranı görmeye veya bu işlemi yapmaya yetkili değil.",
            Hint = "Yetkinizin değişmesi gerekiyorsa yöneticinize başvurun.",
            Icon = "bi-shield-lock",
            RequestId = requestId
        },
        404 => new ErrorViewModel
        {
            StatusCode = 404,
            Title = "Kayıt bulunamadı",
            Message = "Aradığınız sayfa ya da kayıt yok. Kayıt arşivlenmiş, numarası yanlış "
                    + "yazılmış veya erişim yetkiniz dışında olabilir.",
            Hint = "Adresi kontrol edin ya da listeden ilerleyin.",
            Icon = "bi-search",
            RequestId = requestId
        },
        405 => new ErrorViewModel
        {
            StatusCode = 405,
            Title = "Bu işlem böyle yapılamıyor",
            Message = "İstek, sayfanın beklediği biçimde gelmedi.",
            Hint = "İşlemi ekrandaki düğmeleri kullanarak yeniden deneyin.",
            Icon = "bi-slash-circle",
            RequestId = requestId
        },
        413 => new ErrorViewModel
        {
            StatusCode = 413,
            Title = "Gönderilen dosyalar çok büyük",
            Message = "Yüklenen dosyalar izin verilen toplam boyutu aşıyor.",
            Hint = "En fazla 5 dosya ve dosya başına 10 MB yükleyebilirsiniz.",
            Icon = "bi-file-earmark-x",
            RequestId = requestId
        },
        429 => new ErrorViewModel
        {
            StatusCode = 429,
            Title = "Çok fazla istek gönderildi",
            Message = "Kısa sürede çok sayıda istek alındı.",
            Hint = "Bir süre bekleyip yeniden deneyin.",
            Icon = "bi-hourglass-split",
            RequestId = requestId
        },
        503 => new ErrorViewModel
        {
            StatusCode = 503,
            Title = "Servis şu anda kullanılamıyor",
            Message = "Uygulama geçici olarak yanıt veremiyor; bakım veya yoğunluk olabilir.",
            Hint = "Birkaç dakika sonra yeniden deneyin.",
            Icon = "bi-plug",
            RequestId = requestId
        },
        >= 500 => new ErrorViewModel
        {
            StatusCode = statusCode,
            Title = "Beklenmeyen bir hata oluştu",
            Message = "İşlem tamamlanamadı. Hata kaydedildi; verilerinizde değişiklik "
                    + "yapılmadıysa işlemi yeniden deneyebilirsiniz.",
            Hint = "Sorun sürerse aşağıdaki takip numarasıyla destek ekibine başvurun.",
            Icon = "bi-exclamation-triangle",
            RequestId = requestId
        },
        _ => new ErrorViewModel
        {
            StatusCode = statusCode,
            Title = "İstek tamamlanamadı",
            Message = "İşlem sırasında beklenmeyen bir durum oluştu.",
            Hint = "Sayfayı yenileyip yeniden deneyin.",
            Icon = "bi-exclamation-circle",
            RequestId = requestId
        }
    };
}
