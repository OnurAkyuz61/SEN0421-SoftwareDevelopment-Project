using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

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

    public async Task<IActionResult> Index(bool unreadOnly = false)
    {
        ViewBag.UnreadOnly = unreadOnly;
        ViewBag.UnreadCount = await _notify.UnreadCountAsync(_current.User!.Id);
        return View(await _notify.GetForAsync(_current.User!.Id, unreadOnly));
    }

    // Bildirime tıklanınca okundu işaretle, ilgili talebe (veya listeye) git
    [HttpGet]
    public async Task<IActionResult> Open(int id)
    {
        var list = await _notify.GetForAsync(_current.User!.Id, take: 500);
        var n = list.FirstOrDefault(x => x.Id == id);
        if (n == null) return RedirectToAction(nameof(Index));

        await _notify.MarkReadAsync(_current.User!.Id, id);

        if (n.TicketId == null) return RedirectToAction(nameof(Index));

        return _current.IsStaff
            ? RedirectToAction("Details", "Tickets", new { id = n.TicketId })
            : RedirectToAction("Details", "Portal", new { id = n.TicketId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int? id, string? returnUrl)
    {
        await _notify.MarkReadAsync(_current.User!.Id, id);

        if (id == null) TempData["Ok"] = "Bildirimler okundu olarak işaretlendi.";
        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
    }
}
