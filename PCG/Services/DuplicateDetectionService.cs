using Microsoft.EntityFrameworkCore;
using PCG.Data;
using PCG.Models;

namespace PCG.Services;

public class DuplicateDetectionService : IDuplicateDetectionService
{
    private readonly ApplicationDbContext _db;

    public DuplicateDetectionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DuplicateCheckResult> CheckAsync(
        DocumentType type,
        string? invoiceNumber,
        string? vendor,
        decimal? amount,
        string fileSha256,
        int? excludeDocumentId,
        CancellationToken ct = default)
    {
        var q = _db.Documents.AsNoTracking().Where(d => d.Status != DocumentStatus.Rejected);
        if (excludeDocumentId.HasValue)
            q = q.Where(d => d.Id != excludeDocumentId.Value);

        // Same file bytes
        var sameFile = await q.FirstOrDefaultAsync(d => d.FileSha256 == fileSha256, ct);
        if (sameFile != null)
        {
            return new DuplicateCheckResult
            {
                Flag = DuplicateFlag.IdenticalFile,
                MatchingDocumentId = sameFile.Id,
                BlocksUpload = true,
                Message = "An identical file was already uploaded."
            };
        }

        var invNorm = NormalizeInvoiceNumber(invoiceNumber);
        var vendorNorm = NormalizeVendor(vendor);

        var invoiceDup = invNorm != null
            ? await q.FirstOrDefaultAsync(d => d.Type == type && d.InvoiceNumber != null && d.InvoiceNumber.ToLower() == invNorm, ct)
            : null;

        Document? vendorAmountDup = null;
        if (vendorNorm != null && amount.HasValue)
        {
            var amt = decimal.Round(amount.Value, 2);
            // Load candidates with same amount into memory, then normalize vendor for comparison
            var candidates = await q
                .Where(d => d.Vendor != null && d.Amount.HasValue && decimal.Round(d.Amount.Value, 2) == amt)
                .ToListAsync(ct);
            vendorAmountDup = candidates
                .FirstOrDefault(d => NormalizeVendor(d.Vendor!) == vendorNorm);
        }

        if (invoiceDup != null && vendorAmountDup != null && invoiceDup.Id == vendorAmountDup.Id)
        {
            return new DuplicateCheckResult
            {
                Flag = DuplicateFlag.Both,
                MatchingDocumentId = invoiceDup.Id,
                BlocksUpload = true,
                Message = "Duplicate invoice number and matching vendor/amount found."
            };
        }

        if (invoiceDup != null)
        {
            return new DuplicateCheckResult
            {
                Flag = DuplicateFlag.InvoiceNumberMatch,
                MatchingDocumentId = invoiceDup.Id,
                BlocksUpload = true,
                Message = "This invoice number already exists for the same document type."
            };
        }

        if (vendorAmountDup != null)
        {
            return new DuplicateCheckResult
            {
                Flag = DuplicateFlag.VendorAmountSuspected,
                MatchingDocumentId = vendorAmountDup.Id,
                BlocksUpload = false,
                Message = "Another document exists with the same vendor and amount — please confirm this is not a duplicate."
            };
        }

        return new DuplicateCheckResult { Flag = DuplicateFlag.None, BlocksUpload = false, Message = "No duplicates detected." };
    }

    private static string? NormalizeInvoiceNumber(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return s.Trim().ToLowerInvariant();
    }

    private static string? NormalizeVendor(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return System.Text.RegularExpressions.Regex.Replace(s.Trim().ToLowerInvariant(), @"\s+", " ");
    }
}
