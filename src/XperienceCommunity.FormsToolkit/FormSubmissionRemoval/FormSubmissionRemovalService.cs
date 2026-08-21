using CMS.Base;
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
    ILogger<FormSubmissionRemovalService> logger) : IFormSubmissionRemovalService
{
    // Xperience by Kentico's documented default storage location for form file uploads,
    // confirmed against a running instance: <web app physical root>\assets\BizFormFiles.
    // CMS.IO.File.Delete did not resolve the "~/assets/bizformfiles/" virtual form directly
    // (it appears to expect an already-resolved path and silently no-ops otherwise), so the
    // physical path is built explicitly from the web application's physical root instead.
    private static readonly string bizFormFilesPhysicalFolder =
        CMS.IO.Path.Combine(SystemContext.WebApplicationPhysicalPath, "assets", "BizFormFiles");

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
                DeleteUploadedFiles(item, uploadFieldSourceNames, formId);
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
                DeleteUploadedFiles(item, uploadFieldSourceNames, formId);
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

    private void DeleteUploadedFiles(BizFormItem item, IReadOnlyList<string> uploadFieldSourceNames, int formId)
    {
        foreach (string sourceName in uploadFieldSourceNames)
        {
            string systemFileName = UploadedFileName.ExtractSystemFileName(item.GetValue(sourceName));
            if (string.IsNullOrWhiteSpace(systemFileName))
            {
                continue;
            }

            try
            {
                string path = CMS.IO.Path.Combine(bizFormFilesPhysicalFolder, systemFileName);
                if (CMS.IO.File.Exists(path))
                {
                    CMS.IO.File.Delete(path);
                }
                else
                {
                    logger.LogWarning(
                        "Uploaded file was not found at the expected path for form {FormId} submission {SubmissionId}.",
                        formId,
                        item.ItemID);
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Could not delete an uploaded file for form {FormId} submission {SubmissionId}.",
                    formId,
                    item.ItemID);
            }
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
