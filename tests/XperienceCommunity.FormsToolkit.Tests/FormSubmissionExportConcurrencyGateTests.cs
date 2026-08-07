using Microsoft.Extensions.Options;

using XperienceCommunity.FormsToolkit.FormSubmissionExport;

namespace XperienceCommunity.FormsToolkit.Tests;

public class FormSubmissionExportConcurrencyGateTests
{
    [Test]
    public void EnforcesPerUserAndApplicationLimitsAndReleasesLeases()
    {
        using var gate = new FormSubmissionExportConcurrencyGate(
            Options.Create(new FormsToolkitOptions { MaximumConcurrentExports = 2 }));

        using var first = gate.TryAcquire(1);
        using var second = gate.TryAcquire(2);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(gate.TryAcquire(1), Is.Null);
            Assert.That(gate.TryAcquire(3), Is.Null);
        });

        second!.Dispose();
        using var third = gate.TryAcquire(3);
        Assert.That(third, Is.Not.Null);
    }
}
