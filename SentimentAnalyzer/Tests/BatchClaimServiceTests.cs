using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using SentimentAnalyzer.Agents.Orchestration;
using SentimentAnalyzer.API.Services.Claims;

namespace SentimentAnalyzer.Tests;

/// <summary>
/// Unit tests for <see cref="BatchClaimService"/> CSV batch processing.
/// Uses realistic insurance claim data — never "test", "foo", or "bar".
/// </summary>
public class BatchClaimServiceTests
{
    private readonly Mock<IPIIRedactor> _mockRedactor;
    private readonly Mock<ILogger<BatchClaimService>> _mockLogger;
    private readonly BatchClaimService _service;

    public BatchClaimServiceTests()
    {
        _mockRedactor = new Mock<IPIIRedactor>();
        // Default: redactor returns input unchanged (PII is redacted in real impl)
        _mockRedactor.Setup(r => r.Redact(It.IsAny<string>())).Returns<string>(s => s);

        _mockLogger = new Mock<ILogger<BatchClaimService>>();
        _service = new BatchClaimService(_mockRedactor.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessBatchAsync_ValidCsvWith5Rows_Returns5ResultsZeroErrors()
    {
        // Arrange
        var csv = new StringBuilder();
        csv.AppendLine("ClaimId,ClaimType,Description,EstimatedAmount,IncidentDate");
        csv.AppendLine("CLM-2024-001,Water Damage,Burst pipe in basement causing flooding to 500 sq ft,12000,2024-01-15");
        csv.AppendLine("CLM-2024-002,Auto Collision,Rear-end collision on Highway 101 with police report filed,8500,2024-01-20");
        csv.AppendLine("CLM-2024-003,Theft,Home burglary while traveling with electronics and jewelry stolen,25000,2024-02-01");
        csv.AppendLine("CLM-2024-004,Liability,Visitor slipped on icy walkway sustaining broken wrist,6500,2024-02-08");
        csv.AppendLine("CLM-2024-005,Property Damage,Tree fell on garage roof during windstorm causing structural damage,18000,2024-02-10");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));

        // Act
        var result = await _service.ProcessBatchAsync(stream);

        // Assert
        Assert.NotEmpty(result.BatchId);
        Assert.StartsWith("BATCH-", result.BatchId);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(5, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(5, result.ProcessedCount);
        Assert.Equal("Completed", result.Status);
        Assert.Equal(5, result.Results.Count);
        Assert.Empty(result.Errors);

        // Verify each result has correct row numbers (2-based due to header)
        Assert.Equal(2, result.Results[0].RowNumber);
        Assert.Equal("CLM-2024-001", result.Results[0].ClaimId);
        Assert.Equal("Triaged", result.Results[0].Status);
        Assert.NotEmpty(result.Results[0].Severity);

        // Verify PII redactor was called for each description
        _mockRedactor.Verify(r => r.Redact(It.IsAny<string>()), Times.Exactly(5));
    }

    [Fact]
    public async Task ProcessBatchAsync_MixedValidAndInvalidRows_ReturnsPartialSuccess()
    {
        // Arrange
        var csv = new StringBuilder();
        csv.AppendLine("ClaimId,ClaimType,Description,EstimatedAmount,IncidentDate");
        csv.AppendLine("CLM-2024-010,Water Damage,Pipe burst in kitchen flooding first floor,15000,2024-03-01");
        csv.AppendLine(",Auto Collision,Missing claim ID should cause error,5000,2024-03-02"); // Missing ClaimId
        csv.AppendLine("CLM-2024-012,Fire,,7500,2024-03-03"); // Missing Description
        csv.AppendLine("CLM-2024-013,Theft,Jewelry stolen from safe during vacation,not-a-number,2024-03-04"); // Invalid amount
        csv.AppendLine("CLM-2024-014,Liability,Guest injured on property during party,9000,2024-03-05");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));

        // Act
        var result = await _service.ProcessBatchAsync(stream);

        // Assert
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(3, result.ErrorCount);
        Assert.Equal("Completed", result.Status);
        Assert.Equal(2, result.Results.Count);
        Assert.Equal(3, result.Errors.Count);

