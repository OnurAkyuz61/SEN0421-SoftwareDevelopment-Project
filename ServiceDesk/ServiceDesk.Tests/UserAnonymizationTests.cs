using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

public class UserAnonymizationTests
{
    [Fact]
    public void EmailPlaceholder_benzersiz_ve_kisa()
    {
        var email = UserAnonymizationPolicy.EmailPlaceholder(42);
        Assert.Equal("anon-42@anonymized.local", email);
        Assert.True(email.Length <= 150);
    }

    [Fact]
    public void ApplyToUser_pii_scrub_eder()
    {
        var user = new AppUser
        {
            Id = 7,
            FirstName = "Ayşe",
            LastName = "Yılmaz",
            Email = "ayse@ornek.com",
            PhoneNumber = "+905551112233",
            PasswordHash = "hash",
            PasswordChangedAt = DateTime.UtcNow,
            MustChangePassword = true,
            DepartmentId = 1,
            TeamId = 2,
            IsActive = true,
            LastLoginAt = DateTime.UtcNow,
            DailyDigestEnabled = true,
            LastDigestSentAt = DateTime.UtcNow
        };

        UserAnonymizationPolicy.ApplyToUser(user, "unusable");

        Assert.Equal("Anonim", user.FirstName);
        Assert.Equal("#7", user.LastName);
        Assert.Equal("anon-7@anonymized.local", user.Email);
        Assert.Null(user.PhoneNumber);
        Assert.Equal("unusable", user.PasswordHash);
        Assert.Null(user.PasswordChangedAt);
        Assert.False(user.MustChangePassword);
        Assert.Null(user.DepartmentId);
        Assert.Null(user.TeamId);
        Assert.False(user.IsActive);
        Assert.Null(user.LastLoginAt);
        Assert.False(user.DailyDigestEnabled);
        Assert.Null(user.LastDigestSentAt);
        Assert.True(UserAnonymizationPolicy.IsAnonymized(user));
    }

    [Theory]
    [InlineData("anon-3@anonymized.local", true)]
    [InlineData("ayse@ornek.com", false)]
    [InlineData("", false)]
    public void IsAnonymized_dogru_tespit(string email, bool beklenen)
    {
        Assert.Equal(beklenen, UserAnonymizationPolicy.IsAnonymized(email));
    }
}
