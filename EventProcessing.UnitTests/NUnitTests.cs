using EventProcessing.Core.Enums;
using EventProcessing.Core.Models;
using EventProcessing.IngestionApi.Validators;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

namespace EventProcessing.UnitTests;

[TestFixture]
public class NUnitTests
{
    private TransactionEventValidator _validator = null!;

    [SetUp]
    public void Setup()
    {
        _validator = new TransactionEventValidator();
    }

    [Test]
    public async Task Validate_ValidEvent_ShouldPass()
    {
        var evt = new TransactionEvent
        {
            EventId = Guid.NewGuid(),
            CustomerId = "nunit-test",
            Type = TransactionType.Credit,
            Amount = 100m,
            Currency = "TRY",
            OccurredAt = DateTime.UtcNow
        };

        var result = await _validator.ValidateAsync(evt);
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public async Task Validate_EmptyEventId_ShouldFail()
    {
        var evt = new TransactionEvent
        {
            EventId = Guid.Empty,
            CustomerId = "nunit-test",
            Type = TransactionType.Credit,
            Amount = 100m,
            Currency = "TRY",
            OccurredAt = DateTime.UtcNow
        };

        var result = await _validator.ValidateAsync(evt);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.PropertyName == "EventId"), Is.True);
    }

    [Test]
    public void Test1()
    {
        Assert.That(1 + 1, Is.EqualTo(2));
    }
}
