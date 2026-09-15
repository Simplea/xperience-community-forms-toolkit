using CMS.DataEngine;
using CMS.OnlineForms;

using Microsoft.Extensions.Logging;

using XperienceCommunity.FormsToolkit.FormSubmissionExport;

namespace XperienceCommunity.FormsToolkit.FormSubmissionRemoval;

public interface IFormSubmissionRemovalService
{
    public Task<int> DeleteByIdsAsync(int formId, IReadOnlyList<int> submissionIds, CancellationToken cancellationToken);

    public Task<int> CountMatchingAsync(int formId, FormSubmissionRemovalOptions options, CancellationToken cancellationToken);

    public Task<int> DeleteMatchingAsync(int formId, FormSubmissionRemovalOptions options, CancellationToken cancellationToken);
}

internal sealed class FormSubmissionRemovalService(
    IFormSubmissionExportService exportService,
    IUploadedFilePhysicalStore fileStore,
    ILogger<FormSubmissionRemovalService> logger) : IFormSubmissionRemovalService
{
    private const int BatchSize = 1000;

    public async Task<int> DeleteByIdsAsync(int formId, IReadOnlyList<int> submissionIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submissionIds);
        var distinctIds = submissionIds.Where(id => id > 0).Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            throw new FormSubmissionRemovalValidationException("No submissions were selected.");
        }

        var definition = await exportService.GetDefinitionAsync(formId, cancellationToken);
        var uploadFieldSourceNames = GetUploadFieldSourceNames(definition);
        string[] columns = [definition.ItemIdColumn, .. uploadFieldSourceNames];

        int deletedCount = 0;
        foreach (int[] idBatch in distinctIds.Chunk(BatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var items = await BizFormItemProvider.GetItems(definition.FormClassName)
                .Columns(columns)
                .WhereIn(definition.ItemIdColumn, idBatch)
                .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken);

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!DeleteUploadedFiles(item, uploadFieldSourceNames, formId))
                {
                    return deletedCount;
                }

                item.Delete();
                deletedCount++;
            }
        }

        return deletedCount;
    }

    public async Task<int> CountMatchingAsync(int formId, FormSubmissionRemovalOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var definition = await exportService.GetDefinitionAsync(formId, cancellationToken);

        var query = ApplyRange(BizFormItemProvider.GetItems(definition.FormClassName), options.Range)
            .Columns(definition.ItemIdColumn);

        if (options.MaximumRecords is int limit)
        {
            var bounded = await query.TopN(limit).GetEnumerableTypedResultAsync(cancellationToken: cancellationToken);
            return bounded.Count();
        }

        return query.Count;
    }

    public async Task<int> DeleteMatchingAsync(int formId, FormSubmissionRemovalOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var definition = await exportService.GetDefinitionAsync(formId, cancellationToken);
        var uploadFieldSourceNames = GetUploadFieldSourceNames(definition);
        string[] columns = [definition.ItemIdColumn, nameof(BizFormItem.FormInserted), .. uploadFieldSourceNames];

        var upperBoundaryResult = await BizFormItemProvider.GetItems(definition.FormClassName)
            .Columns(definition.ItemIdColumn)
            .OrderByDescending(definition.ItemIdColumn)
            .TopN(1)
            .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken);
        int upperSubmissionId = upperBoundaryResult.FirstOrDefault()?.ItemID ?? 0;

        int deletedCount = 0;
        DateTime? cursorInserted = null;
        int cursorId = 0;

        while (upperSubmissionId > 0
            && (options.MaximumRecords is null || deletedCount < options.MaximumRecords))
        {
            cancellationToken.ThrowIfCancellationRequested();
            int requestedBatchSize = options.MaximumRecords is int maximum
                ? Math.Min(BatchSize, maximum - deletedCount)
                : BatchSize;

            var query = CreateBatchQuery(
                definition,
                options,
                upperSubmissionId,
                cursorInserted,
                cursorId,
                requestedBatchSize,
                columns);
            var batch = (await query.GetEnumerableTypedResultAsync(cancellationToken: cancellationToken)).ToList();
            if (batch.Count == 0)
            {
                break;
            }

            foreach (var item in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                cursorInserted = item.FormInserted;
                cursorId = item.ItemID;
                if (!DeleteUploadedFiles(item, uploadFieldSourceNames, formId))
                {
                    return deletedCount;
                }

                item.Delete();
                deletedCount++;
            }

            if (batch.Count < requestedBatchSize)
            {
                break;
            }
        }

        return deletedCount;
    }

    /// <summary>
    /// Deletes every uploaded file referenced by <paramref name="item"/>. Returns <see langword="false"/>
    /// without deleting the submission row if any file that exists could not be removed, so a submission
    /// is never reported as deleted while its uploaded file is orphaned on disk (see docs/specs/
    /// form-submission-removal.md's Deletion contract). A file that is already missing is logged and
    /// treated as already cleaned up, since there is nothing left to orphan.
    /// </summary>
    private bool DeleteUploadedFiles(BizFormItem item, IReadOnlyList<string> uploadFieldSourceNames, int formId)
    {
        foreach (string sourceName in uploadFieldSourceNames)
        {
            string systemFileName = UploadedFileName.ExtractSystemFileName(item.GetValue(sourceName));
            if (!TryDeleteUploadedFile(systemFileName, formId, item.ItemID))
            {
                return false;
            }
        }

        return true;
    }

    internal bool TryDeleteUploadedFile(string systemFileName, int formId, int submissionId)
    {
        if (string.IsNullOrWhiteSpace(systemFileName))
        {
            return true;
        }

        try
        {
            if (fileStore.Exists(systemFileName))
            {
                fileStore.Delete(systemFileName);
            }
            else
            {
                logger.LogWarning(
                    "Uploaded file was not found at the expected path for form {FormId} submission {SubmissionId}.",
                    formId,
                    submissionId);
            }

            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not delete an uploaded file for form {FormId} submission {SubmissionId}. The submission was not deleted.",
                formId,
                submissionId);
            return false;
        }
    }

    private static ObjectQuery<BizFormItem> ApplyRange(ObjectQuery<BizFormItem> query, FormSubmissionRemovalRange range)
    {
        if (range.FromUtc is DateTime fromUtc)
        {
            query.WhereGreaterOrEquals(nameof(BizFormItem.FormInserted), fromUtc);
        }

        if (range.ToExclusiveUtc is DateTime toExclusiveUtc)
        {
            query.WhereLessThan(nameof(BizFormItem.FormInserted), toExclusiveUtc);
        }

        return query;
    }

    private static ObjectQuery<BizFormItem> CreateBatchQuery(
        FormSubmissionExportDefinition definition,
        FormSubmissionRemovalOptions options,
        int upperSubmissionId,
        DateTime? cursorInserted,
        int cursorId,
        int batchSize,
        string[] columns)
    {
        var query = ApplyRange(BizFormItemProvider.GetItems(definition.FormClassName), options.Range)
            .Columns(columns)
            .WhereLessOrEquals(definition.ItemIdColumn, upperSubmissionId)
            .TopN(batchSize);

        if (options.SortDirection == FormSubmissionRemovalSortDirection.Ascending)
        {
            query.OrderByAscending(nameof(BizFormItem.FormInserted), definition.ItemIdColumn);
        }
        else
        {
            query.OrderByDescending(nameof(BizFormItem.FormInserted), definition.ItemIdColumn);
        }

        if (cursorInserted is DateTime inserted)
        {
            if (options.SortDirection == FormSubmissionRemovalSortDirection.Ascending)
            {
                query.Where(outer => outer
                    .WhereGreaterThan(nameof(BizFormItem.FormInserted), inserted)
                    .Or()
                    .Where(inner => inner
                        .WhereEquals(nameof(BizFormItem.FormInserted), inserted)
                        .WhereGreaterThan(definition.ItemIdColumn, cursorId)));
            }
            else
            {
                query.Where(outer => outer
                    .WhereLessThan(nameof(BizFormItem.FormInserted), inserted)
                    .Or()
                    .Where(inner => inner
                        .WhereEquals(nameof(BizFormItem.FormInserted), inserted)
                        .WhereLessThan(definition.ItemIdColumn, cursorId)));
            }
        }

        return query;
    }

    private static IReadOnlyList<string> GetUploadFieldSourceNames(FormSubmissionExportDefinition definition) =>
        definition.Fields
            .Where(field => field.IsUploadedFile)
            .Select(field => field.SourceName)
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
