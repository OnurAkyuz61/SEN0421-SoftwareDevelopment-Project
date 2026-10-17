using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

[AuthorizeRole(UserRole.Agent, UserRole.Admin)]
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

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int? id, string? returnUrl)
    {
        await _notify.MarkReadAsync(_current.User!.Id, id);

        if (id == null) TempData["Ok"] = "Bildirimler okundu olarak işaretlendi.";
        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
    }
}
