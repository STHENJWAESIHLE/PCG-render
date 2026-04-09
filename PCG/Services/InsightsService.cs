using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PCG.Data;
using PCG.Models;
using PCG.Models.Reports;

namespace PCG.Services;

public class InsightsOptions
{
    public string? OpenAIApiKey { get; set; }
    public string OpenAIModel { get; set; } = "gpt-4o-mini";
}

public class InsightsService : IInsightsService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly InsightsOptions _options;
    private readonly ILogger<InsightsService> _logger;

    public InsightsService(
        ApplicationDbContext db,
        IHttpClientFactory httpClientFactory,
        IOptions<InsightsOptions> options,
        ILogger<InsightsService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    // Clean up vendor names that have table headers concatenated
    private static string CleanVendorName(string? vendor)
    {
        if (string.IsNullOrWhiteSpace(vendor)) return "Unknown";
        var v = vendor.Trim();
        // Stop at common invoice table headers
        var stopWords = new[] { "Currency", "Description", "Qty", "Quantity", "Unit", "Price", "Line Total",
                                "Invoice", "Date", "Due Date", "Tax", "VAT", "Subtotal", "Total", "Payment",
                                "Address", "Phone", "Email", "Reg", "Co.", "Pty", "Ltd", "Limited", "ZAR" };
        foreach (var word in stopWords)
        {
            var idx = v.IndexOf(word, StringComparison.OrdinalIgnoreCase);
            if (idx > 3)
                v = v[..idx].Trim();
        }
        // Handle camelCase (e.g., "KingdomCurrency" -> "Kingdom")
        var camelPattern = @"[a-z](?=[A-Z])";
        var camelMatches = System.Text.RegularExpressions.Regex.Matches(v, camelPattern);
        foreach (System.Text.RegularExpressions.Match m in camelMatches.Cast<System.Text.RegularExpressions.Match>().Reverse())
        {
            var rest = v[(m.Index + 1)..];
            foreach (var word in stopWords)
            {
                if (rest.StartsWith(word, StringComparison.OrdinalIgnoreCase) && m.Index > 3)
                {
                    v = v[..m.Index].Trim();
                    break;
                }
            }
        }
        // Remove common suffixes
        v = System.Text.RegularExpressions.Regex.Replace(v, @"(?i)(document|supplies?|products?).*$", "").Trim();
        v = System.Text.RegularExpressions.Regex.Replace(v, @"\s+", " ");
        v = v.TrimEnd(',', '.', ':', ';', '-', ' ', '/');
        return v.Length >= 2 ? v : "Unknown";
    }

    public async Task<InsightsViewModel> BuildInsightsAsync(ReportFilterModel filter, CancellationToken ct = default)
    {
        var q = _db.Documents.AsNoTracking().Where(d => d.Status != DocumentStatus.Rejected);

        if (filter.DateFrom.HasValue)
        {
            var f = filter.DateFrom.Value.Date;
            q = q.Where(d => (d.DocumentDate ?? d.UploadedAtUtc.Date) >= f);
        }
        if (filter.DateTo.HasValue)
        {
            var t = filter.DateTo.Value.Date;
            q = q.Where(d => (d.DocumentDate ?? d.UploadedAtUtc.Date) <= t);
        }
        if (!string.IsNullOrWhiteSpace(filter.Vendor))
        {
            var v = filter.Vendor.Trim();
            q = q.Where(d => d.Vendor != null && d.Vendor.Contains(v));
        }

        var docs = await q.ToListAsync(ct);

        var vm = new InsightsViewModel();

        var byMonth = docs
            .Where(d => d.Amount.HasValue)
            .Select(d => new
            {
                Month = (d.DocumentDate ?? d.UploadedAtUtc).ToString("yyyy-MM"),
                Signed = d.Type == DocumentType.CreditNote ? -d.Amount!.Value : d.Amount!.Value
            })
            .GroupBy(x => x.Month)
            .OrderBy(g => g.Key)
            .Select(g => new InsightTrendPoint
            {
                Period = g.Key,
                Amount = g.Sum(x => x.Signed),
                Count = g.Count()
            })
            .ToList();
        vm.MonthlyTrends = byMonth;

        if (byMonth.Count >= 2)
        {
            var last = byMonth[^1].Amount;
            var prev = byMonth[^2].Amount;
            if (prev != 0 && Math.Abs((last - prev) / prev) > 0.25m)
            {
                vm.Anomalies.Add(new InsightAnomaly
                {
                    Description = $"Large month-on-month change in {byMonth[^1].Period}: {last:N2} vs prior {prev:N2}.",
                    Severity = "warning"
                });
            }
        }

        var vendorGroups = docs.Where(d => d.Amount.HasValue && !string.IsNullOrWhiteSpace(d.Vendor))
            .GroupBy(d => d.Vendor!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Vendor = g.Key, Total = g.Sum(x => x.Type == DocumentType.CreditNote ? -x.Amount!.Value : x.Amount!.Value) })
            .ToList();
        var totalAbs = vendorGroups.Sum(x => Math.Abs(x.Total));
        if (totalAbs > 0 && vendorGroups.Count > 0)
        {
            var top = vendorGroups.OrderByDescending(x => Math.Abs(x.Total)).First();
            var pct = Math.Abs(top.Total) / totalAbs * 100;
            if (pct > 45)
            {
                var cleanVendor = CleanVendorName(top.Vendor);
                vm.Anomalies.Add(new InsightAnomaly
                {
                    Description = $"Concentration risk: vendor \"{cleanVendor}\" represents about {pct:F0}% of filtered document value.",
                    Severity = "info"
                });
            }
        }

        var amounts = docs.Where(d => d.Amount.HasValue).Select(d => d.Type == DocumentType.CreditNote ? -d.Amount!.Value : d.Amount!.Value).ToList();
        if (amounts.Count >= 5)
        {
            var vals = amounts.Select(a => (double)a).ToList();
            var meanD = vals.Average();
            var variance = vals.Sum(a => (a - meanD) * (a - meanD)) / vals.Count;
            var std = (decimal)Math.Sqrt(variance);
            var mean = (decimal)meanD;
            foreach (var d in docs.Where(x => x.Amount.HasValue))
            {
                var a = d.Type == DocumentType.CreditNote ? -d.Amount!.Value : d.Amount!.Value;
                if (std > 0 && Math.Abs(a - mean) > 2.5m * std)
                {
                    var cleanVendor = CleanVendorName(d.Vendor);
                    vm.Anomalies.Add(new InsightAnomaly
                    {
                        Description = $"Statistical outlier: {cleanVendor} — {a:N2} (mean {mean:N2}).",
                        Severity = "warning"
                    });
                    break;
                }
            }
        }

        vm.SpendingBullets.Add($"Documents in scope: {docs.Count}");
        vm.SpendingBullets.Add($"Approved: {docs.Count(d => d.Status == DocumentStatus.Approved)} | Pending: {docs.Count(d => d.Status is DocumentStatus.PendingReviewer or DocumentStatus.PendingManager or DocumentStatus.PendingAdmin)}");
        var net = docs.Where(d => d.Amount.HasValue).Sum(d => d.Type == DocumentType.CreditNote ? -d.Amount!.Value : d.Amount!.Value);
        vm.SpendingBullets.Add($"Net amount (invoices − credit notes): {net:N2}");
        var vat = docs.Where(d => d.VatAmount.HasValue).Sum(d => d.VatAmount!.Value);
        vm.SpendingBullets.Add($"Sum of VAT lines: {vat:N2}");

        if (!string.IsNullOrWhiteSpace(_options.OpenAIApiKey))
        {
            try
            {
                vm.AiNarrative = await CallOpenAiNarrativeAsync(vm, net, vat, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenAI insights narrative failed.");
            }
        }

        return vm;
    }

    private async Task<string?> CallOpenAiNarrativeAsync(InsightsViewModel vm, decimal net, decimal vat, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Trends by month:");
        foreach (var t in vm.MonthlyTrends.TakeLast(6))
            sb.AppendLine($"{t.Period}: {t.Amount:N2} ({t.Count} docs)");
        sb.AppendLine($"Net spend: {net:N2}, VAT sum: {vat:N2}");
        sb.AppendLine("Flags:");
        foreach (var a in vm.Anomalies)
            sb.AppendLine($"- {a.Description}");

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.OpenAIApiKey);
        client.BaseAddress = new Uri("https://api.openai.com/");
        client.Timeout = TimeSpan.FromMinutes(1);

        var body = new
        {
            model = _options.OpenAIModel,
            messages = new object[]
            {
                new { role = "system", content = "You are a finance analyst. Write 3-5 short bullet insights (trends, anomalies, savings opportunities) for approvers. Plain text, no markdown." },
                new { role = "user", content = sb.ToString() }
            },
            temperature = 0.4
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        using var resp = await client.SendAsync(req, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
    }
}
