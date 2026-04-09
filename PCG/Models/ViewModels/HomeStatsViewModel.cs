namespace PCG.Models.ViewModels;

public class HomeStatsViewModel
{
    public int TotalDocuments { get; set; }
    public int PendingReviewer { get; set; }
    public int PendingManager { get; set; }
    public int PendingAdmin { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public decimal TotalAmount { get; set; }

    public int TotalPending => PendingReviewer + PendingManager + PendingAdmin;
}
