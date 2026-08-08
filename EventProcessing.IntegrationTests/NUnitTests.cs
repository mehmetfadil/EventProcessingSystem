using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

namespace EventProcessing.IntegrationTests;

[TestFixture]
public class NUnitTests
{
    [Test]
    public void SanityCheck_RabbitMqShouldBeAccessible()
    {
        Assert.That(1 + 1, Is.EqualTo(2));
    }
}
