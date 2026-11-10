using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Data;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// KULLANICI PORTALI JSON UÇLARI. Portalın HTML tarafı PortalController'da;
// burası aynı veriyi istemci tarafı (liste yenileme, mobil) için döner.
//
//   Taleplerim  GET  api/portal/tickets?page=1&pageSize=20
//
// YETKİ KAPSAMI: PortalController ile birebir aynı kural geçerli —
//   1) Talep oturumdaki kullanıcıya mı ait (RequesterId == me.Id)
//   2) Talep arşivlenmiş mi (global query filter: IsDeleted)
// Başkasının talebi listeye hiç düşmez. Tüm talepleri görmek yönetim
// tarafının işidir (/Tickets).
//
// Entity değil DTO döner: iç notlar, SLA duraklama sayaçları, arşivleme
// nedeni gibi alanlar kullanıcıya açılmaz. Alan eklemek bilinçli bir karar
// olsun diye DTO elle yazıldı.
[ApiController]
[Route("api/portal")]
[ApiAuthorizeRole]
public class PortalApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CurrentUser _current;

    public PortalApiController(AppDbContext db, CurrentUser current)
    {
        _db = db;
        _current = current;
    }

    // Portal listesinin gösterdiği alanlar. Metin ve renkler Ui.cs'ten gelir;
    // ham enum değeri de gönderilir ki istemci filtreleyip sıralayabilsin.
    public record MyTicketDto(
        int Id, string Number, string Title,
        TicketType Type, string TypeText,
        Priority Priority, string PriorityText, string PriorityTag,
        TicketStatus Status, string StatusText, string StatusTag,
        string? CategoryName,
        ApprovalStatus ApprovalStatus, string ApprovalStatusText, string ApprovalStatusTag,
        bool IsOpen, bool IsSlaBreached, bool SurveyPending,
        DateTime CreatedAt, DateTime SlaDueAt, DateTime? ResolvedAt, DateTime? ClosedAt);

    [HttpGet("tickets")]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<IActionResult> MyTickets(int page = 1, int pageSize = Paging.DefaultPageSize)
    {
        var me = _current.User!;

        // Sıralama PortalController.Index ile aynı (en yeni üstte). CreatedAt'e
        // Id eklendi: aynı saniyede açılmış talepler sayfalar arasında yer
        // değiştirmesin, sıra her istekte aynı çıksın.
        var (tickets, pager) = await _db.Tickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Survey)
            .Where(t => t.RequesterId == me.Id)
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .ToPageAsync(page, pageSize);   // sayfa sınırlarını kendisi kırpar (1–100)

        return Ok(ApiPage<MyTicketDto>.From(tickets.Select(ToDto).ToList(), pager));
    }

    private static MyTicketDto ToDto(Ticket t) => new(
        t.Id, Ui.TicketNo(t), t.Title,
        t.Type, Ui.TypeText(t.Type),
        t.Priority, Ui.PriorityText(t.Priority), Ui.PriorityTag(t.Priority),
        t.Status, Ui.StatusText(t.Status), Ui.StatusTag(t.Status),
        t.Category?.Name,
        t.ApprovalStatus, Ui.ApprovalText(t.ApprovalStatus), Ui.ApprovalTag(t.ApprovalStatus),
        TicketWorkflow.IsOpen(t.Status),
        t.IsSlaBreached,
        // Portal listesindeki "anket bekliyor" işaretiyle aynı kural
        (t.Status is TicketStatus.Resolved or TicketStatus.Closed) && t.Survey == null,
        t.CreatedAt, t.SlaDueAt, t.ResolvedAt, t.ClosedAt);
}
