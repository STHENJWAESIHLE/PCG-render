using PCG.Models.Extraction;

namespace PCG.Services;

public interface IInvoiceExtractionService
{
    Task<InvoiceExtractionResult> ExtractAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
}
