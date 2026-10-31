using System.IO.Compression;
using System.Text;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

// Dosya yüklemede uzantı, içerik türü ve dosya imzası birlikte doğrulanmalı;
// görsel olarak açılan dosyalar yalnızca gerçek raster görseller olmalı.
public class UploadPolicyTests
{
    private static readonly byte[] PngBytes =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52 };
    private static readonly byte[] JpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46 };
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7\n%âãÏÓ\n");
    private static readonly byte[] ExeBytes = { 0x4D, 0x5A, 0x90, 0, 3, 0, 0, 0 };
    private static readonly byte[] HtmlBytes = Encoding.UTF8.GetBytes("<html><script>alert(document.cookie)</script></html>");

    private static MemoryStream S(byte[] b) => new(b);

    private static byte[] Zip(params string[] entries)
    {
        var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var e in entries)
            {
                using var w = new StreamWriter(z.CreateEntry(e).Open());
                w.Write("x");
            }
        return ms.ToArray();
    }

    [Fact]
    public void Gercek_png_kabul_edilir()
    {
        Assert.Null(UploadPolicy.Check("ekran.png", "image/png", S(PngBytes)));
    }

    [Fact]
    public void Tur_bildirilmeyen_gercek_dosya_kabul_edilir()
    {
        Assert.Null(UploadPolicy.Check("ekran.png", "application/octet-stream", S(PngBytes)));
        Assert.Null(UploadPolicy.Check("ekran.png", null, S(PngBytes)));
    }

    [Fact]
    public void Uzantisi_png_yapilmis_html_reddedilir()
    {
        Assert.NotNull(UploadPolicy.Check("resim.png", "image/png", S(HtmlBytes)));
    }

    [Fact]
    public void Png_uzantili_ama_html_bildirilen_dosya_reddedilir()
    {
        Assert.NotNull(UploadPolicy.Check("resim.png", "text/html", S(PngBytes)));
    }

    [Fact]
    public void Pdf_adiyla_yuklenen_exe_reddedilir()
    {
        Assert.NotNull(UploadPolicy.Check("fatura.pdf", "application/pdf", S(ExeBytes)));
    }

    [Fact]
    public void Jpeg_uzantili_png_reddedilir()
    {
        Assert.NotNull(UploadPolicy.Check("foto.jpg", "image/jpeg", S(PngBytes)));
    }

    [Theory]
    [InlineData("script.svg")]
    [InlineData("sayfa.html")]
    [InlineData("kurulum.exe")]
    [InlineData("betik.js")]
    [InlineData("dosya")]
    public void Izin_listesi_disindaki_uzanti_reddedilir(string ad)
    {
        Assert.NotNull(UploadPolicy.Check(ad, null, S(PngBytes)));
    }

    [Fact]
    public void Gercek_pdf_ve_jpeg_kabul_edilir()
    {
        Assert.Null(UploadPolicy.Check("rapor.PDF", "application/pdf", S(PdfBytes)));
        Assert.Null(UploadPolicy.Check("foto.jpeg", "image/jpeg", S(JpegBytes)));
    }

    [Fact]
    public void Gercek_docx_kabul_edilir_sahte_docx_reddedilir()
    {
        var docx = Zip("[Content_Types].xml", "word/document.xml");
        Assert.Null(UploadPolicy.Check("rapor.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", S(docx)));

        var sahte = Zip("readme.txt", "virus.exe");
        Assert.NotNull(UploadPolicy.Check("rapor.docx", null, S(sahte)));
    }

    [Fact]
    public void Xlsx_icinde_word_parcasi_olan_arsiv_reddedilir()
    {
        var docx = Zip("[Content_Types].xml", "word/document.xml");
        Assert.NotNull(UploadPolicy.Check("tablo.xlsx", null, S(docx)));
    }

    [Fact]
    public void Metin_dosyasi_kabul_edilir_ikili_icerik_reddedilir()
    {
        Assert.Null(UploadPolicy.Check("hata.log", "text/plain", S(Encoding.UTF8.GetBytes("2026-10-31 HATA: bağlantı koptu\n"))));
        Assert.NotNull(UploadPolicy.Check("notlar.txt", "text/plain", S(ExeBytes)));
        Assert.NotNull(UploadPolicy.Check("notlar.txt", "text/plain", S(new byte[] { 0x41, 0x00, 0x42 })));
    }

    [Fact]
    public void Kontrol_sonrasi_akis_basa_doner()
    {
        var s = S(PngBytes);
        UploadPolicy.Check("ekran.png", "image/png", s);
        Assert.Equal(0, s.Position);
    }

    [Theory]
    [InlineData("ekran.png", "image/png")]
    [InlineData("x.JPG", "image/jpeg")]
    [InlineData("rapor.pdf", "application/pdf")]
    [InlineData("eski.svg", "application/octet-stream")]
    [InlineData("eski.html", "application/octet-stream")]
    public void Icerik_turu_istemciden_degil_uzantidan_turetilir(string ad, string beklenen)
    {
        Assert.Equal(beklenen, UploadPolicy.ContentTypeFor(ad));
    }

    [Theory]
    [InlineData("a.png", true)]
    [InlineData("a.webp", true)]
    [InlineData("a.svg", false)]
    [InlineData("a.pdf", false)]
    [InlineData("a.txt", false)]
    public void Yalnizca_raster_gorseller_tarayicida_acilir(string ad, bool beklenen)
    {
        Assert.Equal(beklenen, UploadPolicy.IsInlineImage(ad));
    }

    [Theory]
    [InlineData("..\\..\\web.config.png", "web.config.png")]
    [InlineData("../../etc/passwd.txt", "passwd.txt")]
    [InlineData("fatura‮gnp.exe.txt", "faturagnp.exe.txt")] // sağdan sola (RTL) hilesi
    [InlineData("rapor\".pdf", "rapor.pdf")]
    [InlineData(".png", "dosya.png")]
    public void Dosya_adi_temizlenir(string giris, string beklenen)
    {
        Assert.Equal(beklenen, UploadPolicy.SafeFileName(giris));
    }
}
