using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServiceDesk.Filters;
using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Controllers;

// DESTEK PERSONELİ İŞ YÜKÜ API'si.
//
//   Sıralama  GET  api/agents/workload?teamId=
//
// Açık talep sayısına göre en az yüklüden en çoğa. Atama ekranı / otomasyon
// için; son kullanıcı çağırmaz.
//
// Sözleşme: Models/ApiContracts.cs — { items: [...] }, hata { error }.
[ApiController]
[Route("api/agents")]
[ApiAuthorizeRole(UserRole.Agent, UserRole.Admin)]
[EnableRateLimiting(RateLimitPolicies.Search)]
public class AgentsApiController : ControllerBase
{
    private readonly AgentWorkloadRanking _workload;

    public AgentsApiController(AgentWorkloadRanking workload) => _workload = workload;

    public record AgentWorkloadDto(
        int Rank,
        int UserId,
        string FullName,
        string Email,
        UserRole Role,
        string RoleText,
        int? TeamId,
        string? TeamName,
        int OpenTickets);

    [HttpGet("workload")]
    public async Task<IActionResult> Workload(int? teamId = null)
    {
        var ranked = await _workload.ListAsync(teamId);
        return Ok(ApiItems<AgentWorkloadDto>.From(
            ranked.Select(r => new AgentWorkloadDto(
                r.Rank, r.UserId, r.FullName, r.Email,
                r.Role, Ui.RoleText(r.Role),
                r.TeamId, r.TeamName, r.OpenTickets)).ToList()));
    }
}
