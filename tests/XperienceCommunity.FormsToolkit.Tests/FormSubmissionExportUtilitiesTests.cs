using CMS.OnlineForms;

using XperienceCommunity.FormsToolkit.FormSubmissionExport;

namespace XperienceCommunity.FormsToolkit.Tests;

public class FormSubmissionExportUtilitiesTests
{
    [Test]
    public void CreatesSafeInvariantFileName()
    {
        string result = FormSubmissionExportFileName.Create(
            "Contact:Us/../../Test",
            FormSubmissionExportFormat.Excel,
            preview: true,
            new DateTimeOffset(2026, 8, 6, 12, 34, 56, TimeSpan.FromHours(-5)));

        Assert.That(result, Is.EqualTo("Contact-Us-------Test-submissions-preview-20260806-173456Z.xlsx"));
        Assert.That(result, Does.Match("^[A-Za-z0-9_-]+-submissions-preview-[0-9]{8}-[0-9]{6}Z\\.xlsx$"));
    }

    [TestCase(@"C:\\temporary\\private\\résumé.pdf", "résumé.pdf")]
    [TestCase("/srv/private/report.csv", "report.csv")]
    [TestCase("filename.txt", "filename.txt")]
    [TestCase(null, "")]
    public void UploadedFilesContainOnlyTheOriginalFileName(string? value, string expected) =>
        Assert.That(UploadedFileName.Extract(value), Is.EqualTo(expected));

    [Test]
    public void UploadedFileObjectsUseOriginalNameInsteadOfSystemName()
    {
        var value = new BizFormUploadFile
        {
            SystemFileName = @"C:\assets\bizformfiles\internal-file-name.bin",
            OriginalFileName = @"C:\fake-browser-path\customer-report.xlsx",
        };

        Assert.That(UploadedFileName.Extract(value), Is.EqualTo("customer-report.xlsx"));
    }

    [TestCase(null, null, false)]
    [TestCase(null, "Kentico.Forms.Web.Mvc.FileUploaderComponent", true)]
    [TestCase(null, "Custom.FileUploaderLookalikeComponent", false)]
    [TestCase(BizFormUploadFile.DATATYPE_FORMFILE, "Custom.UploadComponent", true)]
    [TestCase("text", "Custom.TextInputComponent", false)]
    public void DetectsUploadedFileComponentsFromMetadata(
        string? dataType,
        string? componentIdentifier,
        bool expected) =>
        Assert.That(FormSubmissionExportService.IsUploadedFileField(dataType, componentIdentifier), Is.EqualTo(expected));
}