        // Verify specific error messages
        var claimIdError = result.Errors.First(e => e.Field == "ClaimId");
        Assert.Equal(3, claimIdError.RowNumber);
        Assert.Contains("required", claimIdError.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var descError = result.Errors.First(e => e.Field == "Description");
        Assert.Equal(4, descError.RowNumber);

        var amountError = result.Errors.First(e => e.Field == "EstimatedAmount");
        Assert.Equal(5, amountError.RowNumber);
        Assert.Contains("not-a-number", amountError.ErrorMessage);
    }

    [Fact]
    public async Task ProcessBatchAsync_EmptyCsv_ReturnsFailedWithError()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(""));

        // Act
        var result = await _service.ProcessBatchAsync(stream);

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Equal(0, result.TotalCount);
        Assert.Single(result.Errors);
        Assert.Contains("empty", result.Errors[0].ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessBatchAsync_MissingRequiredColumns_ReturnsFailedWithHeaderError()
    {
        // Arrange — CSV with wrong column names
        var csv = "PolicyNumber,Type,Notes\nPOL-001,Auto,Some notes\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await _service.ProcessBatchAsync(stream);

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Single(result.Errors);
        Assert.Equal("Headers", result.Errors[0].Field);
        Assert.Contains("ClaimId", result.Errors[0].ErrorMessage);
        Assert.Contains("Description", result.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task ProcessBatchAsync_QuotedCommasInDescription_ParsesCorrectly()
    {
        // Arrange — Description contains commas within quotes (RFC 4180)
        var csv = new StringBuilder();
        csv.AppendLine("ClaimId,ClaimType,Description,EstimatedAmount,IncidentDate");
        csv.AppendLine("CLM-2024-020,Water Damage,\"Pipe burst in kitchen, basement, and garage causing extensive flooding\",22000,2024-04-01");
        csv.AppendLine("CLM-2024-021,Auto Collision,\"Rear-end collision at intersection of Main St. and 5th Ave, police report #2024-5678 filed\",9500,2024-04-02");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));

        // Act
        var result = await _service.ProcessBatchAsync(stream);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal("Completed", result.Status);

        // Verify the descriptions were parsed correctly (commas preserved within quotes)
        _mockRedactor.Verify(r => r.Redact(It.Is<string>(s => s.Contains("kitchen, basement, and garage"))), Times.Once);
        _mockRedactor.Verify(r => r.Redact(It.Is<string>(s => s.Contains("Main St. and 5th Ave"))), Times.Once);
    }

    [Fact]
    public async Task ProcessBatchAsync_HeaderOnlyNoDataRows_ReturnsFailedWithError()
    {
        // Arrange
        var csv = "ClaimId,ClaimType,Description,EstimatedAmount,IncidentDate\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await _service.ProcessBatchAsync(stream);

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Equal(0, result.TotalCount);
        Assert.Single(result.Errors);
        Assert.Contains("no data rows", result.Errors[0].ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessBatchAsync_InvalidDateFormat_ReturnsErrorForRow()
    {
        // Arrange
        var csv = new StringBuilder();
        csv.AppendLine("ClaimId,ClaimType,Description,EstimatedAmount,IncidentDate");
        csv.AppendLine("CLM-2024-030,Water Damage,Flooding in basement from broken water heater,10000,not-a-date");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));

        // Act
        var result = await _service.ProcessBatchAsync(stream);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Single(result.Errors);
        Assert.Equal("IncidentDate", result.Errors[0].Field);
        Assert.Contains("not-a-date", result.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task ProcessBatchAsync_SeverityDetermination_CriticalForFireClaims()
    {
        // Arrange
        var csv = new StringBuilder();
        csv.AppendLine("ClaimId,ClaimType,Description,EstimatedAmount,IncidentDate");
        csv.AppendLine("CLM-2024-040,Structure Fire,Catastrophic structure fire destroyed entire home and garage,150000,2024-05-01");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));

        // Act
        var result = await _service.ProcessBatchAsync(stream);

        // Assert
        Assert.Single(result.Results);
        Assert.Equal("Critical", result.Results[0].Severity);
    }

    [Fact]
    public async Task ProcessBatchAsync_AllRowsInvalid_ReturnsFailedStatus()
    {
        // Arrange
        var csv = new StringBuilder();
        csv.AppendLine("ClaimId,ClaimType,Description,EstimatedAmount,IncidentDate");
        csv.AppendLine(",Auto,Missing claim ID,5000,2024-06-01");
        csv.AppendLine("CLM-001,,Missing claim type,5000,2024-06-02");
        csv.AppendLine("CLM-002,Fire,,5000,2024-06-03");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));

        // Act
        var result = await _service.ProcessBatchAsync(stream);

        // Assert
        Assert.Equal("Failed", result.Status);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(3, result.ErrorCount);
    }

    [Fact]
    public void ParseCsvLine_SimpleFields_ParsesCorrectly()
    {
        // Arrange & Act
        var fields = BatchClaimService.ParseCsvLine("CLM-001,Auto,Rear-end collision,5000,2024-01-15");

        // Assert
        Assert.Equal(5, fields.Length);
        Assert.Equal("CLM-001", fields[0]);
        Assert.Equal("Auto", fields[1]);
        Assert.Equal("Rear-end collision", fields[2]);
        Assert.Equal("5000", fields[3]);
        Assert.Equal("2024-01-15", fields[4]);
    }

    [Fact]
    public void ParseCsvLine_QuotedFieldWithComma_ParsesCorrectly()
    {
        // Arrange & Act
        var fields = BatchClaimService.ParseCsvLine("CLM-002,Theft,\"Jewelry, electronics, and cash stolen from home\",25000,2024-02-01");

        // Assert
        Assert.Equal(5, fields.Length);
        Assert.Equal("CLM-002", fields[0]);
        Assert.Equal("Theft", fields[1]);
        Assert.Equal("Jewelry, electronics, and cash stolen from home", fields[2]);
        Assert.Equal("25000", fields[3]);
    }

    [Fact]
    public void ParseCsvLine_EscapedQuotes_ParsesCorrectly()
    {
        // Arrange — double quotes within quoted field
        var fields = BatchClaimService.ParseCsvLine("CLM-003,Liability,\"Customer said \"\"I slipped on ice\"\" near entrance\",8000,2024-03-01");

        // Assert
        Assert.Equal(5, fields.Length);
        Assert.Equal("Customer said \"I slipped on ice\" near entrance", fields[2]);
    }

    [Fact]
    public async Task ProcessBatchAsync_CurrencySymbolInAmount_ParsesSuccessfully()
    {
        // Arrange — Amount contains $ and commas
        var csv = new StringBuilder();
        csv.AppendLine("ClaimId,ClaimType,Description,EstimatedAmount,IncidentDate");
        csv.AppendLine("CLM-2024-050,Water Damage,Pipe burst flooding basement and first floor,\"$12,500.00\",2024-07-01");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));

        // Act
        var result = await _service.ProcessBatchAsync(stream);

        // Assert — should parse successfully since we strip $ and commas
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task ProcessBatchAsync_PIIRedactorIsCalled_ForEachValidRow()
    {
        // Arrange
        var csv = new StringBuilder();
        csv.AppendLine("ClaimId,ClaimType,Description,EstimatedAmount,IncidentDate");
        csv.AppendLine("CLM-2024-060,Auto,My SSN is 123-45-6789 and I had an accident on Highway 101,5000,2024-08-01");
        csv.AppendLine("CLM-2024-061,Theft,Contact me at john@email.com about stolen property,15000,2024-08-02");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));

        // Act
        var result = await _service.ProcessBatchAsync(stream);

        // Assert
        Assert.Equal(2, result.SuccessCount);
        _mockRedactor.Verify(r => r.Redact(It.Is<string>(s => s.Contains("123-45-6789"))), Times.Once);
        _mockRedactor.Verify(r => r.Redact(It.Is<string>(s => s.Contains("john@email.com"))), Times.Once);
    }
}
