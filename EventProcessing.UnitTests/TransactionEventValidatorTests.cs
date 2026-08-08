using EventProcessing.Core.Enums;
using EventProcessing.Core.Models;
using EventProcessing.IngestionApi.Validators;

namespace EventProcessing.UnitTests;

public class TransactionEventValidatorTests
{
    private readonly TransactionEventValidator _validator = new();

    private static TransactionEvent ValidEvent => new()
    {
        EventId = Guid.NewGuid(),
        CustomerId = "customer-001",
        Type = TransactionType.Credit,
        Amount = 100m,
        Currency = "TRY",
        OccurredAt = DateTime.UtcNow
    };

    // =========================================================================
    // BAŞARILI SENARYOLAR
    // =========================================================================

    [Fact]
    public async Task Validate_ValidEvent_Passes()
    {
        var result = await _validator.ValidateAsync(ValidEvent);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_DebitEvent_Passes()
    {
        var evt = ValidEvent;
        evt.Type = TransactionType.Debit;
        var result = await _validator.ValidateAsync(evt);
        Assert.True(result.IsValid);
    }

    // =========================================================================
    // EventId DOĞRULAMALARI
    // =========================================================================

    [Fact]
    public async Task Validate_EmptyEventId_Fails()
    {
        var evt = ValidEvent;
        evt.EventId = Guid.Empty;
        var result = await _validator.ValidateAsync(evt);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "EventId");
    }

    // =========================================================================
    // CustomerId DOĞRULAMALARI
    // =========================================================================

    [Fact]
    public async Task Validate_EmptyCustomerId_Fails()
    {
        var evt = ValidEvent;
        evt.CustomerId = "";
        var result = await _validator.ValidateAsync(evt);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "CustomerId");
    }

    [Fact]
    public async Task Validate_CustomerIdTooLong_Fails()
    {
        var evt = ValidEvent;
        evt.CustomerId = new string('x', 101); // max 100
        var result = await _validator.ValidateAsync(evt);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "CustomerId");
    }

    [Fact]
    public async Task Validate_CustomerIdMaxLength_Passes()
    {
        var evt = ValidEvent;
        evt.CustomerId = new string('x', 100); // tam 100
        var result = await _validator.ValidateAsync(evt);
        Assert.True(result.IsValid);
    }

    // =========================================================================
    // Amount DOĞRULAMALARI
    // =========================================================================

    [Fact]
    public async Task Validate_ZeroAmount_Fails()
    {
        var evt = ValidEvent;
        evt.Amount = 0;
        var result = await _validator.ValidateAsync(evt);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Amount");
    }

    [Fact]
    public async Task Validate_NegativeAmount_Fails()
    {
        var evt = ValidEvent;
        evt.Amount = -50m;
        var result = await _validator.ValidateAsync(evt);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Amount");
    }

    // =========================================================================
    // Currency DOĞRULAMALARI
    // =========================================================================

    [Fact]
    public async Task Validate_EmptyCurrency_Fails()
    {
        var evt = ValidEvent;
        evt.Currency = "";
        var result = await _validator.ValidateAsync(evt);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Currency");
    }

    [Fact]
    public async Task Validate_ShortCurrency_Fails()
    {
        var evt = ValidEvent;
        evt.Currency = "AB"; // 2 karakter, 3 olmalı
        var result = await _validator.ValidateAsync(evt);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Currency");
    }

    [Fact]
    public async Task Validate_LongCurrency_Fails()
    {
        var evt = ValidEvent;
        evt.Currency = "ABCD"; // 4 karakter, 3 olmalı
        var result = await _validator.ValidateAsync(evt);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Currency");
    }

    [Fact]
    public async Task Validate_LowercaseCurrency_Fails()
    {
        var evt = ValidEvent;
        evt.Currency = "try"; // büyük harf olmalı
        var result = await _validator.ValidateAsync(evt);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Currency");
    }

    // =========================================================================
    // OccurredAt DOĞRULAMALARI
    // =========================================================================

    [Fact]
    public async Task Validate_DefaultOccurredAt_Fails()
    {
        var evt = ValidEvent;
        evt.OccurredAt = default; // DateTime.MinValue
        var result = await _validator.ValidateAsync(evt);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "OccurredAt");
    }
}
