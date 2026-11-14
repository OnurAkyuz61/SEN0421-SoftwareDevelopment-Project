using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// RAPOR JSON UÇLARI (Destek / Yönetici).
//
//   Birim SLA uyumu  GET  api/reports/sla-by-department?from=&to=
//
// Dönemde açılan talepler, talep sahibinin birimine göre gruplanır.
// Sözleşme: ApiContracts — { items, from, to }, hata { error }.
[ApiController]
[Route("api/reports")]
[ApiAuthorizeRole(UserRole.Agent, UserRole.Admin)]
[EnableRateLimiting(RateLimitPolicies.Search)]
public class ReportsApiController : ControllerBase
{
    private readonly DepartmentSlaCompliance _sla;

    public ReportsApiController(DepartmentSlaCompliance sla) => _sla = sla;

    public record DepartmentSlaDto(
        int? DepartmentId,
        string DepartmentName,
        int TotalTickets,
        int BreachedTickets,
        decimal SlaComplianceRate,
        int FirstResponseMeasured,
        int FirstResponseBreached,
        decimal FirstResponseComplianceRate);

    public record DepartmentSlaReportDto(
        DateTime From,
        DateTime To,
        IReadOnlyList<DepartmentSlaDto> Items);

    [HttpGet("sla-by-department")]
    public async Task<IActionResult> SlaByDepartment(DateTime? from = null, DateTime? to = null)
    {
        var (fromLocal, toLocal, rows) = await _sla.ReportAsync(from, to);
        return Ok(new DepartmentSlaReportDto(
            fromLocal,
            toLocal,
            rows.Select(r => new DepartmentSlaDto(
                r.DepartmentId,
                r.DepartmentName,
                r.TotalTickets,
                r.BreachedTickets,
                r.SlaComplianceRate,
                r.FirstResponseMeasured,
                r.FirstResponseBreached,
                r.FirstResponseComplianceRate)).ToList()));
    }
}
