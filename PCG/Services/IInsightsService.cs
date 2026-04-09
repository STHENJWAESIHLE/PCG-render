using PCG.Models.Reports;

namespace PCG.Services;

public class InsightTrendPoint
{
    public string Period { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class InsightAnomaly
{
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
}

public class InsightsViewModel
{
    public List<InsightTrendPoint> MonthlyTrends { get; set; } = new();
    public List<InsightAnomaly> Anomalies { get; set; } = new();
    public List<string> SpendingBullets { get; set; } = new();
    public string? AiNarrative { get; set; }
}

public interface IInsightsService
{
    Task<InsightsViewModel> BuildInsightsAsync(ReportFilterModel filter, CancellationToken ct = default);
}
