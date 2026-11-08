using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;
using ServiceDesk.Services.Notifications;

namespace ServiceDesk.Controllers;

// Hem portal hem yönetim: her oturum açmış kullanıcı kendi bildirimlerini görür.
[AuthorizeRole]
public class NotificationsController : Controller
{
    private readonly NotificationService _notify;
    private readonly CurrentUser _current;

    public NotificationsController(NotificationService notify, CurrentUser current)
    {
        _notify = notify;
        _current = current;
    }

    // Liste sayfalı: binlerce bildirimi olan kullanıcıda da tek sayfa yüklenir
    public async Task<IActionResult> Index(bool unreadOnly = false, int page = 1)
    {
        var me = _current.User!.Id;
        var (items, pager) = await _notify.GetPageAsync(me, unreadOnly, page);

        ViewBag.UnreadOnly = unreadOnly;
        ViewBag.UnreadCount = await _notify.UnreadCountAsync(me);
        ViewBag.Pager = pager;
        return View(items);
    }

    // Hafif uç nokta: yalnızca okunmamış sayısı (önbellekten). Zil, sayfayı
    // yenilemeden bunu yoklar. Oturum kapalıysa AuthorizeRole giriş sayfasına
    // yönlendirir; istemci bunu 401 gibi değerlendirir.
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> UnreadCount() =>
        Json(new { count = await _notify.UnreadCountAsync(_current.User!.Id) });

    // Bildirime tıklanınca okundu işaretle, ilgili talebe (veya listeye) git.
    // Bildirim oturumdaki kullanıcıya ait değilse hiç bulunmaz.
    [HttpGet]
    public async Task<IActionResult> Open(int id)
    {
        var me = _current.User!;

        var n = await _notify.GetOwnAsync(me.Id, id);
        if (n == null) return NotFound();

        await _notify.MarkReadAsync(me.Id, id);

        if (n.TicketId == null) return RedirectToAction(nameof(Index));

        if (n.Ticket == null)
        {
            TempData["Err"] = "Bildirimin ilgili olduğu talep arşivlendiği için açılamıyor.";
            return RedirectToAction(nameof(Index));
        }

        return _current.IsStaff
            ? RedirectToAction("Details", "Tickets", new { id = n.TicketId })
            : RedirectToAction("Details", "Portal", new { id = n.TicketId });
    }

    // Tek bildirim (id dolu) ya da tümü (id boş; 500'lük partilerle işlenir)
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int? id, string? returnUrl)
    {
        var me = _current.User!.Id;
        if (id != null)
        {
            await _notify.MarkReadAsync(me, id);
        }
        else
        {
            var count = await _notify.MarkAllReadAsync(me);
            TempData["Ok"] = count == 0
                ? "Okunmamış bildirim yok."
                : $"{count} bildirim okundu olarak işaretlendi.";
        }

        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
    }

    // Toplu okundu işaretleme sayfa bazında: ekranda listelenen bildirimler
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPageRead(List<int>? ids, string? returnUrl)
    {
        var count = await _notify.MarkReadAsync(_current.User!.Id, ids ?? new List<int>());
        TempData["Ok"] = $"Bu sayfadaki {count} bildirim okundu olarak işaretlendi.";
        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
    }
}

// Yönetici: gönderilemeyen e-posta / SMS kuyruğu (yeniden deneme durumu)
[AuthorizeRole(UserRole.Admin)]
public class MessageQueueController : Controller
{
    private readonly AppDbContext _db;
    private readonly NotificationDispatcher _dispatcher;
    private readonly NotificationService _notify;

    public MessageQueueController(AppDbContext db, NotificationDispatcher dispatcher, NotificationService notify)
    {
        _db = db;
        _dispatcher = dispatcher;
        _notify = notify;
    }

    public async Task<IActionResult> Index(string? state, int page = 1)
    {
        var q = _db.Notifications.AsNoTracking()
            .Include(n => n.Recipient)
            .Where(n => n.Channel != NotificationChannel.InApp);

        q = state switch
        {
            "failed" => q.Where(n => n.IsFailed),
            "retrying" => q.Where(n => !n.IsSent && !n.IsFailed && n.AttemptCount > 0),
            "pending" => q.Where(n => !n.IsSent && !n.IsFailed),
            "sent" => q.Where(n => n.IsSent),
            _ => q.Where(n => !n.IsSent)
        };

        var (items, pager) = await q.OrderByDescending(n => n.CreatedAt).ToPageAsync(page);

        ViewBag.State = state;
        ViewBag.Pager = pager;
        ViewBag.Counts = new QueueCounts(
            await _db.Notifications.CountAsync(n => n.Channel != NotificationChannel.InApp && !n.IsSent && !n.IsFailed),
            await _db.Notifications.CountAsync(n => n.Channel != NotificationChannel.InApp && !n.IsSent && !n.IsFailed && n.AttemptCount > 0),
            await _db.Notifications.CountAsync(n => n.Channel != NotificationChannel.InApp && n.IsFailed));
        return View(items);
    }

    public sealed record QueueCounts(int Pending, int Retrying, int Failed);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(int id)
    {
        if (await _dispatcher.RequeueAsync(id))
            TempData["Ok"] = "Kayıt yeniden deneme kuyruğuna alındı.";
        else
            TempData["Err"] = "Kayıt bulunamadı veya zaten gönderilmiş.";
        _notify.SignalDelivery();
        return RedirectToAction(nameof(Index), new { state = "pending" });
    }
}
