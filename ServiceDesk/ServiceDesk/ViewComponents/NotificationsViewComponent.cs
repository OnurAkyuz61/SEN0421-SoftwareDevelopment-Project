using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Data;
using ServiceDesk.Services;

namespace ServiceDesk.ViewComponents;

// Üst bardaki bildirim zili (her sayfada görünür)
public class NotificationsViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    public NotificationsViewComponent(AppDbContext db) => _db = db;

    public async Task<IViewComponentResult> InvokeAsync() =>
        View(await NotificationService.GetAsync(_db));
}
