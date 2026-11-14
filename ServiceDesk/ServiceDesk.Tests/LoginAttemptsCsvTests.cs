using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

public class LoginAttemptsCsvTests
{
    [Fact]
    public void Build_baslik_ve_satir_uret()
    {
        var attempt = new LoginAttempt
        {
            Email = "ali@ornek.com",
            Result = LoginResult.WrongPassword,
            IpAddress = "1.2.3.4",
            UserId = 7,
            User = new AppUser { FirstName = "Ali", LastName = "Yılmaz" },
            AttemptedAt = new DateTime(2026, 5, 1, 12, 30, 0, DateTimeKind.Utc)
        };

        var csv = LoginAttemptsCsv.Build(new[] { LoginAttemptsCsv.From(attempt) });

        Assert.StartsWith("Zaman;E-posta;Sonuç;IP;Kullanıcı Id;Kullanıcı", csv);
        Assert.Contains("ali@ornek.com", csv);
        Assert.Contains("Şifre hatalı", csv);
        Assert.Contains("1.2.3.4", csv);
        Assert.Contains("Ali Yılmaz", csv);
    }
}
