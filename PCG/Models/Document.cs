using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PCG.Models;

public class Document
{
    public int Id { get; set; }

    [Range(0, 1, ErrorMessage = "Only Invoice and Credit Note document types are allowed.")]
    public DocumentType Type { get; set; }

    [Required, MaxLength(512)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required, MaxLength(512)]
    public string StoredFileName { get; set; } = string.Empty;

    [MaxLength(128)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    [MaxLength(64)]
    public string FileSha256 { get; set; } = string.Empty;

    public string? UploadedById { get; set; }
    public ApplicationUser? UploadedBy { get; set; }

    public DateTime UploadedAtUtc { get; set; }

    [MaxLength(128)]
    public string? InvoiceNumber { get; set; }

    [MaxLength(256)]
    public string? Vendor { get; set; }

    public DateTime? DocumentDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? VatAmount { get; set; }

    public string? ExtractionRawJson { get; set; }
    public bool ExtractionUsedOpenAI { get; set; }

    public DocumentStatus Status { get; set; }

    [MaxLength(2000)]
    public string? RejectionReason { get; set; }

    public DuplicateFlag DuplicateFlag { get; set; }

    public int? DuplicateOfDocumentId { get; set; }
    public Document? DuplicateOfDocument { get; set; }

    /// <summary>User acknowledged vendor+amount suspected duplicate and uploaded anyway.</summary>
    public bool VendorAmountOverrideAcknowledged { get; set; }

    public ICollection<ApprovalHistoryEntry> ApprovalHistory { get; set; } = new List<ApprovalHistoryEntry>();
}
