using System.ComponentModel.DataAnnotations;
using PCG.Models;

namespace PCG.Models.Reports;

public class ReportFilterModel
{
    [DataType(DataType.Date)]
    public DateTime? DateFrom { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DateTo { get; set; }

    public string? Vendor { get; set; }

    /// <summary>Filter: null = all, or Pending / Approved / Rejected aggregate.</summary>
    public string? ApprovalStatusFilter { get; set; }

    public decimal? AmountMin { get; set; }
    public decimal? AmountMax { get; set; }

    /// <summary>SpendSummary, VendorAnalysis, TaxVat — drives dashboard sections.</summary>
    public string ReportView { get; set; } = "SpendSummary";
}
