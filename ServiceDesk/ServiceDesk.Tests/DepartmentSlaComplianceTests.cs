using ServiceDesk.Services;

namespace ServiceDesk.Tests;

public class DepartmentSlaComplianceTests
{
    [Fact]
    public void ComplianceRate_bos_ve_dolu()
    {
        Assert.Equal(100m, DepartmentSlaCompliancePolicy.ComplianceRate(0, 0));
        Assert.Equal(80.0m, DepartmentSlaCompliancePolicy.ComplianceRate(10, 2));
    }

    [Fact]
    public void Summarize_birime_gore_gruplar()
    {
        var facts = new[]
        {
            new DepartmentSlaCompliancePolicy.TicketFact(1, false, true, false),
            new DepartmentSlaCompliancePolicy.TicketFact(1, true, true, true),
            new DepartmentSlaCompliancePolicy.TicketFact(null, false, false, false),
            new DepartmentSlaCompliancePolicy.TicketFact(2, false, true, false),
        };
        var names = new Dictionary<int, string> { [1] = "IT", [2] = "İK" };

        var rows = DepartmentSlaCompliancePolicy.Summarize(facts, names);

        Assert.Equal(3, rows.Count);
        var it = rows.Single(r => r.DepartmentId == 1);
        Assert.Equal("IT", it.DepartmentName);
        Assert.Equal(2, it.TotalTickets);
        Assert.Equal(1, it.BreachedTickets);
        Assert.Equal(50.0m, it.SlaComplianceRate);
        Assert.Equal(2, it.FirstResponseMeasured);
        Assert.Equal(1, it.FirstResponseBreached);
        Assert.Equal(50.0m, it.FirstResponseComplianceRate);

        var none = rows.Single(r => r.DepartmentId == null);
        Assert.Equal("Birimsiz", none.DepartmentName);
        Assert.Equal(100m, none.SlaComplianceRate);
    }
}
