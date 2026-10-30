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

    // Bildirime tıklanınca okundu işaretle, ilgili talebe (veya listeye) git.
    // Bildirim oturumdaki kullanıcıya ait değilse hiç bulunmaz: başka bir
    // kullanıcının bildirim numarası denenerek talebine atlanamaz.
    [HttpGet]
    public async Task<IActionResult> Open(int id)
    {
        var me = _current.User!;

        var n = await _notify.GetOwnAsync(me.Id, id);
        if (n == null) return NotFound();

        await _notify.MarkReadAsync(me.Id, id);

        if (n.TicketId == null) return RedirectToAction(nameof(Index));

        // Talep arşivlendiyse (soft-delete) sorgulara düşmez: Ticket boş gelir.
        // Kullanıcıyı 404'e göndermek yerine nedenini söylüyoruz.
        if (n.Ticket == null)
        {
            TempData["Err"] = "Bildirimin ilgili olduğu talep arşivlendiği için açılamıyor.";
            return RedirectToAction(nameof(Index));
        }

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
