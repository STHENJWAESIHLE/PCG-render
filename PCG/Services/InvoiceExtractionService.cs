using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PCG.Models;
using PCG.Models.Extraction;
using UglyToad.PdfPig;

namespace PCG.Services;

public class InvoiceExtractionOptions
{
    public string? OpenAIApiKey { get; set; }
    public string OpenAIModel { get; set; } = "gpt-4o-mini";
}

public class InvoiceExtractionService : IInvoiceExtractionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly InvoiceExtractionOptions _options;
    private readonly ILogger<InvoiceExtractionService> _logger;

    public InvoiceExtractionService(
        IHttpClientFactory httpClientFactory,
        IOptions<InvoiceExtractionOptions> options,
        ILogger<InvoiceExtractionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<InvoiceExtractionResult> ExtractAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        await using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes));

        string? text = null;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) || ext == ".pdf")
        {
            text = TryReadPdfText(bytes);
        }

        var heuristic = HeuristicFromText(text, fileName);

        if (!string.IsNullOrWhiteSpace(_options.OpenAIApiKey))
        {
            try
            {
                var ai = await CallOpenAIAsync(bytes, fileName, contentType, text, ct);
                if (ai != null)
                {
                    ai.UsedOpenAI = true;
                    MergePreferNonEmpty(heuristic, ai);
                    heuristic.RawNotes = (heuristic.RawNotes ?? "") + " OpenAI-assisted extraction.";
                    return heuristic;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenAI extraction failed; using heuristics only.");
            }
        }

        heuristic.UsedOpenAI = false;
        heuristic.RawNotes ??= string.IsNullOrEmpty(text)
            ? "No machine-readable text (try PDF with text layer or set OpenAI key for images)."
            : "Heuristic extraction from document text.";
        return heuristic;
    }

    private static void MergePreferNonEmpty(InvoiceExtractionResult target, InvoiceExtractionResult source)
    {
        if (!string.IsNullOrWhiteSpace(source.Vendor)) target.Vendor = source.Vendor;
        if (source.DocumentDate.HasValue) target.DocumentDate = source.DocumentDate;
        if (source.Amount.HasValue) target.Amount = source.Amount;
        if (source.VatAmount.HasValue) target.VatAmount = source.VatAmount;
        if (!string.IsNullOrWhiteSpace(source.InvoiceNumber)) target.InvoiceNumber = source.InvoiceNumber;
    }

    private static DocumentType? DetectDocumentType(string source)
    {
        // Credit note keywords
        var creditNotePatterns = new[]
        {
            @"(?i)credit\s*note",
            @"(?i)credit\s*memo",
            @"(?i)tax\s*credit",
            @"(?i)\\bcn\\b",
            @"(?i)credite?\s*nota"
        };

        foreach (var pattern in creditNotePatterns)
        {
            if (Regex.IsMatch(source, pattern))
                return DocumentType.CreditNote;
        }

        // Invoice keywords (default if no credit note indicators found)
        var invoicePatterns = new[]
        {
            @"(?i)\\binvoice\\b",
            @"(?i)\\btax\\s*invoice\\b",
            @"(?i)\\bfacture\\b",
            @"(?i)\\brechnung\\b"
        };

        foreach (var pattern in invoicePatterns)
        {
            if (Regex.IsMatch(source, pattern))
                return DocumentType.Invoice;
        }

        return null; // Could not determine
    }

    private static string? TryReadPdfText(byte[] bytes)
    {
        try
        {
            using var doc = PdfDocument.Open(bytes);
            var sb = new StringBuilder();
            foreach (var page in doc.GetPages())
                sb.AppendLine(page.Text);
            var t = sb.ToString();
            return string.IsNullOrWhiteSpace(t) ? null : t;
        }
        catch
        {
            return null;
        }
    }

    private static InvoiceExtractionResult HeuristicFromText(string? text, string fileName)
    {
        var r = new InvoiceExtractionResult();
        var source = (text ?? "") + " " + fileName;

        // Detect document type from keywords
        r.DetectedDocumentType = DetectDocumentType(source);

        var inv = Regex.Match(source, @"(?i)invoice\s*#?\s*[:.]?\s*([A-Z0-9\-\/]+)");
        if (inv.Success) r.InvoiceNumber = inv.Groups[1].Value.Trim();

        var money = Regex.Matches(source, @"(?:R|ZAR|\$|£|€)?\s*([\d]{1,3}(?:[.,\s]\d{3})*(?:[.,]\d{2})|\d+[.,]\d{2})");
        var amounts = new List<decimal>();
        foreach (Match m in money)
        {
            var s = m.Groups[1].Value.Replace(" ", "").Replace(",", ".");
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) && d > 0 && d < 1_000_000_000)
                amounts.Add(decimal.Round(d, 2));
        }
        if (amounts.Count > 0)
            r.Amount = amounts.OrderByDescending(x => x).First();

        var vat = Regex.Match(source, @"(?i)(?:vat|tax)\s*[:.]?\s*(?:R|ZAR|\$)?\s*([\d]+[.,]?\d*)");
        if (vat.Success && decimal.TryParse(vat.Groups[1].Value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            r.VatAmount = decimal.Round(v, 2);

        var dateMatch = Regex.Match(source, @"\b(\d{4}[-/]\d{2}[-/]\d{2}|\d{1,2}[-/]\d{1,2}[-/]\d{2,4})\b");
        if (dateMatch.Success && DateTime.TryParse(dateMatch.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            r.DocumentDate = dt.Date;

        var vendorLine = Regex.Match(source, @"(?i)(?:from|vendor|supplier|bill\s*from|seller)\s*[:.]?\s*([^\n\r,]{2,60})");
        if (vendorLine.Success)
        {
            var vendor = vendorLine.Groups[1].Value.Trim();
            // Stop at common invoice table headers and keywords
            var stopWords = new[] { "Currency", "Description", "Qty", "Quantity", "Unit", "Price", "Line Total",
                                    "Invoice", "Date", "Due Date", "Tax", "VAT", "Subtotal", "Total", "Payment",
                                    "Address", "Phone", "Email", "Reg", "Reg.", "Co.", "Pty", "Ltd", "Limited" };
            foreach (var word in stopWords)
            {
                var idx = vendor.IndexOf(word, StringComparison.OrdinalIgnoreCase);
                if (idx > 3) // Keep at least 3 chars before stop word
                    vendor = vendor[..idx].Trim();
            }
            // Handle camelCase concatenated text (e.g., "KingdomCurrency" -> stop at "Currency")
            var camelPattern = @"[a-z](?=[A-Z])";
            var camelMatches = Regex.Matches(vendor, camelPattern);
            foreach (Match m in camelMatches.Cast<Match>().Reverse())
            {
                var checkPos = m.Index + 1;
                var restOfString = vendor[checkPos..];
                foreach (var word in stopWords)
                {
                    if (restOfString.StartsWith(word, StringComparison.OrdinalIgnoreCase) && m.Index > 3)
                    {
                        vendor = vendor[..m.Index].Trim();
                        break;
                    }
                }
            }
            // Clean up trailing artifacts
            vendor = Regex.Replace(vendor, @"\s+", " "); // normalize spaces
            vendor = vendor.TrimEnd(',', '.', ':', ';', '-', ' ', '/', '\\');
            // Remove common suffixes that indicate table headers
            vendor = Regex.Replace(vendor, @"(?i)(document|supplies?|products?).*$", "").Trim();
            if (vendor.Length >= 2)
                r.Vendor = vendor;
        }

        return r;
    }

    private async Task<InvoiceExtractionResult?> CallOpenAIAsync(byte[] bytes, string fileName, string contentType, string? text, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.OpenAIApiKey);
        client.BaseAddress = new Uri("https://api.openai.com/");
        client.Timeout = TimeSpan.FromMinutes(2);

        var isImage = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        object userContent;
        if (isImage)
        {
            var b64 = Convert.ToBase64String(bytes);
            var mime = contentType.Split(';').FirstOrDefault() ?? "image/png";
            userContent = new object[]
            {
                new { type = "text", text = "Extract JSON only from this document image. Determine if it is an Invoice or Credit Note. Return: documentType (\"Invoice\" or \"CreditNote\"), vendor, documentDate (ISO yyyy-MM-dd), amount (number), vatAmount (number), invoiceNumber. Use null if unknown." },
                new { type = "image_url", image_url = new { url = $"data:{mime};base64,{b64}" } }
            };
        }
        else
        {
            var snippet = string.IsNullOrEmpty(text) ? "(no text extracted from PDF)" : text[..Math.Min(text.Length, 12000)];
            userContent = $"Document file name: {fileName}\n\nText content:\n{snippet}\n\nReturn JSON: documentType (\"Invoice\" or \"CreditNote\"), vendor, documentDate (ISO yyyy-MM-dd), amount, vatAmount, invoiceNumber. Use null if unknown.";
        }

        var body = new
        {
            model = _options.OpenAIModel,
            messages = new object[]
            {
                new { role = "system", content = "You extract structured invoice fields. Reply with JSON only, no markdown." },
                new { role = "user", content = userContent }
            },
            temperature = 0.1
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

        using var resp = await client.SendAsync(req, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI API error: {Status} {Body}", resp.StatusCode, json);
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(content)) return null;

        content = content.Trim();
        if (content.StartsWith("```"))
        {
            var idx = content.IndexOf('{');
            var end = content.LastIndexOf('}');
            if (idx >= 0 && end > idx) content = content[idx..(end + 1)];
        }

        var parsed = JsonSerializer.Deserialize<OpenAiInvoiceDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (parsed == null) return null;

        var result = new InvoiceExtractionResult
        {
            Vendor = parsed.Vendor,
            InvoiceNumber = parsed.InvoiceNumber,
            Amount = parsed.Amount,
            VatAmount = parsed.VatAmount,
            DetectedDocumentType = parsed.DocumentType?.ToLowerInvariant() switch
            {
                "creditnote" or "credit note" or "credit-note" or "credit_memo" or "credit memo" => DocumentType.CreditNote,
                "invoice" or "tax invoice" => DocumentType.Invoice,
                _ => null
            }
        };
        if (!string.IsNullOrEmpty(parsed.DocumentDate) && DateTime.TryParse(parsed.DocumentDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            result.DocumentDate = d.Date;
        return result;
    }

    private sealed class OpenAiInvoiceDto
    {
        [JsonPropertyName("documentType")]
        public string? DocumentType { get; set; }
        [JsonPropertyName("vendor")]
        public string? Vendor { get; set; }
        [JsonPropertyName("documentDate")]
        public string? DocumentDate { get; set; }
        [JsonPropertyName("amount")]
        public decimal? Amount { get; set; }
        [JsonPropertyName("vatAmount")]
        public decimal? VatAmount { get; set; }
        [JsonPropertyName("invoiceNumber")]
        public string? InvoiceNumber { get; set; }
    }
}
