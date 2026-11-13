using ServiceDesk.Models;

namespace ServiceDesk.Services;

// Destek personelinin hangi talepleri görebileceği.
// Ekibi tanımlı destek personeli yalnızca kendi ekibine düşen talepleri ve
// kendisine atanmış talepleri görür. Yönetici ve ekibi olmayan destek
// personeli (ör. genel havuz) tüm talepleri görür.
public static class TicketScope
{
    public static bool IsRestricted(AppUser user) => user.Role == UserRole.Agent && user.TeamId != null;

    public static IQueryable<Ticket> VisibleTo(this IQueryable<Ticket> query, AppUser user)
    {
        if (!IsRestricted(user)) return query;
        var teamId = user.TeamId;
        var userId = user.Id;
        return query.Where(t => t.AssignedTeamId == teamId || t.AssigneeId == userId);
    }

    public static bool CanSee(AppUser user, Ticket ticket) =>
        !IsRestricted(user) || ticket.AssignedTeamId == user.TeamId || ticket.AssigneeId == user.Id;

    // ───── Her rol için görünürlük ───────────────────────────────────────
    //
    // Yukarıdaki VisibleTo/CanSee yönetim tarafının (TicketsController)
    // kuralıdır ve YALNIZCA destek personelini kısıtlar: son kullanıcı için
    // IsRestricted false döner, yani VisibleTo ona her talebi açar. Bugüne
    // kadar sorun olmamasının tek sebebi o ekranların
    // [AuthorizeRole(Agent, Admin)] ile korunması; son kullanıcı kendi
    // taleplerini portalda "RequesterId == ben" süzgeciyle görüyor.
    //
    // Global arama ilk uç ki her rol aynı sorguyu çalıştırıyor. Bu yüzden
    // "rolü ne olursa olsun bu kullanıcı hangi talepleri görebilir" kuralı
    // açıkça burada yazılı: son kullanıcı yalnızca KENDİ talebini, destek
    // personeli ekip kapsamını, yönetici hepsini.
    //
    // Kural VisibleTo'nun içine gömülmedi: oradaki davranışı değiştirmek
    // yönetim ekranlarının sözleşmesini sessizce değiştirmek olurdu.
    public static IQueryable<Ticket> SearchableBy(this IQueryable<Ticket> query, AppUser user) =>
        user.Role == UserRole.User
            ? query.Where(t => t.RequesterId == user.Id)
            : query.VisibleTo(user);

    public static bool CanSearch(AppUser user, Ticket ticket) =>
        user.Role == UserRole.User
            ? ticket.RequesterId == user.Id
            : CanSee(user, ticket);
}
