using XperienceCommunity.FormsToolkit.FormSubmissionExport;

namespace XperienceCommunity.FormsToolkit.Tests;

public class FormSubmissionUpperBoundaryTests
{
    [Test]
    public void UsesTheCurrentBoundaryWhenNoneIsRequested() =>
        Assert.That(FormSubmissionUpperBoundary.Resolve(1019, null), Is.EqualTo(1019));

    [Test]
    public void ExcludesSubmissionsCreatedAfterTheRequestedBoundary() =>
        Assert.That(FormSubmissionUpperBoundary.Resolve(1020, 1019), Is.EqualTo(1019));

    [Test]
    public void CannotBeWidenedBeyondTheCurrentBoundary() =>
        Assert.That(FormSubmissionUpperBoundary.Resolve(1019, int.MaxValue), Is.EqualTo(1019));

    [Test]
    public void KeepsAnEmptyPreviewEmpty() =>
        Assert.That(FormSubmissionUpperBoundary.Resolve(1019, 0), Is.EqualTo(0));
}
