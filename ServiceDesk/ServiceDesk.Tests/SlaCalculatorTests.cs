using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

// SLA hedef tarihi hesabı: mesai saatleri, hafta sonu ve resmî tatiller.
// Bu hesap yanlış olursa bütün SLA raporlaması yanlış olur, bu yüzden
// projedeki ilk birim testler buraya yazıldı.
public class SlaCalculatorTests
{
    // Pazartesi–Cuma 09:00–18:00 (günde 9 saat)
    private static BusinessCalendar Takvim() => new()
    {
        Id = 1,
        Name = "Test Mesai",
        WorkStartMinutes = 9 * 60,
        WorkEndMinutes = 18 * 60,
        WorkDays = "1,2,3,4,5"
    };

    // 29 Ekim 2026 Perşembe — Cumhuriyet Bayramı
    private static HashSet<DateTime> Tatiller() => new() { new DateTime(2026, 10, 29) };

    // Yerel saatle verilen başlangıca dakika ekler, sonucu yerel saatle döner
    private static DateTime Hesapla(DateTime yerelBaslangic, int dakika, bool tatilVar = true)
    {
        var sonucUtc = SlaCalculator.AddWorkingMinutes(
            AppTime.ToUtc(yerelBaslangic), dakika, Takvim(), tatilVar ? Tatiller() : null);
        return AppTime.ToLocal(sonucUtc);
    }

    [Fact]
    public void Mesai_icinde_baslayan_is_ayni_gun_biter()
    {
        // Pazartesi 10:00 + 4 saat
        var sonuc = Hesapla(new DateTime(2026, 10, 19, 10, 0, 0), 240);
        Assert.Equal(new DateTime(2026, 10, 19, 14, 0, 0), sonuc);
    }

    [Fact]
    public void Gun_sonuna_sigmayan_is_ertesi_is_gunune_tasar()
    {
        // Pazartesi 16:00 + 4 saat: 2 saat bugün, 2 saat yarın
        var sonuc = Hesapla(new DateTime(2026, 10, 19, 16, 0, 0), 240);
        Assert.Equal(new DateTime(2026, 10, 20, 11, 0, 0), sonuc);
    }

    [Fact]
    public void Hafta_sonu_sayilmaz()
    {
        // Cuma 17:00 + 8 saat: 1 saat Cuma, kalan 7 saat Pazartesi
        var sonuc = Hesapla(new DateTime(2026, 10, 23, 17, 0, 0), 480);
        Assert.Equal(new DateTime(2026, 10, 26, 16, 0, 0), sonuc);
    }

    [Fact]
    public void Mesai_disinda_acilan_talep_mesai_basinda_saymaya_baslar()
    {
        // Cumartesi öğlen açılan talep Pazartesi 09:00'da saymaya başlar
        var sonuc = Hesapla(new DateTime(2026, 10, 24, 12, 0, 0), 60);
        Assert.Equal(new DateTime(2026, 10, 26, 10, 0, 0), sonuc);
    }

    [Fact]
    public void Mesai_oncesi_acilan_talep_ayni_gun_saymaya_baslar()
    {
        var sonuc = Hesapla(new DateTime(2026, 10, 19, 7, 0, 0), 60);
        Assert.Equal(new DateTime(2026, 10, 19, 10, 0, 0), sonuc);
    }

    [Fact]
    public void Resmi_tatil_atlanir()
    {
        // Çarşamba 09:00 + 18 saat: Çarşamba 9, Perşembe TATİL, Cuma 9 saat
        var sonuc = Hesapla(new DateTime(2026, 10, 28, 9, 0, 0), 1080);
        Assert.Equal(new DateTime(2026, 10, 30, 18, 0, 0), sonuc);
    }

    [Fact]
    public void Tatil_tanimli_degilse_o_gun_de_calisilir()
    {
        var sonuc = Hesapla(new DateTime(2026, 10, 28, 9, 0, 0), 1080, tatilVar: false);
        Assert.Equal(new DateTime(2026, 10, 29, 18, 0, 0), sonuc);
    }

    [Fact]
    public void Dusuk_oncelik_72_mesai_saati_sekiz_is_gunu_eder()
    {
        var sonuc = Hesapla(new DateTime(2026, 10, 19, 9, 0, 0), 4320);
        Assert.Equal(new DateTime(2026, 10, 28, 18, 0, 0), sonuc);
    }

    [Fact]
    public void Takvim_yoksa_7x24_sayilir()
    {
        // Kritik öncelik mesai saatine bağlı değildir
        var baslangic = AppTime.ToUtc(new DateTime(2026, 10, 23, 17, 0, 0));
        var sonuc = AppTime.ToLocal(SlaCalculator.AddWorkingMinutes(baslangic, 240, calendar: null));
        Assert.Equal(new DateTime(2026, 10, 23, 21, 0, 0), sonuc);
    }

    [Fact]
    public void Sifir_veya_negatif_sure_baslangici_degistirmez()
    {
        var baslangic = AppTime.ToUtc(new DateTime(2026, 10, 19, 10, 0, 0));
        Assert.Equal(baslangic, SlaCalculator.AddWorkingMinutes(baslangic, 0, Takvim()));
        Assert.Equal(baslangic, SlaCalculator.AddWorkingMinutes(baslangic, -30, Takvim()));
    }

    [Fact]
    public void Bozuk_takvim_yapilandirmasinda_takvim_saatine_dusulur()
    {
        // Bitiş başlangıçtan önce: sessizce yanlış hedef üretmek yerine 7x24 sayar
        var bozuk = Takvim();
        bozuk.WorkEndMinutes = bozuk.WorkStartMinutes;

        var baslangic = AppTime.ToUtc(new DateTime(2026, 10, 19, 10, 0, 0));
        var sonuc = SlaCalculator.AddWorkingMinutes(baslangic, 120, bozuk);

        Assert.Equal(baslangic.AddMinutes(120), sonuc);
    }
}
