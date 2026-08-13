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

    [Test]
    public void AllowsColumnsThatAreVisibleInTheListing()
    {
        FormSubmissionExportField[] available =
        [
            new("first", "First", "First", false, FormSubmissionExportFieldKind.FormField, VisibleInListing: true),
            new("second", "Second", "Second", false, FormSubmissionExportFieldKind.FormField, VisibleInListing: false),
        ];

        Assert.DoesNotThrow(() => FormSubmissionExportService.ValidateCurrentViewColumnIdentifiers(["first"], available));
    }

    [Test]
    public void RejectsAColumnThatIsNotVisibleInTheListing()
    {
        // Reproduces the "Export selected" bug: a field that exists on the form (for example,
        // the raw primary-key column) but is not rendered in the listing must still be rejected,
        // exactly like the current-view header dropdown already enforces.
        FormSubmissionExportField[] available =
        [
            new("first", "First", "First", false, FormSubmissionExportFieldKind.FormField, VisibleInListing: true),
            new("hidden", "Hidden", "Hidden", false, FormSubmissionExportFieldKind.FormField, VisibleInListing: false),
        ];

        Assert.Throws<FormSubmissionExportValidationException>(
            () => FormSubmissionExportService.ValidateCurrentViewColumnIdentifiers(["first", "hidden"], available));
    }
}
