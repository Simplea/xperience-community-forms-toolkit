using XperienceCommunity.FormsToolkit.FormSubmissionExport;

namespace XperienceCommunity.FormsToolkit.Tests;

public class FormSubmissionCurrentViewTests
{
    [TestCase(new[] { 0 })]
    [TestCase(new[] { -1 })]
    [TestCase(new[] { 7, 7 })]
    public void RejectsInvalidOrDuplicateSubmissionIdentifiers(int[] identifiers) => Assert.Throws<FormSubmissionExportValidationException>(
            () => FormSubmissionExportService.ValidateCurrentViewSubmissionIds(identifiers));

    [Test]
    public void AllowsAnEmptyCurrentPage() => Assert.DoesNotThrow(() => FormSubmissionExportService.ValidateCurrentViewSubmissionIds([]));

    [Test]
    public void PreservesTheDisplayedColumnOrder()
    {
        FormSubmissionExportField[] available =
        [
            new("first", "First", "First", false, FormSubmissionExportFieldKind.FormField),
            new("second", "Second", "Second", false, FormSubmissionExportFieldKind.FormField),
            new("third", "Third", "Third", false, FormSubmissionExportFieldKind.FormField),
        ];

        var selected = FormSubmissionExportService.ResolveSelectedFields(available, ["third", "first"]);

        Assert.That(selected.Select(field => field.Identifier), Is.EqualTo(new[] { "third", "first" }));
    }
}
