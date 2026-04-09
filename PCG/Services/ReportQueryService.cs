using Microsoft.EntityFrameworkCore;
using PCG.Data;
using PCG.Models;
using PCG.Models.Reports;

namespace PCG.Services;

public class ReportQueryService : IReportQueryService
{
    private readonly ApplicationDbContext _db;

    public ReportQueryService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(List<ReportRowVm> Rows, SpendSummaryVm Spend, List<VendorAnalysisRow> Vendors, TaxVatSummaryVm Tax)> BuildReportAsync(ReportFilterModel filter, CancellationToken ct = default)
    {
        var q = _db.Documents.AsNoTracking().AsQueryable();

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

        if (filter.AmountMin.HasValue)
            q = q.Where(d => d.Amount >= filter.AmountMin.Value);
        if (filter.AmountMax.HasValue)
            q = q.Where(d => d.Amount <= filter.AmountMax.Value);

        if (!string.IsNullOrWhiteSpace(filter.ApprovalStatusFilter))
        {
            var s = filter.ApprovalStatusFilter.Trim();
            if (s.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                q = q.Where(d => d.Status == DocumentStatus.PendingReviewer || d.Status == DocumentStatus.PendingManager || d.Status == DocumentStatus.PendingAdmin);
            else if (s.Equals("Approved", StringComparison.OrdinalIgnoreCase))
                q = q.Where(d => d.Status == DocumentStatus.Approved);
            else if (s.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
                q = q.Where(d => d.Status == DocumentStatus.Rejected);
        }

        var list = await q.OrderByDescending(d => d.UploadedAtUtc).ToListAsync(ct);

        var rows = list.Select(d => new ReportRowVm
        {
            Id = d.Id,
            Type = d.Type.ToString(),
            InvoiceNumber = d.InvoiceNumber,
            Vendor = d.Vendor,
            DocumentDate = d.DocumentDate,
            Amount = d.Amount,
            VatAmount = d.VatAmount,
            Status = StatusLabel(d.Status),
            UploadedAtUtc = d.UploadedAtUtc
        }).ToList();

        var invSum = list.Where(d => d.Type == DocumentType.Invoice && d.Amount.HasValue).Sum(d => d.Amount!.Value);
        var cnSum = list.Where(d => d.Type == DocumentType.CreditNote && d.Amount.HasValue).Sum(d => d.Amount!.Value);

        var spend = new SpendSummaryVm
        {
            TotalInvoices = invSum,
            TotalCreditNotes = cnSum,
            NetSpend = invSum - cnSum,
            DocumentCount = list.Count
        };

        var vendors = list
            .Where(d => !string.IsNullOrWhiteSpace(d.Vendor) && d.Amount.HasValue)
            .GroupBy(d => d.Vendor!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new VendorAnalysisRow
            {
                Vendor = g.Key,
                TotalAmount = g.Sum(x => x.Type == DocumentType.CreditNote ? -x.Amount!.Value : x.Amount!.Value),
                Count = g.Count()
            })
            .OrderByDescending(x => Math.Abs(x.TotalAmount))
            .ToList();

        var tax = new TaxVatSummaryVm
        {
            TotalNet = list.Where(d => d.Amount.HasValue).Sum(d => d.Type == DocumentType.CreditNote ? -d.Amount!.Value : d.Amount!.Value),
            TotalVat = list.Where(d => d.VatAmount.HasValue).Sum(d => d.VatAmount!.Value)
        };

        return (rows, spend, vendors, tax);
    }

    private static string StatusLabel(DocumentStatus s) => s switch
    {
        DocumentStatus.PendingReviewer or DocumentStatus.PendingManager or DocumentStatus.PendingAdmin => "Pending",
        DocumentStatus.Approved => "Approved",
        DocumentStatus.Rejected => "Rejected",
        _ => s.ToString()
    };
}
