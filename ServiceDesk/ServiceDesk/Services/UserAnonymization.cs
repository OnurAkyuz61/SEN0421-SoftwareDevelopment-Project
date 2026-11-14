using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Models;

namespace ServiceDesk.Services;

// KVKK: kişisel veriler silinir / yer tutucuyla değiştirilir; Id ve talep
// geçmişi FK ile kalır. Hesap silinmez — anonimleştirilip pasifleştirilir.
public static class UserAnonymizationPolicy
{
    public const string FirstNamePlaceholder = "Anonim";
    public const string EmailDomain = "anonymized.local";

    public static string EmailPlaceholder(int userId) => $"anon-{userId}@{EmailDomain}";

    public static string LastNamePlaceholder(int userId) => $"#{userId}";

    // Yer tutucu e-posta alan adı ile tanınır (anon-{id}@anonymized.local).
    public static bool IsAnonymized(string email) =>
        !string.IsNullOrEmpty(email)
        && email.EndsWith("@" + EmailDomain, StringComparison.OrdinalIgnoreCase);

    public static bool IsAnonymized(AppUser user) => IsAnonymized(user.Email);

    public static void ApplyToUser(AppUser user, string unusablePasswordHash)
    {
        user.FirstName = FirstNamePlaceholder;
        user.LastName = LastNamePlaceholder(user.Id);
        user.Email = EmailPlaceholder(user.Id);
        user.PhoneNumber = null;
        user.PasswordHash = unusablePasswordHash;
        user.PasswordChangedAt = null;
        user.MustChangePassword = false;
        user.DepartmentId = null;
        user.TeamId = null;
        user.IsActive = false;
        user.LastLoginAt = null;
        user.DailyDigestEnabled = false;
        user.LastDigestSentAt = null;
    }
}

public class UserAnonymization
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<AppUser> _hasher;
    private readonly InactiveUserTicketRelease _ticketRelease;
    private readonly AuditService _audit;

    public UserAnonymization(AppDbContext db, IPasswordHasher<AppUser> hasher,
                             InactiveUserTicketRelease ticketRelease, AuditService audit)
    {
        _db = db;
        _hasher = hasher;
        _ticketRelease = ticketRelease;
        _audit = audit;
    }

    public sealed record Result(int ReleasedTickets, bool AlreadyAnonymized);

    // Kullanıcı tracked olmalı. SaveChanges çağırmaz.
    public async Task<Result> ApplyAsync(AppUser user, int byUserId)
    {
        if (UserAnonymizationPolicy.IsAnonymized(user))
            return new Result(0, AlreadyAnonymized: true);

        var displayBefore = user.FullName;
        var oldEmail = user.Email;
        var wasActive = user.IsActive;

        var released = 0;
        if (InactiveUserTicketReleasePolicy.ShouldRelease(wasActive, isActive: false))
        {
            var release = await _ticketRelease.ApplyAsync(user.Id, byUserId, displayBefore);
            released = release.Released;
        }

        var junk = _hasher.HashPassword(user, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        UserAnonymizationPolicy.ApplyToUser(user, junk);

        var histories = await _db.PasswordHistories.Where(h => h.UserId == user.Id).ToListAsync();
        if (histories.Count > 0) _db.PasswordHistories.RemoveRange(histories);

        var resets = await _db.PasswordResetTokens.Where(t => t.UserId == user.Id).ToListAsync();
        if (resets.Count > 0) _db.PasswordResetTokens.RemoveRange(resets);

        var sessions = await _db.UserActiveSessions.Where(s => s.UserId == user.Id).ToListAsync();
        if (sessions.Count > 0) _db.UserActiveSessions.RemoveRange(sessions);

        // Aynı e-posta / UserId ile giriş denemelerindeki kişisel e-postayı scrub et
        var attempts = await _db.LoginAttempts
            .Where(a => a.UserId == user.Id || a.Email == oldEmail)
            .ToListAsync();
        foreach (var a in attempts)
            a.Email = user.Email;

        var invites = await _db.UserInvites.Where(i => i.Email == oldEmail).ToListAsync();
        foreach (var inv in invites)
        {
            inv.Email = user.Email;
            inv.FirstName = UserAnonymizationPolicy.FirstNamePlaceholder;
            inv.LastName = UserAnonymizationPolicy.LastNamePlaceholder(user.Id);
            inv.UsedAt ??= AppTime.UtcNow;
        }

        // Denetim satırlarındaki denormalize e-posta
        var audits = await _db.AuditLogs.Where(a => a.UserId == user.Id && a.Email != null).ToListAsync();
        foreach (var a in audits)
            a.Email = user.Email;

        _audit.Queue(AuditAction.UserAnonymized, byUserId,
            detail: $"Kullanıcı #{user.Id} anonimleştirildi (KVKK). Önceki görünen ad scrub edildi; " +
                    $"{released} açık talep bırakıldı.");

        return new Result(released, AlreadyAnonymized: false);
    }
}
