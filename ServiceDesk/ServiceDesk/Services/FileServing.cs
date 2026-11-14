using Microsoft.Net.Http.Headers;

namespace ServiceDesk.Services;

// Yüklenen bir dosyanın tarayıcıya GÜVENLİ servis edilmesi.
//
// Kurallar AttachmentsController.Get içinde yazılmıştı; uygunsuzluk kanıtı da
// (NonconformityAttachment) aynı şekilde servis edilmek zorunda. İki yere
// kopyalanırsa zamanla ayrı düşerler — biri "nosniff" alır, öteki almaz —
// bu yüzden tek yerde duruyor:
//   • İçerik türü veritabanındaki değerden değil, diskteki dosyanın
//     uzantısından türetilir (UploadPolicy).
//   • Yalnızca PNG/JPEG/GIF/WEBP tarayıcıda açılır ve açılmadan önce dosyanın
//     imzası bir kez daha kontrol edilir; tutmazsa indirme olarak gelir.
//   • nosniff: tarayıcı türü içeriğe bakıp "tahmin" edemez.
//   • CSP sandbox: görsel yeni sekmede açılsa bile betik çalışamaz, form
//     gönderemez, çerezlere erişemez.
//   • Önbellek yok: ortak bilgisayarda oturum kapandıktan sonra kalmasın.
public static class FileServing
{
    public static void ApplySafeHeaders(HttpResponse response)
    {
        var h = response.Headers;
        h[HeaderNames.XContentTypeOptions] = "nosniff";
        h[HeaderNames.ContentSecurityPolicy] = "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'; sandbox";
        h["Cross-Origin-Resource-Policy"] = "same-origin";
        h[HeaderNames.CacheControl] = "private, no-store";
        h["Referrer-Policy"] = "no-referrer";
    }

    // inline gösterim için güvenli ad: "Farklı kaydet" de doğru adı önerir
    public static void SetInlineFileName(HttpResponse response, string fileName)
    {
        var cd = new ContentDispositionHeaderValue("inline");
        cd.SetHttpFileName(fileName);
        response.Headers[HeaderNames.ContentDisposition] = cd.ToString();
    }

    // Görsel olmayan türler olduğu gibi; imzası tutmayan "görsel" genel ikili
    // tür olarak iner — tarayıcı içeriği yorumlamaz
    public static string DownloadType(string contentType) =>
        contentType.StartsWith("image/", StringComparison.Ordinal) ? "application/octet-stream" : contentType;

    // Dosya tarayıcıda gösterilebilir bir görsel mi (imzası da doğrulanarak)
    public static bool CanShowInline(string path, string storedFileName) =>
        UploadPolicy.IsInlineImage(storedFileName) && ImageSignatureMatches(path, storedFileName);

    private static bool ImageSignatureMatches(string path, string storedName)
    {
        try
        {
            using var fs = File.OpenRead(path);
            return UploadPolicy.Check(storedName, null, fs) == null;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
