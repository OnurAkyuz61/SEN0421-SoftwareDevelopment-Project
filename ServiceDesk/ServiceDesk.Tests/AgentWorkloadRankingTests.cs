using ServiceDesk.Models;
using ServiceDesk.Services;

namespace ServiceDesk.Tests;

public class AgentWorkloadRankingTests
{
    [Fact]
    public void Rank_az_yukluden_coga_siralar()
    {
        var rows = new[]
        {
            new AgentWorkloadRankingPolicy.Row(2, "Zeynep", "z@x", UserRole.Agent, 1, "A", 5),
            new AgentWorkloadRankingPolicy.Row(1, "Ali", "a@x", UserRole.Agent, 1, "A", 1),
            new AgentWorkloadRankingPolicy.Row(3, "Can", "c@x", UserRole.Admin, null, null, 1),
        };

        var ranked = AgentWorkloadRankingPolicy.Rank(rows);

        Assert.Equal(3, ranked.Count);
        Assert.Equal(1, ranked[0].Rank);
        Assert.Equal(1, ranked[0].UserId); // Ali (1 açık, ada göre Can'dan önce)
        Assert.Equal(2, ranked[1].Rank);
        Assert.Equal(3, ranked[1].UserId); // Can
        Assert.Equal(3, ranked[2].Rank);
        Assert.Equal(2, ranked[2].UserId); // Zeynep
        Assert.Equal(5, ranked[2].OpenTickets);
    }

    [Fact]
    public void Rank_bos_liste_bos_doner()
    {
        Assert.Empty(AgentWorkloadRankingPolicy.Rank(Array.Empty<AgentWorkloadRankingPolicy.Row>()));
    }
}
