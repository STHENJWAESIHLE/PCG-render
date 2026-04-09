using System.ComponentModel.DataAnnotations;

namespace PCG.Models.ViewModels;

public class UploadConfirmVm
{
    [Required]
    public string TempFileName { get; set; } = string.Empty;

    [Required]
    [Range(0, 1, ErrorMessage = "Only Invoice and Credit Note document types are allowed.")]
    public PCG.Models.DocumentType Type { get; set; }

    [Required]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    [Required]
    public string FileSha256 { get; set; } = string.Empty;

    [Required]
    public string ExtractionJson { get; set; } = string.Empty;

    public bool ConfirmVendorAmountDuplicate { get; set; }
}
