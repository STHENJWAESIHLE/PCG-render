using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCG.Authorization;
using PCG.Models.Reports;
using PCG.Services;

namespace PCG.Controllers;

[Authorize]
public class InsightsController : Controller
{
    private readonly IInsightsService _insights;

    public InsightsController(IInsightsService insights)
    {
        _insights = insights;
    }

    public async Task<IActionResult> Index(ReportFilterModel filter, CancellationToken ct)
    {
        if (User.IsExternalUser())
            return RedirectToAction("Index", "Home");

        var vm = await _insights.BuildInsightsAsync(filter, ct);
        ViewBag.Filter = filter;
        return View(vm);
    }
}
