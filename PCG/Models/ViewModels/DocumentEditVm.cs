using System.ComponentModel.DataAnnotations;

namespace PCG.Models.ViewModels;

public class DocumentEditVm
{
    public int Id { get; set; }

    [MaxLength(128)]
    public string? InvoiceNumber { get; set; }

    [MaxLength(256)]
    public string? Vendor { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DocumentDate { get; set; }

    public decimal? Amount { get; set; }
    public decimal? VatAmount { get; set; }
}
