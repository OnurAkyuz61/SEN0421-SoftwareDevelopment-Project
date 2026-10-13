namespace ServiceDesk.Models;

// Arayüzde etiket renkleri (site.css .tag-*) ve Türkçe metinler
public static class Ui
{
    public static string PriorityText(Priority p) => p switch
    {
        Priority.Critical => "Kritik", Priority.High => "Yüksek", Priority.Medium => "Orta", _ => "Düşük"
    };

    public static string PriorityTag(Priority p) => p switch
    {
        Priority.Critical => "tag-red", Priority.High => "tag-amber", Priority.Medium => "tag-blue", _ => "tag-gray"
    };

    public static string StatusText(TicketStatus s) => s switch
    {
        TicketStatus.New => "Yeni", TicketStatus.InProgress => "İşlemde",
        TicketStatus.Resolved => "Çözüldü", _ => "Kapandı"
    };

    public static string StatusTag(TicketStatus s) => s switch
    {
        TicketStatus.New => "tag-blue", TicketStatus.InProgress => "tag-amber",
        TicketStatus.Resolved => "tag-green", _ => "tag-gray"
    };

    public static string TypeText(TicketType t) => t == TicketType.Incident ? "Arıza" : "Değişiklik";

    public static string CategoryText(TicketCategory c) => c switch
    {
        TicketCategory.Hardware => "Donanım", TicketCategory.Software => "Yazılım",
        TicketCategory.Network => "Ağ / İnternet", TicketCategory.Access => "Hesap / Erişim",
        TicketCategory.Email => "E-posta", _ => "Diğer"
    };

    public static string ApprovalText(ApprovalStatus a) => a switch
    {
        ApprovalStatus.Pending => "Onay bekliyor", ApprovalStatus.Approved => "Onaylandı",
        ApprovalStatus.Rejected => "Reddedildi", _ => "-"
    };

    public static string ApprovalTag(ApprovalStatus a) => a switch
    {
        ApprovalStatus.Pending => "tag-amber", ApprovalStatus.Approved => "tag-green",
        ApprovalStatus.Rejected => "tag-red", _ => "tag-gray"
    };

    public static string FileSize(long bytes) =>
        bytes < 1024 * 1024 ? $"{Math.Max(1, bytes / 1024.0):0.#} KB" : $"{bytes / 1024.0 / 1024.0:0.#} MB";
}
