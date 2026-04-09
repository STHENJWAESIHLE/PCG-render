using PCG.Models;

namespace PCG.Models.Extraction;

public class InvoiceExtractionResult
{
    public string? Vendor { get; set; }
    public DateTime? DocumentDate { get; set; }
    public decimal? Amount { get; set; }
    public decimal? VatAmount { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? RawNotes { get; set; }
    public bool UsedOpenAI { get; set; }
    /// <summary>AI-detected document type (Invoice or CreditNote) from content analysis.</summary>
    public DocumentType? DetectedDocumentType { get; set; }
}
