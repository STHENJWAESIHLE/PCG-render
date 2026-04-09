using PCG.Models;

namespace PCG.Services;

public class DuplicateCheckResult
{
    public DuplicateFlag Flag { get; set; }
    public int? MatchingDocumentId { get; set; }
    public bool BlocksUpload { get; set; }
    public string Message { get; set; } = string.Empty;
}

public interface IDuplicateDetectionService
{
    Task<DuplicateCheckResult> CheckAsync(
        DocumentType type,
        string? invoiceNumber,
        string? vendor,
        decimal? amount,
        string fileSha256,
        int? excludeDocumentId,
        CancellationToken ct = default);
}
