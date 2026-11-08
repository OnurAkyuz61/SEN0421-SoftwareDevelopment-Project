using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ServiceDesk.Models;
using ServiceDesk.Services;
using ServiceDesk.Services.Notifications;

namespace ServiceDesk.ViewComponents;

// Üst bardaki bildirim zili. Sayaç önbellekten gelir (UnreadCountCache);
// liste yalnızca okunmamış varsa sorgulanır. Sayfa açık kaldıkça zil,
// /Notifications/UnreadCount ucunu yoklayarak sayacı günceller.
public class NotificationsViewComponent : ViewComponent
{
    private readonly NotificationService _notify;
    private readonly CurrentUser _current;
    private readonly NotificationSettings _cfg;

    public NotificationsViewComponent(NotificationService notify, CurrentUser current,
                                      IOptions<NotificationSettings> cfg)
    {
        _notify = notify;
        _current = current;
        _cfg = cfg.Value;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = _current.User;
        if (user == null) return View(new BellVm(0, new List<Notification>(), _cfg.UnreadPollSeconds));

        var count = await _notify.UnreadCountAsync(user.Id);
        var items = count == 0
            ? new List<Notification>()
            : await _notify.GetForAsync(user.Id, unreadOnly: true, take: 6);

        return View(new BellVm(count, items, _cfg.UnreadPollSeconds));
    }
}

public sealed record BellVm(int UnreadCount, List<Notification> Items, int PollSeconds);
