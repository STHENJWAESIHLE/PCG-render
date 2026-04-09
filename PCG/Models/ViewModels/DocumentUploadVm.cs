using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PCG.Models.ViewModels;

public class DocumentUploadVm
{
    [Required]
    public PCG.Models.DocumentType Type { get; set; }

    [Required]
    public IFormFile? File { get; set; }

    /// <summary>Required when server detected vendor+amount suspected duplicate.</summary>
    public bool ConfirmVendorAmountDuplicate { get; set; }
}
