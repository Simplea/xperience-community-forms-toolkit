namespace XperienceCommunity.FormsToolkit.Tests;

public class FormsToolkitOptionsTests
{
    [Test]
    public void FormSubmissionExportIsEnabledByDefault() =>
        Assert.That(new FormsToolkitOptions().EnableFormSubmissionExport, Is.True);

    [Test]
    public void FormSubmissionRemovalIsEnabledByDefault() =>
        Assert.That(new FormsToolkitOptions().EnableFormSubmissionRemoval, Is.True);
}
