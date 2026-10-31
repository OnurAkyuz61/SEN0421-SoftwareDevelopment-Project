using ServiceDesk.Services;

namespace ServiceDesk.Tests;

// Aynı talebi iki kişi aynı anda düzenlerse ikincisi birincinin değişikliğini
// görmeden üzerine yazamamalı.
public class TicketConcurrencyTests
{
    private static readonly byte[] V1 = { 0, 0, 0, 0, 0, 0, 0x07, 0xD1 };
    private static readonly byte[] V2 = { 0, 0, 0, 0, 0, 0, 0x07, 0xD2 };

    [Fact]
    public void Ayni_surum_eski_sayilmaz()
    {
        Assert.False(TicketConcurrency.IsStale(V1, (byte[])V1.Clone()));
    }

    [Fact]
    public void Arada_degisen_talep_eski_sayilir()
    {
        Assert.True(TicketConcurrency.IsStale(V2, V1));
    }

    [Fact]
    public void Surum_gonderilmediyse_kontrol_yapilmaz()
    {
        Assert.False(TicketConcurrency.IsStale(V2, null));
    }

    [Fact]
    public void Bozuk_surum_degeri_cakisma_sayilir()
    {
        Assert.True(TicketConcurrency.IsStale(V1, TicketConcurrency.Decode("bu-base64-degil!")));
    }

    [Fact]
    public void Surum_formdan_gidip_gelince_bozulmaz()
    {
        var encoded = TicketConcurrency.Encode(V1);
        Assert.False(TicketConcurrency.IsStale(V1, TicketConcurrency.Decode(encoded)));
    }
}
