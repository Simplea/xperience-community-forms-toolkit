using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using XperienceCommunity.FormsToolkit.FormCloning;
using XperienceCommunity.FormsToolkit.FormSubmissionExport;
using XperienceCommunity.FormsToolkit.FormSubmissionRemoval;

namespace XperienceCommunity.FormsToolkit;

public static class FormsToolkitServiceCollectionExtensions
{
    public static IServiceCollection AddFormsToolkit(
        this IServiceCollection services,
        Action<FormsToolkitOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<FormsToolkitOptions>();
        }

        services.AddControllers().AddApplicationPart(typeof(FormSubmissionExportController).Assembly);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IFormSubmissionExportValueFormatter, FormSubmissionExportValueFormatter>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IFormSubmissionExportWriter, CsvExportWriter>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IFormSubmissionExportWriter, ExcelExportWriter>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IFormSubmissionExportWriter, XmlExportWriter>());
        services.TryAddSingleton<IFormSubmissionExportTokenService, FormSubmissionExportTokenService>();
        services.TryAddSingleton<IFormSubmissionExportConcurrencyGate, FormSubmissionExportConcurrencyGate>();
        services.TryAddScoped<IFormSubmissionExportUserAccessor, FormSubmissionExportUserAccessor>();
        services.TryAddScoped<IFormSubmissionExportPermissionEvaluator, FormSubmissionExportPermissionEvaluator>();
        services.TryAddScoped<IFormSubmissionExportService, FormSubmissionExportService>();
        services.TryAddScoped<IFormCloneService, FormCloneService>();
        services.TryAddScoped<IFormSubmissionRemovalService, FormSubmissionRemovalService>();
        return services;
    }
}
