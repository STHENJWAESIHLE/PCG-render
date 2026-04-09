using ClosedXML.Excel;
using PCG.Models.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PCG.Services;

public class ReportExportService : IReportExportService
{
    public ReportExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] ToExcel(ReportFilterModel filter, List<ReportRowVm> rows, SpendSummaryVm spend, List<VendorAnalysisRow> vendors, TaxVatSummaryVm tax)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Documents");
        ws.Cell(1, 1).Value = "Id";
        ws.Cell(1, 2).Value = "Type";
        ws.Cell(1, 3).Value = "Invoice #";
        ws.Cell(1, 4).Value = "Vendor";
        ws.Cell(1, 5).Value = "Document date";
        ws.Cell(1, 6).Value = "Amount";
        ws.Cell(1, 7).Value = "VAT";
        ws.Cell(1, 8).Value = "Status";
        ws.Cell(1, 9).Value = "Uploaded (UTC)";
        var r = 2;
        foreach (var row in rows)
        {
            ws.Cell(r, 1).Value = row.Id;
            ws.Cell(r, 2).Value = row.Type;
            ws.Cell(r, 3).Value = row.InvoiceNumber ?? "";
            ws.Cell(r, 4).Value = row.Vendor ?? "";
            ws.Cell(r, 5).Value = row.DocumentDate;
            ws.Cell(r, 6).Value = row.Amount;
            ws.Cell(r, 7).Value = row.VatAmount;
            ws.Cell(r, 8).Value = row.Status;
            ws.Cell(r, 9).Value = row.UploadedAtUtc;
            r++;
        }
        ws.Columns().AdjustToContents();

        var s = wb.AddWorksheet("Summary");
        s.Cell(1, 1).Value = "Total invoices";
        s.Cell(1, 2).Value = spend.TotalInvoices;
        s.Cell(2, 1).Value = "Total credit notes";
        s.Cell(2, 2).Value = spend.TotalCreditNotes;
        s.Cell(3, 1).Value = "Net spend";
        s.Cell(3, 2).Value = spend.NetSpend;
        s.Cell(4, 1).Value = "Total VAT (sum)";
        s.Cell(4, 2).Value = tax.TotalVat;

        var v = wb.AddWorksheet("Vendor analysis");
        v.Cell(1, 1).Value = "Vendor";
        v.Cell(1, 2).Value = "Net total";
        v.Cell(1, 3).Value = "Count";
        var vr = 2;
        foreach (var x in vendors)
        {
            v.Cell(vr, 1).Value = x.Vendor;
            v.Cell(vr, 2).Value = x.TotalAmount;
            v.Cell(vr, 3).Value = x.Count;
            vr++;
        }
        v.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] ToPdf(ReportFilterModel filter, List<ReportRowVm> rows, SpendSummaryVm spend, List<VendorAnalysisRow> vendors, TaxVatSummaryVm tax)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Header().Text("PCG Document Reports").SemiBold().FontSize(18);
                page.Content().Column(col =>
                {
                    col.Item().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(10).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(8).Text($"Filters — From: {filter.DateFrom:yyyy-MM-dd} To: {filter.DateTo:yyyy-MM-dd} Vendor: {filter.Vendor ?? "—"} Status: {filter.ApprovalStatusFilter ?? "—"}").FontSize(9);
                    col.Item().PaddingTop(12).Text("Spend summary").SemiBold();
                    col.Item().Text($"Invoices: {spend.TotalInvoices:N2}  Credit notes: {spend.TotalCreditNotes:N2}  Net: {spend.NetSpend:N2}  Docs: {spend.DocumentCount}");
                    col.Item().PaddingTop(8).Text("Tax / VAT").SemiBold();
                    col.Item().Text($"Total VAT (sum of lines): {tax.TotalVat:N2}");
                    col.Item().PaddingTop(12).Text("Top vendors").SemiBold();
                    foreach (var x in vendors.Take(15))
                        col.Item().Text($"{x.Vendor}: {x.TotalAmount:N2} ({x.Count} docs)").FontSize(10);

                    col.Item().PaddingTop(16).Text("Document lines (max 40)").SemiBold();
                    foreach (var row in rows.Take(40))
                    {
                        col.Item().Text($"{row.Id} | {row.Type} | {row.Vendor} | {row.Amount:N2} | {row.Status}").FontSize(9);
                    }
                });
            });
        });
        return doc.GeneratePdf();
    }
}
