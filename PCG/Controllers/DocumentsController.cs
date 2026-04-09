using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PCG.Authorization;
using PCG.Data;
using PCG.Models;
using PCG.Models.Extraction;
using PCG.Models.ViewModels;
using PCG.Services;

namespace PCG.Controllers;

[Authorize]
public class DocumentsController : Controller
{
    private static readonly string[] AllowedExtensions = [".pdf", ".png", ".jpg", ".jpeg"];

    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IInvoiceExtractionService _extraction;
    private readonly IDuplicateDetectionService _duplicates;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        ApplicationDbContext db,
        IWebHostEnvironment env,
        IInvoiceExtractionService extraction,
        IDuplicateDetectionService duplicates,
        UserManager<ApplicationUser> users,
        ILogger<DocumentsController> logger)
    {
        _db = db;
        _env = env;
        _extraction = extraction;
        _duplicates = duplicates;
        _users = users;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var query = _db.Documents.AsNoTracking().Include(d => d.UploadedBy).AsQueryable();

        // External users only see their own documents
        if (User.IsExternalUser())
        {
            var userId = _users.GetUserId(User);
            query = query.Where(d => d.UploadedById == userId);
        }

        var list = await query
            .OrderByDescending(d => d.UploadedAtUtc)
            .Take(500)
            .ToListAsync(ct);
        return View(list);
    }

    [HttpGet]
    public IActionResult Upload()
    {
        if (!User.CanUpload())
            return Forbid();
        return View(new DocumentUploadVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(DocumentUploadVm model, CancellationToken ct)
    {
        if (!User.CanUpload())
            return Forbid();

        if (model.File == null || model.File.Length == 0)
        {
            ModelState.AddModelError(nameof(model.File), "Select a file.");
            return View(model);
        }

        var ext = Path.GetExtension(model.File.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            ModelState.AddModelError(nameof(model.File), "Only PDF and image files (invoices / credit notes) are allowed.");
            return View(model);
        }

        // Only Invoice and Credit Note are allowed
        if (model.Type != DocumentType.Invoice && model.Type != DocumentType.CreditNote)
        {
            ModelState.AddModelError(nameof(model.Type), "Only Invoice and Credit Note document types are allowed.");
            return View(model);
        }

        await using var readStream = model.File.OpenReadStream();
        await using var ms = new MemoryStream();
        await readStream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes));

        var dup = await _duplicates.CheckAsync(model.Type, null, null, null, hash, null, ct);
        if (dup.BlocksUpload && dup.Flag == DuplicateFlag.IdenticalFile)
        {
            ModelState.AddModelError(string.Empty, dup.Message);
            return View(model);
        }

        await using var extractStream = new MemoryStream(bytes);
        var extracted = await _extraction.ExtractAsync(extractStream, model.File.FileName, model.File.ContentType ?? "application/octet-stream", ct);

        // Validate that selected document type matches detected type
        if (extracted.DetectedDocumentType.HasValue && extracted.DetectedDocumentType.Value != model.Type)
        {
            var detectedName = extracted.DetectedDocumentType.Value == DocumentType.Invoice ? "Invoice" : "Credit Note";
            var selectedName = model.Type == DocumentType.Invoice ? "Invoice" : "Credit Note";
            ModelState.AddModelError(nameof(model.Type), $"Document type mismatch: You selected '{selectedName}' but the system detected a '{detectedName}'. Please select the correct document type and try again.");
            return View(model);
        }

        var inv = extracted.InvoiceNumber;
        var vendor = extracted.Vendor;
        var amt = extracted.Amount;

        dup = await _duplicates.CheckAsync(model.Type, inv, vendor, amt, hash, null, ct);
        if (dup.BlocksUpload)
        {
            ModelState.AddModelError(string.Empty, dup.Message);
            return View(model);
        }

        if (dup.Flag == DuplicateFlag.VendorAmountSuspected && !model.ConfirmVendorAmountDuplicate)
        {
            var tempName = $"{Guid.NewGuid():N}{ext}";
            var tempRoot = Path.Combine(_env.ContentRootPath, "App_Data", "upload_temp");
            Directory.CreateDirectory(tempRoot);
            var tempPath = Path.Combine(tempRoot, tempName);
            await System.IO.File.WriteAllBytesAsync(tempPath, bytes, ct);

            ViewData["DupMessage"] = dup.Message;
            return View("UploadConfirm", new UploadConfirmVm
            {
                TempFileName = tempName,
                Type = model.Type,
                OriginalFileName = model.File.FileName,
                ContentType = model.File.ContentType ?? "application/octet-stream",
                FileSizeBytes = bytes.LongLength,
                FileSha256 = hash,
                ExtractionJson = JsonSerializer.Serialize(extracted, new JsonSerializerOptions { WriteIndented = false }),
                ConfirmVendorAmountDuplicate = false
            });
        }

        var userId = _users.GetUserId(User);
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var root = Path.Combine(_env.ContentRootPath, "App_Data", "uploads");
        Directory.CreateDirectory(root);
        var fullPath = Path.Combine(root, storedName);
        await System.IO.File.WriteAllBytesAsync(fullPath, bytes, ct);

        var doc = new Document
        {
            Type = model.Type,
            OriginalFileName = model.File.FileName,
            StoredFileName = storedName,
            ContentType = model.File.ContentType ?? "application/octet-stream",
            FileSizeBytes = bytes.LongLength,
            FileSha256 = hash,
            UploadedById = userId,
            UploadedAtUtc = DateTime.UtcNow,
            InvoiceNumber = extracted.InvoiceNumber,
            Vendor = extracted.Vendor,
            DocumentDate = extracted.DocumentDate,
            Amount = extracted.Amount,
            VatAmount = extracted.VatAmount,
            ExtractionRawJson = JsonSerializer.Serialize(extracted),
            ExtractionUsedOpenAI = extracted.UsedOpenAI,
            Status = DocumentStatus.PendingReviewer,
            DuplicateFlag = dup.Flag == DuplicateFlag.VendorAmountSuspected ? DuplicateFlag.VendorAmountSuspected : DuplicateFlag.None,
            DuplicateOfDocumentId = dup.MatchingDocumentId,
            VendorAmountOverrideAcknowledged = dup.Flag == DuplicateFlag.VendorAmountSuspected && model.ConfirmVendorAmountDuplicate
        };

        _db.Documents.Add(doc);
        await _db.SaveChangesAsync(ct);
        TempData["Message"] = "Document uploaded and routed to Reviewer (step 1 of 3).";
        return RedirectToAction(nameof(Details), new { id = doc.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadConfirm(UploadConfirmVm model, CancellationToken ct)
    {
        if (!User.CanUpload())
            return Forbid();
        if (!model.ConfirmVendorAmountDuplicate)
        {
            ModelState.AddModelError(nameof(model.ConfirmVendorAmountDuplicate), "Confirm that you have reviewed the possible duplicate.");
            return View(model);
        }

        var tempRoot = Path.Combine(_env.ContentRootPath, "App_Data", "upload_temp");
        var tempPath = Path.Combine(tempRoot, model.TempFileName);
        if (!System.IO.File.Exists(tempPath))
        {
            ModelState.AddModelError(string.Empty, "Upload session expired. Please upload again.");
            return View(model);
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(tempPath, ct);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (!string.Equals(hash, model.FileSha256, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "File integrity check failed.");
            TryDelete(tempPath);
            return View(model);
        }

        InvoiceExtractionResult? extracted = null;
        try
        {
            extracted = JsonSerializer.Deserialize<InvoiceExtractionResult>(model.ExtractionJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { /* use nulls */ }

        var dup = await _duplicates.CheckAsync(model.Type, extracted?.InvoiceNumber, extracted?.Vendor, extracted?.Amount, hash, null, ct);
        if (dup.BlocksUpload)
        {
            ModelState.AddModelError(string.Empty, dup.Message);
            TryDelete(tempPath);
            return View(model);
        }

        var ext = Path.GetExtension(model.OriginalFileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            ext = ".pdf";

        var storedName = $"{Guid.NewGuid():N}{ext}";
        var root = Path.Combine(_env.ContentRootPath, "App_Data", "uploads");
        Directory.CreateDirectory(root);
        var finalPath = Path.Combine(root, storedName);
        System.IO.File.Move(tempPath, finalPath);

        var userId = _users.GetUserId(User);
        var doc = new Document
        {
            Type = model.Type,
            OriginalFileName = model.OriginalFileName,
            StoredFileName = storedName,
            ContentType = model.ContentType,
            FileSizeBytes = model.FileSizeBytes,
            FileSha256 = hash,
            UploadedById = userId,
            UploadedAtUtc = DateTime.UtcNow,
            InvoiceNumber = extracted?.InvoiceNumber,
            Vendor = extracted?.Vendor,
            DocumentDate = extracted?.DocumentDate,
            Amount = extracted?.Amount,
            VatAmount = extracted?.VatAmount,
            ExtractionRawJson = model.ExtractionJson,
            ExtractionUsedOpenAI = extracted?.UsedOpenAI ?? false,
            Status = DocumentStatus.PendingReviewer,
            DuplicateFlag = DuplicateFlag.VendorAmountSuspected,
            DuplicateOfDocumentId = dup.MatchingDocumentId,
            VendorAmountOverrideAcknowledged = true
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync(ct);
        TempData["Message"] = "Document saved with duplicate acknowledgment — routed to Reviewer (step 1 of 3).";
        return RedirectToAction(nameof(Details), new { id = doc.Id });
    }

    private static void TryDelete(string path)
    {
        try { System.IO.File.Delete(path); } catch { /* ignore */ }
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var d = await _db.Documents
            .Include(x => x.UploadedBy)
            .Include(x => x.ApprovalHistory).ThenInclude(h => h.Actor)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d == null) return NotFound();
        return View(d);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var d = await _db.Documents.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d == null) return NotFound();
        if (!CanEdit(User, d))
            return Forbid();
        return View(new DocumentEditVm
        {
            Id = d.Id,
            InvoiceNumber = d.InvoiceNumber,
            Vendor = d.Vendor,
            DocumentDate = d.DocumentDate,
            Amount = d.Amount,
            VatAmount = d.VatAmount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(DocumentEditVm model, CancellationToken ct)
    {
        var d = await _db.Documents.FirstOrDefaultAsync(x => x.Id == model.Id, ct);
        if (d == null) return NotFound();
        if (!CanEdit(User, d))
            return Forbid();

        var dup = await _duplicates.CheckAsync(d.Type, model.InvoiceNumber, model.Vendor, model.Amount, d.FileSha256, d.Id, ct);
        if (dup.BlocksUpload)
        {
            ModelState.AddModelError(string.Empty, dup.Message);
            return View(model);
        }

        d.InvoiceNumber = model.InvoiceNumber;
        d.Vendor = model.Vendor;
        d.DocumentDate = model.DocumentDate;
        d.Amount = model.Amount;
        d.VatAmount = model.VatAmount;
        if (dup.Flag == DuplicateFlag.VendorAmountSuspected)
        {
            d.DuplicateFlag = DuplicateFlag.VendorAmountSuspected;
            d.DuplicateOfDocumentId = dup.MatchingDocumentId;
        }
        else if (dup.Flag == DuplicateFlag.None)
        {
            d.DuplicateFlag = DuplicateFlag.None;
            d.DuplicateOfDocumentId = null;
        }

        await _db.SaveChangesAsync(ct);
        TempData["Message"] = "Document fields updated.";
        return RedirectToAction(nameof(Details), new { id = d.Id });
    }

    private bool CanEdit(ClaimsPrincipal user, Document d)
    {
        if (d.Status == DocumentStatus.Approved) return false;
        if (user.IsInRole(RoleNames.Admin)) return true;
        if (d.Status != DocumentStatus.PendingReviewer) return false;
        var uid = _users.GetUserId(user);
        return uid != null && uid == d.UploadedById;
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var d = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d == null) return NotFound();
        var path = Path.Combine(_env.ContentRootPath, "App_Data", "uploads", d.StoredFileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, d.ContentType, d.OriginalFileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? comment, CancellationToken ct)
    {
        var d = await _db.Documents.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d == null) return NotFound();
        var stage = AuthHelpers.ExpectedStage(d.Status);
        if (stage == null) return BadRequest();
        if (!User.CanActAtStage(stage.Value))
            return Forbid();

        var userId = _users.GetUserId(User);
        if (!User.IsInRole(RoleNames.Admin) && userId != null && d.UploadedById != null && userId == d.UploadedById)
        {
            TempData["Error"] = "Internal control: the uploader cannot approve their own document.";
            return RedirectToAction(nameof(Details), new { id });
        }
        d.ApprovalHistory.Add(new ApprovalHistoryEntry
        {
            Stage = stage.Value,
            Decision = ApprovalDecision.Approved,
            ActorUserId = userId,
            ActedAtUtc = DateTime.UtcNow,
            Comment = comment
        });
        d.Status = AuthHelpers.NextStatusAfterApproval(d.Status);
        d.RejectionReason = null;
        await _db.SaveChangesAsync(ct);
        TempData["Message"] = d.Status == DocumentStatus.Approved ? "Final approval complete — document approved." : "Approved — moved to next stage.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "Rejection reason is required.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var d = await _db.Documents.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d == null) return NotFound();
        var stage = AuthHelpers.ExpectedStage(d.Status);
        if (stage == null) return BadRequest();
        if (!User.CanActAtStage(stage.Value))
            return Forbid();

        var userId = _users.GetUserId(User);
        if (!User.IsInRole(RoleNames.Admin) && userId != null && d.UploadedById != null && userId == d.UploadedById)
        {
            TempData["Error"] = "Internal control: the uploader cannot reject their own document.";
            return RedirectToAction(nameof(Details), new { id });
        }
        d.ApprovalHistory.Add(new ApprovalHistoryEntry
        {
            Stage = stage.Value,
            Decision = ApprovalDecision.Rejected,
            ActorUserId = userId,
            ActedAtUtc = DateTime.UtcNow,
            Comment = reason
        });
        d.Status = DocumentStatus.Rejected;
        d.RejectionReason = reason.Trim();
        await _db.SaveChangesAsync(ct);
        TempData["Message"] = "Document rejected.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
