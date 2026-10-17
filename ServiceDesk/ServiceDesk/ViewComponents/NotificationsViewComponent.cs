using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Services;

namespace ServiceDesk.ViewComponents;

// Üst bardaki bildirim zili. Artık hesaplanmış liste değil,
// oturumdaki kullanıcının okunmamış bildirimlerini gösterir.
public class NotificationsViewComponent : ViewComponent
{
    private readonly NotificationService _notify;
    private readonly CurrentUser _current;

    public NotificationsViewComponent(NotificationService notify, CurrentUser current)
    {
        _notify = notify;
        _current = current;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = _current.User;
        if (user == null) return View(new List<ServiceDesk.Models.Notification>());

        return View(await _notify.GetForAsync(user.Id, unreadOnly: true, take: 10));
    }
}
