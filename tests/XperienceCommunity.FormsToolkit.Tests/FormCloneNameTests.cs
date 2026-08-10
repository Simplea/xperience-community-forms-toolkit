using XperienceCommunity.FormsToolkit.FormCloning;

namespace XperienceCommunity.FormsToolkit.Tests;

public class FormCloneNameTests
{
    [Test]
    public void CreatesDefaultCloneName() =>
        Assert.That(FormCloneName.GetDefault("Contact us"), Is.EqualTo("Contact us (copy)"));

    [Test]
    public void TrimsSubmittedName() =>
        Assert.That(FormCloneName.Normalize("  Contact us copy  "), Is.EqualTo("Contact us copy"));

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void RejectsEmptySubmittedName(string? displayName)
    {
        var exception = Assert.Throws<FormCloneValidationException>(() => FormCloneName.Normalize(displayName));

        Assert.That(exception!.Message, Is.EqualTo("Form name is required."));
    }

    [Test]
    public void RejectsSubmittedNameOverMaximumLength()
    {
        string displayName = new('a', FormCloneConstants.MaximumDisplayNameLength + 1);

        Assert.Throws<FormCloneValidationException>(() => FormCloneName.Normalize(displayName));
    }

    [Test]
    public void TruncatesLongSourceNameWhenCreatingDefault()
    {
        string result = FormCloneName.GetDefault(new string('a', FormCloneConstants.MaximumDisplayNameLength));

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Length.EqualTo(FormCloneConstants.MaximumDisplayNameLength));
            Assert.That(result, Does.EndWith(" (copy)"));
        });
    }
}
