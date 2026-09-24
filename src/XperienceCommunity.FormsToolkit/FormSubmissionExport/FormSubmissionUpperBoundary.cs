using CMS.OnlineForms;

namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

/// <summary>
/// The highest submission ID an export or deletion may reach. Submission IDs only grow, so capping a
/// query at an ID observed earlier excludes every submission created after that moment.
/// </summary>
internal static class FormSubmissionUpperBoundary
{
    public static async Task<int> GetCurrentAsync(FormSubmissionExportDefinition definition, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await BizFormItemProvider.GetItems(definition.FormClassName)
            .Columns(definition.ItemIdColumn)
            .OrderByDescending(definition.ItemIdColumn)
            .TopN(1)
            .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken);
        return result.FirstOrDefault()?.ItemID ?? 0;
    }

    /// <summary>
    /// Applies a caller-supplied boundary, such as the one a delete preview observed. It can only
    /// narrow the current boundary, never widen it, so a tampered value cannot reach newer submissions.
    /// </summary>
    public static int Resolve(int currentUpperSubmissionId, int? requestedUpperSubmissionId) =>
        requestedUpperSubmissionId is int requested
            ? Math.Min(requested, currentUpperSubmissionId)
            : currentUpperSubmissionId;
}
