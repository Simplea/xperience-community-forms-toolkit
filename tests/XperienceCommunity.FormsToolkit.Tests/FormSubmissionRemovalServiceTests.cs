using Microsoft.Extensions.Logging.Abstractions;

using XperienceCommunity.FormsToolkit.FormSubmissionExport;
using XperienceCommunity.FormsToolkit.FormSubmissionRemoval;

namespace XperienceCommunity.FormsToolkit.Tests;

public class FormSubmissionRemovalServiceTests
{
    [Test]
    public void TryDeleteUploadedFile_ReturnsTrue_WhenFileNameIsEmpty()
    {
        var fileStore = new FakeUploadedFilePhysicalStore();
        var service = CreateService(fileStore);

        bool result = service.TryDeleteUploadedFile(string.Empty, formId: 1, submissionId: 1);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(fileStore.ExistsCalls, Is.Empty);
            Assert.That(fileStore.DeleteCalls, Is.Empty);
        });
    }

    [Test]
    public void TryDeleteUploadedFile_DeletesFileAndReturnsTrue_WhenFileExists()
    {
        var fileStore = new FakeUploadedFilePhysicalStore { FileExists = true };
        var service = CreateService(fileStore);

        bool result = service.TryDeleteUploadedFile("file.txt", formId: 1, submissionId: 1);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(fileStore.DeleteCalls, Is.EqualTo(new[] { "file.txt" }));
        });
    }

    [Test]
    public void TryDeleteUploadedFile_ReturnsTrue_WithoutDeleting_WhenFileDoesNotExist()
    {
        var fileStore = new FakeUploadedFilePhysicalStore { FileExists = false };
        var service = CreateService(fileStore);

        bool result = service.TryDeleteUploadedFile("file.txt", formId: 1, submissionId: 1);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(fileStore.DeleteCalls, Is.Empty);
        });
    }

    [Test]
    public void TryDeleteUploadedFile_ReturnsFalse_WhenDeleteThrows()
    {
        var fileStore = new FakeUploadedFilePhysicalStore
        {
            FileExists = true,
            DeleteException = new IOException("File is locked."),
        };
        var service = CreateService(fileStore);

        bool result = service.TryDeleteUploadedFile("file.txt", formId: 1, submissionId: 1);

        // A failed delete of a file that still exists must never be reported as success:
        // the caller relies on this to avoid deleting the submission row and orphaning the file.
        Assert.That(result, Is.False);
    }

    private static FormSubmissionRemovalService CreateService(IUploadedFilePhysicalStore fileStore) =>
        new(
            new NotSupportedExportService(),
            fileStore,
            NullLogger<FormSubmissionRemovalService>.Instance);

    private sealed class FakeUploadedFilePhysicalStore : IUploadedFilePhysicalStore
    {
        public bool FileExists { get; set; }

        public Exception? DeleteException { get; set; }

        public List<string> ExistsCalls { get; } = [];

        public List<string> DeleteCalls { get; } = [];

        public bool Exists(string systemFileName)
        {
            ExistsCalls.Add(systemFileName);
            return FileExists;
        }

        public void Delete(string systemFileName)
        {
            DeleteCalls.Add(systemFileName);
            if (DeleteException is not null)
            {
                throw DeleteException;
            }
        }
    }

    private sealed class NotSupportedExportService : IFormSubmissionExportService
    {
        private static NotSupportedException NotUsed() =>
            new("Not used by the tests exercising file-cleanup behavior.");

        public Task<FormSubmissionExportDefinition> GetDefinitionAsync(int formId, CancellationToken cancellationToken) =>
            throw NotUsed();

        public Task<bool> HasAnySubmissionsAsync(int formId, CancellationToken cancellationToken) =>
            throw NotUsed();

        public Task<PreparedFormSubmissionExport> PrepareAsync(
            int formId,
            FormSubmissionExportCommandRequest request,
            CancellationToken cancellationToken) =>
            throw NotUsed();

        public Task<PreparedFormSubmissionExport> PrepareAsync(
            int formId,
            FormSubmissionExportOptions options,
            CancellationToken cancellationToken) =>
            throw NotUsed();

        public Task<PreparedFormSubmissionExport> PrepareCurrentViewAsync(
            int formId,
            FormSubmissionCurrentViewExportRequest request,
            CancellationToken cancellationToken) =>
            throw NotUsed();

        public Task<int> WriteAsync(
            PreparedFormSubmissionExport export,
            Stream output,
            CancellationToken cancellationToken) =>
            throw NotUsed();
    }
}
