namespace ServiceDesk.Models;

// Arayüzde renk (badge) ve Türkçe metinler için küçük yardımcı
public static class Ui
{
    public static string PriorityText(Priority p) => p switch
    {
        Priority.Critical => "Kritik", Priority.High => "Yüksek", Priority.Medium => "Orta", _ => "Düşük"
    };

    public static string PriorityBadge(Priority p) => p switch
    {
        Priority.Critical => "bg-danger", Priority.High => "bg-warning text-dark",
        Priority.Medium => "bg-info text-dark", _ => "bg-secondary"
    };

    public static string StatusText(TicketStatus s) => s switch
    {
        TicketStatus.New => "Yeni", TicketStatus.InProgress => "İşlemde",
        TicketStatus.Resolved => "Çözüldü", _ => "Kapandı"
    };

    public static string StatusBadge(TicketStatus s) => s switch
    {
        TicketStatus.New => "bg-primary", TicketStatus.InProgress => "bg-warning text-dark",
        TicketStatus.Resolved => "bg-success", _ => "bg-dark"
    };

    public static string TypeText(TicketType t) => t == TicketType.Incident ? "Arıza" : "Değişiklik";
}
