using Kentico.Xperience.Admin.Base;

namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

internal sealed class FormSubmissionExportSelectedActionComponent
    : ActionComponent<FormSubmissionExportSelectedActionProperties, FormSubmissionExportSelectedActionClientProperties>
{
    public override string ClientComponentName => FormSubmissionExportConstants.SelectedClientComponent;

    protected override Task ConfigureClientProperties(FormSubmissionExportSelectedActionClientProperties clientProperties)
    {
        clientProperties.CurrentViewDownloadUrl = Properties.CurrentViewDownloadUrl;
        clientProperties.Columns = Properties.Columns;
        return base.ConfigureClientProperties(clientProperties);
    }
}

internal sealed class FormSubmissionExportSelectedActionProperties : IActionComponentProperties
{
    public string CurrentViewDownloadUrl { get; init; } = string.Empty;

    public IReadOnlyList<string> Columns { get; init; } = [];
}

internal sealed class FormSubmissionExportSelectedActionClientProperties : IActionComponentClientProperties
{
    public string ComponentName { get; init; } = FormSubmissionExportConstants.SelectedClientComponent;

    public string CurrentViewDownloadUrl { get; set; } = string.Empty;

    public IReadOnlyList<string> Columns { get; set; } = [];
}
