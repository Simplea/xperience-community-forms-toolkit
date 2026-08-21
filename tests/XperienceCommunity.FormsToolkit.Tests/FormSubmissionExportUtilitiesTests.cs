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

    [Test]
    public void SystemFileNameExtractionUsesSystemNameInsteadOfOriginalName()
    {
        var value = new BizFormUploadFile
        {
            SystemFileName = @"C:\assets\bizformfiles\internal-file-name.bin",
            OriginalFileName = @"C:\fake-browser-path\customer-report.xlsx",
        };

        Assert.That(UploadedFileName.ExtractSystemFileName(value), Is.EqualTo("internal-file-name.bin"));
    }

    [Test]
    public void ParsesTheFileUploaderComponentsCombinedRawValue()
    {
        // The "Kentico.FileUploader" form component stores its raw column value as a single
        // string, "{systemFileName}/{originalFileName}", confirmed directly against a running
        // instance's database rather than assumed. The system file name is the segment actually
        // used for physical storage; the original file name is what the uploader saw.
        const string raw = "9fa205b3-caf8-4501-9b93-ca3fac72834d.pdf/ANDRÉS_VILLENAS_CV.pdf";

        Assert.Multiple(() =>
        {
            Assert.That(UploadedFileName.ExtractSystemFileName(raw), Is.EqualTo("9fa205b3-caf8-4501-9b93-ca3fac72834d.pdf"));
            Assert.That(UploadedFileName.Extract(raw), Is.EqualTo("ANDRÉS_VILLENAS_CV.pdf"));
        });
    }

    [TestCase("349baeed-2791-484a-a645-851e89a73cfd.pdf", "349baeed-2791-484a-a645-851e89a73cfd.pdf")]
    [TestCase(@"C:\\temporary\\private\\résumé.pdf", "résumé.pdf")]
    [TestCase(null, "")]
    public void SystemFileNameExtractionFallsBackToARawStringValue(string? value, string expected) =>
        Assert.That(UploadedFileName.ExtractSystemFileName(value), Is.EqualTo(expected));

    [TestCase(null, null, false)]
    [TestCase(null, "Kentico.FileUploader", true)]
    [TestCase(null, "Custom.FileUploaderLookalikeComponent", false)]
    [TestCase(BizFormUploadFile.DATATYPE_FORMFILE, "Custom.UploadComponent", true)]
    [TestCase("text", "Custom.TextInputComponent", false)]
    public void DetectsUploadedFileComponentsFromMetadata(
        string? dataType,
        string? componentIdentifier,
        bool expected) =>
        Assert.That(FormSubmissionExportService.IsUploadedFileField(dataType, componentIdentifier), Is.EqualTo(expected));
}
