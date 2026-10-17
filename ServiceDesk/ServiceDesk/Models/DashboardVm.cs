namespace ServiceDesk.Models;

// Yönetim paneli (ISO 9001 KPI) için tek model
public class DashboardVm
{
    // KPI
    public int Total { get; set; }
    public int Open { get; set; }
    public int Unassigned { get; set; }
    public int PendingApproval { get; set; }
    public int Breached { get; set; }
    public double SlaRate { get; set; }
    public double Mttr { get; set; }
    public double Csat { get; set; }

    // Grafikler
    public string[] StatusLabels { get; set; } = Array.Empty<string>();
    public int[] StatusCounts { get; set; } = Array.Empty<int>();
    public string[] CategoryLabels { get; set; } = Array.Empty<string>();
    public int[] CategoryCounts { get; set; } = Array.Empty<int>();
    public string[] DayLabels { get; set; } = Array.Empty<string>();
    public int[] DayCreated { get; set; } = Array.Empty<int>();
    public int[] DayResolved { get; set; } = Array.Empty<int>();

    // Listeler
    public List<Ticket> Approvals { get; set; } = new();
    public List<Ticket> UnassignedList { get; set; } = new();
    public List<Ticket> BreachedList { get; set; } = new();
    public List<SurveyResponse> LatestSurveys { get; set; } = new();

    // ISO 9001 md. 9.1.1: dönemsel ölçüm karşılaştırması
    public List<KpiSnapshot> MonthlyKpi { get; set; } = new();
    public int OpenNonconformities { get; set; }
    public int OverdueNonconformities { get; set; }
}
