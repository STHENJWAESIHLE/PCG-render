using PCG.Models;
using PCG.Models.Reports;

namespace PCG.Services;

public class ReportRowVm
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? InvoiceNumber { get; set; }
    public string? Vendor { get; set; }
    public DateTime? DocumentDate { get; set; }
    public decimal? Amount { get; set; }
    public decimal? VatAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime UploadedAtUtc { get; set; }
}

public class SpendSummaryVm
{
    public decimal TotalInvoices { get; set; }
    public decimal TotalCreditNotes { get; set; }
    public decimal NetSpend { get; set; }
    public int DocumentCount { get; set; }
}

public class VendorAnalysisRow
{
    public string Vendor { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int Count { get; set; }
}

public class TaxVatSummaryVm
{
    public decimal TotalNet { get; set; }
    public decimal TotalVat { get; set; }
}

public interface IReportQueryService
{
    Task<(List<ReportRowVm> Rows, SpendSummaryVm Spend, List<VendorAnalysisRow> Vendors, TaxVatSummaryVm Tax)> BuildReportAsync(ReportFilterModel filter, CancellationToken ct = default);
}
