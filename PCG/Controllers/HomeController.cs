using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PCG.Data;
using PCG.Models;
using PCG.Models.ViewModels;

namespace PCG.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _db;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var stats = new HomeStatsViewModel
                {
                    TotalDocuments = await _db.Documents.CountAsync(),
                    PendingReviewer = await _db.Documents.CountAsync(d => d.Status == DocumentStatus.PendingReviewer),
                    PendingManager = await _db.Documents.CountAsync(d => d.Status == DocumentStatus.PendingManager),
                    PendingAdmin = await _db.Documents.CountAsync(d => d.Status == DocumentStatus.PendingAdmin),
                    Approved = await _db.Documents.CountAsync(d => d.Status == DocumentStatus.Approved),
                    Rejected = await _db.Documents.CountAsync(d => d.Status == DocumentStatus.Rejected),
                    TotalAmount = await _db.Documents.Where(d => d.Status == DocumentStatus.Approved && d.Amount != null).SumAsync(d => d.Amount ?? 0)
                };
                return View(stats);
            }
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
