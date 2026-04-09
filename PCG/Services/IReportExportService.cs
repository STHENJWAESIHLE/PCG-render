using PCG.Models.Reports;

namespace PCG.Services;

public interface IReportExportService
{
    byte[] ToExcel(ReportFilterModel filter, List<ReportRowVm> rows, SpendSummaryVm spend, List<VendorAnalysisRow> vendors, TaxVatSummaryVm tax);
    byte[] ToPdf(ReportFilterModel filter, List<ReportRowVm> rows, SpendSummaryVm spend, List<VendorAnalysisRow> vendors, TaxVatSummaryVm tax);
}
