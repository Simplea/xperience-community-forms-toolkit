using CMS.DataEngine;
using CMS.FormEngine;
using CMS.OnlineForms;

using Microsoft.Extensions.Logging;

namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

public interface IFormSubmissionExportService
{
    public Task<FormSubmissionExportDefinition> GetDefinitionAsync(int formId, CancellationToken cancellationToken);

    public Task<PreparedFormSubmissionExport> PrepareAsync(
        int formId,
        FormSubmissionExportCommandRequest request,
        CancellationToken cancellationToken);

    public Task<PreparedFormSubmissionExport> PrepareAsync(
        int formId,
        FormSubmissionExportOptions options,
        CancellationToken cancellationToken);

    public Task<PreparedFormSubmissionExport> PrepareCurrentViewAsync(
        int formId,
        FormSubmissionCurrentViewExportRequest request,
        CancellationToken cancellationToken);

    public Task<int> WriteAsync(
        PreparedFormSubmissionExport export,
        Stream output,
        CancellationToken cancellationToken);
}

internal sealed class FormSubmissionExportService(
    IInfoProvider<BizFormInfo> formProvider,
    IEnumerable<IFormSubmissionExportWriter> writers,
    IFormSubmissionExportValueFormatter valueFormatter,
    TimeProvider timeProvider,
    ILogger<FormSubmissionExportService> logger) : IFormSubmissionExportService
{
    private const int BatchSize = 1000;
    private readonly IReadOnlyDictionary<FormSubmissionExportFormat, IFormSubmissionExportWriter> writersByFormat =
        writers.ToDictionary(writer => writer.Format);

    public async Task<FormSubmissionExportDefinition> GetDefinitionAsync(int formId, CancellationToken cancellationToken)
    {
        if (formId <= 0)
        {
            throw new FormSubmissionExportValidationException("The form identifier is invalid.");
        }

        var form = await formProvider.GetAsync(formId, cancellationToken) ?? throw new FormSubmissionExportNotFoundException();
        var dataClass = DataClassInfoProvider.GetDataClassInfo(form.FormClassID) ?? throw new FormSubmissionExportNotFoundException();
        var formInfo = FormHelper.GetFormInfo(dataClass.ClassName, clone: false, fallbackToDefault: true, onlyVisible: false)
            ?? throw new FormSubmissionExportNotFoundException();

        var primaryKeyField = formInfo
            .GetFields(visible: true, invisible: true, includeSystem: true, onlyPrimaryKeys: true, includeDummyFields: false)
            .SingleOrDefault() ?? throw new FormSubmissionExportNotFoundException();

        var fields = new List<FormSubmissionExportField>
        {
            new(
                FormSubmissionExportFieldIdentifiers.SubmissionId,
                primaryKeyField.Name,
                "Submission ID",
                false,
                FormSubmissionExportFieldKind.SubmissionId,
                false),
            new(
                FormSubmissionExportFieldIdentifiers.Submitted,
                nameof(BizFormItem.FormInserted),
                "Submitted",
                false,
                FormSubmissionExportFieldKind.Submitted,
                true),
        };

        fields.AddRange(formInfo
            .GetFields(visible: true, invisible: true, includeSystem: false, onlyPrimaryKeys: false, includeDummyFields: false)
            .Select(field =>
            {
                var context = new FormFieldExtractionContext { FormFieldInfo = field };
                string componentIdentifier = context.GetFormComponentIdentifier();
                return new FormSubmissionExportField(
                    field.Name,
                    field.Name,
                    string.IsNullOrWhiteSpace(field.Caption) ? field.Name : field.Caption,
                    IsUploadedFileField(field.DataType, componentIdentifier),
                    FormSubmissionExportFieldKind.FormField,
                    DataTypeManager.IsString(TypeEnum.Field, field.DataType));
            }));

        return new FormSubmissionExportDefinition(
            formId,
            form.FormName,
            dataClass.ClassName,
            primaryKeyField.Name,
            fields);
    }

    public async Task<PreparedFormSubmissionExport> PrepareAsync(
        int formId,
        FormSubmissionExportCommandRequest request,
        CancellationToken cancellationToken)
    {
        var definition = await GetDefinitionAsync(formId, cancellationToken);
        var options = FormSubmissionExportOptionsParser.Parse(request, definition.Fields);
        return await PrepareAsync(definition, options, cancellationToken);
    }

    public async Task<PreparedFormSubmissionExport> PrepareAsync(
        int formId,
        FormSubmissionExportOptions options,
        CancellationToken cancellationToken)
    {
        var definition = await GetDefinitionAsync(formId, cancellationToken);
        FormSubmissionExportOptionsParser.ValidateResolved(options, definition.Fields);
        return await PrepareAsync(definition, options, cancellationToken);
    }

    public async Task<PreparedFormSubmissionExport> PrepareCurrentViewAsync(
        int formId,
        FormSubmissionCurrentViewExportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<int> submissionIds = request.SubmissionIds
            ?? throw new FormSubmissionExportValidationException("The current submissions view is invalid.");
        ValidateCurrentViewSubmissionIds(submissionIds);

        var definition = await GetDefinitionAsync(formId, cancellationToken);
        var options = FormSubmissionExportOptionsParser.Parse(
            new FormSubmissionExportCommandRequest(
                request.Format,
                "export",
                null,
                null,
                "UTC",
                null,
                true,
                "comma",
                "ascending",
                request.Columns),
            definition.Fields);

        var listingFields = definition.Fields
            .Where(field => field.VisibleInListing)
            .Select(field => field.Identifier)
            .ToHashSet(StringComparer.Ordinal);
        if (options.ColumnIdentifiers.Any(identifier => !listingFields.Contains(identifier)))
        {
            throw new FormSubmissionExportValidationException("The current submissions columns are invalid.");
        }

        return CreatePreparedExport(definition, options, upperSubmissionId: 0, submissionIds);
    }

    public async Task<int> WriteAsync(
        PreparedFormSubmissionExport export,
        Stream output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(output);

        if (export.CurrentViewSubmissionIds is not null)
        {
            return await WriteCurrentViewAsync(export, output, cancellationToken);
        }

        var formatWriter = GetWriter(export.Options.Format);
        await using var document = await formatWriter.OpenAsync(output, export, cancellationToken);

        int rowCount = 0;
        DateTime? cursorInserted = null;
        int cursorId = 0;

        while (export.UpperSubmissionId > 0
            && (export.Options.EffectiveMaximumRecords is null || rowCount < export.Options.EffectiveMaximumRecords))
        {
            cancellationToken.ThrowIfCancellationRequested();
            int requestedBatchSize = export.Options.EffectiveMaximumRecords is int maximum
                ? Math.Min(BatchSize, maximum - rowCount)
                : BatchSize;

            var query = CreateQuery(export, cursorInserted, cursorId, requestedBatchSize);
            var result = await query.GetEnumerableTypedResultAsync(cancellationToken: cancellationToken);
            var batch = result.ToList();
            if (batch.Count == 0)
            {
                break;
            }

            foreach (var item in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await document.WriteRecordAsync(CreateCells(export, item), cancellationToken);
                cursorInserted = item.FormInserted;
                cursorId = item.ItemID;
                rowCount++;
            }

            if (batch.Count < requestedBatchSize)
            {
                break;
            }
        }

        await document.CompleteAsync(cancellationToken);
        return rowCount;
    }

    private async Task<PreparedFormSubmissionExport> PrepareAsync(
        FormSubmissionExportDefinition definition,
        FormSubmissionExportOptions options,
        CancellationToken cancellationToken)
    {
        FormSubmissionExportOptionsParser.ValidateResolved(options, definition.Fields);
        cancellationToken.ThrowIfCancellationRequested();
        var upperBoundaryResult = await BizFormItemProvider.GetItems(definition.FormClassName)
            .Columns(definition.ItemIdColumn)
            .OrderByDescending(definition.ItemIdColumn)
            .TopN(1)
            .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken);
        int upperSubmissionId = upperBoundaryResult.FirstOrDefault()?.ItemID ?? 0;

        return CreatePreparedExport(definition, options, upperSubmissionId, currentViewSubmissionIds: null);
    }

    private PreparedFormSubmissionExport CreatePreparedExport(
        FormSubmissionExportDefinition definition,
        FormSubmissionExportOptions options,
        int upperSubmissionId,
        IReadOnlyList<int>? currentViewSubmissionIds)
    {
        IReadOnlyList<FormSubmissionExportField> fields = ResolveSelectedFields(
            definition.Fields,
            options.ColumnIdentifiers);

        var writer = GetWriter(options.Format);
        return new PreparedFormSubmissionExport(
            definition,
            fields,
            upperSubmissionId,
            options,
            FormSubmissionExportFileName.Create(
                definition.FormCodeName,
                options.Format,
                options.Operation == FormSubmissionExportOperation.Preview,
                timeProvider.GetUtcNow()),
            writer.ContentType,
            currentViewSubmissionIds);
    }

    private async Task<int> WriteCurrentViewAsync(
        PreparedFormSubmissionExport export,
        Stream output,
        CancellationToken cancellationToken)
    {
        var formatWriter = GetWriter(export.Options.Format);
        await using var document = await formatWriter.OpenAsync(output, export, cancellationToken);
        var itemsById = new Dictionary<int, BizFormItem>();
        var currentViewSubmissionIds = export.CurrentViewSubmissionIds
            ?? throw new InvalidOperationException("Current-view submission identifiers are missing.");

        foreach (var idBatch in currentViewSubmissionIds.Chunk(BatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await BizFormItemProvider.GetItems(export.Definition.FormClassName)
                .Columns(GetQueryColumns(export))
                .WhereIn(export.Definition.ItemIdColumn, idBatch)
                .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken);
            foreach (var item in result)
            {
                itemsById[item.ItemID] = item;
            }
        }

        int rowCount = 0;
        foreach (int submissionId in currentViewSubmissionIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (itemsById.TryGetValue(submissionId, out var item))
            {
                await document.WriteRecordAsync(CreateCells(export, item), cancellationToken);
                rowCount++;
            }
        }

        await document.CompleteAsync(cancellationToken);
        return rowCount;
    }

    private FormSubmissionExportCell[] CreateCells(PreparedFormSubmissionExport export, BizFormItem item)
    {
        var cells = new FormSubmissionExportCell[export.Fields.Count];
        for (int index = 0; index < export.Fields.Count; index++)
        {
            var field = export.Fields[index];
            try
            {
                object? value = GetValue(item, field);
                cells[index] = valueFormatter.Format(value, field.IsUploadedFile);
            }
            catch (Exception exception)
            {
#pragma warning disable S6667 // Submission values must never be captured through exception messages.
                logger.LogWarning(
                    "Could not convert form {FormId} field {FieldName} for submission {SubmissionId}. Exception: {ExceptionType}",
                    export.Definition.FormId,
                    field.Identifier,
                    item.ItemID,
                    exception.GetType().FullName);
#pragma warning restore S6667
                cells[index] = new FormSubmissionExportCell(string.Empty, field.Kind == FormSubmissionExportFieldKind.FormField);
            }
        }

        return cells;
    }

    private IFormSubmissionExportWriter GetWriter(FormSubmissionExportFormat format) =>
        writersByFormat.TryGetValue(format, out var writer)
            ? writer
            : throw new FormSubmissionExportValidationException("The export format is invalid.");

    private static object? GetValue(BizFormItem item, FormSubmissionExportField field) => field.Kind switch
    {
        FormSubmissionExportFieldKind.SubmissionId => item.ItemID,
        FormSubmissionExportFieldKind.Submitted => item.FormInserted,
        FormSubmissionExportFieldKind.FormField when field.IsUploadedFile => UploadedFileName.Extract(item.GetValue(field.SourceName)),
        FormSubmissionExportFieldKind.FormField => item.GetValue(field.SourceName),
        _ => null,
    };

    private static ObjectQuery<BizFormItem> CreateQuery(
        PreparedFormSubmissionExport export,
        DateTime? cursorInserted,
        int cursorId,
        int batchSize)
    {
        var query = BizFormItemProvider.GetItems(export.Definition.FormClassName)
            .Columns(GetQueryColumns(export))
            .WhereLessOrEquals(export.Definition.ItemIdColumn, export.UpperSubmissionId)
            .TopN(batchSize);

        if (export.Options.SortDirection == FormSubmissionExportSortDirection.Ascending)
        {
            query.OrderByAscending(nameof(BizFormItem.FormInserted), export.Definition.ItemIdColumn);
        }
        else
        {
            query.OrderByDescending(nameof(BizFormItem.FormInserted), export.Definition.ItemIdColumn);
        }

        if (export.Options.Range.FromUtc is DateTime fromUtc)
        {
            query.WhereGreaterOrEquals(nameof(BizFormItem.FormInserted), fromUtc);
        }

        if (export.Options.Range.ToExclusiveUtc is DateTime toExclusiveUtc)
        {
            query.WhereLessThan(nameof(BizFormItem.FormInserted), toExclusiveUtc);
        }

        if (cursorInserted is DateTime inserted)
        {
            if (export.Options.SortDirection == FormSubmissionExportSortDirection.Ascending)
            {
                query.Where(outer => outer
                    .WhereGreaterThan(nameof(BizFormItem.FormInserted), inserted)
                    .Or()
                    .Where(inner => inner
                        .WhereEquals(nameof(BizFormItem.FormInserted), inserted)
                        .WhereGreaterThan(export.Definition.ItemIdColumn, cursorId)));
            }
            else
            {
                query.Where(outer => outer
                    .WhereLessThan(nameof(BizFormItem.FormInserted), inserted)
                    .Or()
                    .Where(inner => inner
                        .WhereEquals(nameof(BizFormItem.FormInserted), inserted)
                        .WhereLessThan(export.Definition.ItemIdColumn, cursorId)));
            }
        }

        return query;
    }

    private static string[] GetQueryColumns(PreparedFormSubmissionExport export) => [
        export.Definition.ItemIdColumn,
        nameof(BizFormItem.FormInserted),
        .. export.Fields
            .Where(field => field.Kind == FormSubmissionExportFieldKind.FormField)
            .Select(field => field.SourceName)
            .Distinct(StringComparer.Ordinal),
    ];

    internal static bool IsUploadedFileField(string? dataType, string? componentIdentifier) =>
        string.Equals(dataType, BizFormUploadFile.DATATYPE_FORMFILE, StringComparison.OrdinalIgnoreCase)
        || string.Equals(
            componentIdentifier,
            "Kentico.Forms.Web.Mvc.FileUploaderComponent",
            StringComparison.OrdinalIgnoreCase);

    internal static void ValidateCurrentViewSubmissionIds(IReadOnlyList<int> submissionIds)
    {
        ArgumentNullException.ThrowIfNull(submissionIds);
        if (submissionIds.Any(id => id <= 0)
            || submissionIds.Distinct().Count() != submissionIds.Count)
        {
            throw new FormSubmissionExportValidationException("The current submissions view is invalid.");
        }
    }

    internal static IReadOnlyList<FormSubmissionExportField> ResolveSelectedFields(
        IReadOnlyList<FormSubmissionExportField> availableFields,
        IReadOnlyList<string> selectedIdentifiers)
    {
        var availableByIdentifier = availableFields.ToDictionary(field => field.Identifier, StringComparer.Ordinal);
        return selectedIdentifiers.Select(identifier => availableByIdentifier[identifier]).ToList();
    }
}
