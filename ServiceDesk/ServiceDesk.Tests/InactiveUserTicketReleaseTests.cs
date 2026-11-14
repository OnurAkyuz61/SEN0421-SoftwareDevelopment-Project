using ServiceDesk.Services;

namespace ServiceDesk.Tests;

public class InactiveUserTicketReleaseTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void ShouldRelease_yalnizca_aktiften_pasife(bool onceki, bool simdi, bool beklenen)
    {
        Assert.Equal(beklenen, InactiveUserTicketReleasePolicy.ShouldRelease(onceki, simdi));
    }
}
