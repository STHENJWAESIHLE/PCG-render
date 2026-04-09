using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCG.Authorization;
using PCG.Models.Reports;
using PCG.Services;

namespace PCG.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private IActionResult BlockExternalUser()
    {
        if (User.IsExternalUser())
            return RedirectToAction("Index", "Home");
        return null!;
    }
    private readonly IReportQueryService _reports;
    private readonly IReportExportService _export;

    public ReportsController(IReportQueryService reports, IReportExportService export)
    {
        _reports = reports;
        _export = export;
    }

    public async Task<IActionResult> Index(ReportFilterModel filter, CancellationToken ct)
    {
        var block = BlockExternalUser();
        if (block != null) return block;

        var (rows, spend, vendors, tax) = await _reports.BuildReportAsync(filter, ct);
        ViewBag.Filter = filter;
        ViewBag.Spend = spend;
        ViewBag.Vendors = vendors;
        ViewBag.Tax = tax;
        return View(rows);
    }

    public async Task<IActionResult> ExportExcel(ReportFilterModel filter, CancellationToken ct)
    {
        var block = BlockExternalUser();
        if (block != null) return block;

        var (rows, spend, vendors, tax) = await _reports.BuildReportAsync(filter, ct);
        var bytes = _export.ToExcel(filter, rows, spend, vendors, tax);
        var name = $"pcg-report-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    public async Task<IActionResult> ExportPdf(ReportFilterModel filter, CancellationToken ct)
    {
        var block = BlockExternalUser();
        if (block != null) return block;

        var (rows, spend, vendors, tax) = await _reports.BuildReportAsync(filter, ct);
        var bytes = _export.ToPdf(filter, rows, spend, vendors, tax);
        var name = $"pcg-report-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
        return File(bytes, "application/pdf", name);
    }
}
