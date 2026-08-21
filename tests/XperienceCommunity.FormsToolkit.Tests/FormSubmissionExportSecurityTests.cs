using Kentico.Membership;
using Kentico.Xperience.Admin.Base;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using XperienceCommunity.FormsToolkit.FormSubmissionExport;

namespace XperienceCommunity.FormsToolkit.Tests;

public class FormSubmissionExportSecurityTests
{
    [Test]
    public void RejectsTamperedProtectedToken()
    {
        var service = new FormSubmissionExportTokenService(new EphemeralDataProtectionProvider());
        string token = service.Create(CreatePayload(userId: 10));

        int tamperIndex = token.Length / 2;
        char replacement = token[tamperIndex] == 'A' ? 'B' : 'A';
        string tampered = token[..tamperIndex] + replacement + token[(tamperIndex + 1)..];

        Assert.That(service.TryRead(tampered, out _), Is.False);
    }

    [Test]
    public async Task DownloadTokenIsBoundToAuthenticatedAdministrator()
    {
        var tokenService = new FormSubmissionExportTokenService(new EphemeralDataProtectionProvider());
        string token = tokenService.Create(CreatePayload(userId: 42));
        var exportService = new NeverCalledExportService();
        var controller = CreateController(99, tokenService, exportService);

        var result = await controller.Download(token, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<ForbidResult>());
            Assert.That(exportService.WasCalled, Is.False);
        });
    }

    [Test]
    public async Task ExpiredTokenIsRejectedBeforeFormDataIsAccessed()
    {
        var tokenService = new FormSubmissionExportTokenService(new EphemeralDataProtectionProvider());
        string token = tokenService.Create(CreatePayload(userId: 42, expiresUtc: DateTimeOffset.UtcNow.AddMinutes(-1)));
        var exportService = new NeverCalledExportService();
        var controller = CreateController(42, tokenService, exportService);

        var result = await controller.Download(token, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(exportService.WasCalled, Is.False);
        });
    }

    [Test]
    public async Task TokenMustCarryTheExportPermissionClaim()
    {
        var tokenService = new FormSubmissionExportTokenService(new EphemeralDataProtectionProvider());
        string token = tokenService.Create(CreatePayload(userId: 42, permission: "Different.Permission"));
        var exportService = new NeverCalledExportService();
        var controller = CreateController(42, tokenService, exportService);

        var result = await controller.Download(token, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<ForbidResult>());
            Assert.That(exportService.WasCalled, Is.False);
        });
    }

    [Test]
    public async Task DownloadRequiresTheCurrentToolkitPermission()
    {
        var tokenService = new FormSubmissionExportTokenService(new EphemeralDataProtectionProvider());
        string token = tokenService.Create(CreatePayload(userId: 42));
        var exportService = new NeverCalledExportService();
        var controller = CreateController(42, tokenService, exportService, permissionSucceeded: false);

        var result = await controller.Download(token, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<ForbidResult>());
            Assert.That(exportService.WasCalled, Is.False);
        });
    }

    [Test]
    public async Task BusyExportServiceReturnsTooManyRequestsBeforeAccessingFormData()
    {
        var tokenService = new FormSubmissionExportTokenService(new EphemeralDataProtectionProvider());
        string token = tokenService.Create(CreatePayload(userId: 42));
        var exportService = new NeverCalledExportService();
        var controller = CreateController(42, tokenService, exportService, new NeverAvailableGate());

        var result = await controller.Download(token, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<ObjectResult>());
            Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(StatusCodes.Status429TooManyRequests));
            Assert.That(exportService.WasCalled, Is.False);
        });
    }

    [Test]
    public async Task CurrentViewExportRequiresTheToolkitPermission()
    {
        var tokenService = new FormSubmissionExportTokenService(new EphemeralDataProtectionProvider());
        var exportService = new NeverCalledExportService();
        var controller = CreateController(42, tokenService, exportService, permissionSucceeded: false);

        var result = await controller.DownloadCurrentView(
            7,
            new FormSubmissionCurrentViewExportRequest("csv", [1], [FormSubmissionExportFieldIdentifiers.Submitted]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<ForbidResult>());
            Assert.That(exportService.WasCalled, Is.False);
        });
    }

    [Test]
    public void PreviewLimitIsPartOfTheProtectedPayload()
    {
        var tokenService = new FormSubmissionExportTokenService(new EphemeralDataProtectionProvider());
        var payload = CreatePayload(userId: 42, preview: true);
        string token = tokenService.Create(payload);

        Assert.That(tokenService.TryRead(token, out var decoded), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(decoded!.Options.Operation, Is.EqualTo(FormSubmissionExportOperation.Preview));
            Assert.That(decoded.Options.EffectiveMaximumRecords, Is.EqualTo(100));
        });
    }

    [Test]
    public void SecurityAttributesDeclareBothAdminSchemesAndExportPermission()
    {
        var authorize = typeof(FormSubmissionExportController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();
        var currentViewMethod = typeof(FormSubmissionExportController)
            .GetMethod(nameof(FormSubmissionExportController.DownloadCurrentView))!;

        Assert.Multiple(() =>
        {
            Assert.That(authorize.AuthenticationSchemes, Does.Contain(AdminIdentityConstants.APPLICATION_SCHEME));
            Assert.That(authorize.AuthenticationSchemes, Does.Contain(AdminIdentityConstants.EXTERNAL_SCHEME));
            Assert.That(
                typeof(FormSubmissionsPageExtender).GetCustomAttributes(typeof(UIPermissionAttribute), inherit: true),
                Has.Length.EqualTo(1));
            Assert.That(
                currentViewMethod.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true),
                Has.Length.EqualTo(1));
        });
    }

    private static FormSubmissionExportController CreateController(
        int authenticatedUserId,
        IFormSubmissionExportTokenService tokenService,
        IFormSubmissionExportService exportService,
        IFormSubmissionExportConcurrencyGate? concurrencyGate = null,
        bool permissionSucceeded = true) =>
        new(
            new StubUserAccessor(authenticatedUserId),
            new StubPermissionEvaluator(permissionSucceeded),
            tokenService,
            exportService,
            concurrencyGate ?? new AlwaysAvailableGate(),
            TimeProvider.System,
            NullLogger<FormSubmissionExportController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    private static FormSubmissionExportTokenPayload CreatePayload(
        int userId,
        string permission = FormSubmissionExportConstants.Permission,
        bool preview = false,
        DateTimeOffset? expiresUtc = null)
    {
        var options = new FormSubmissionExportOptions(
            FormSubmissionExportFormat.Csv,
            preview ? FormSubmissionExportOperation.Preview : FormSubmissionExportOperation.Export,
            new FormSubmissionExportRange(null, null, null, null),
            null,
            preview ? 100 : null,
            true,
            ',',
            FormSubmissionExportSortDirection.Ascending,
            [FormSubmissionExportFieldIdentifiers.SubmissionId]);
        return new FormSubmissionExportTokenPayload(
            userId,
            7,
            permission,
            options,
            expiresUtc ?? DateTimeOffset.UtcNow.AddMinutes(2));
    }

    private sealed class StubUserAccessor(int userId) : IFormSubmissionExportUserAccessor
    {
        public Task<int> GetUserIdAsync() => Task.FromResult(userId);
    }

    private sealed class StubPermissionEvaluator(bool succeeded) : IFormSubmissionExportPermissionEvaluator
    {
        public Task<bool> CanExportAsync() => Task.FromResult(succeeded);
    }

    private sealed class AlwaysAvailableGate : IFormSubmissionExportConcurrencyGate
    {
        public IDisposable TryAcquire(int userId) => new StubLease();

        private sealed class StubLease : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class NeverAvailableGate : IFormSubmissionExportConcurrencyGate
    {
        public IDisposable? TryAcquire(int userId) => null;
    }

    private sealed class NeverCalledExportService : IFormSubmissionExportService
    {
        public bool WasCalled { get; private set; }

        public Task<FormSubmissionExportDefinition> GetDefinitionAsync(int formId, CancellationToken cancellationToken) => Fail<FormSubmissionExportDefinition>();

        public Task<bool> HasAnySubmissionsAsync(int formId, CancellationToken cancellationToken) => Fail<bool>();

        public Task<PreparedFormSubmissionExport> PrepareAsync(int formId, FormSubmissionExportCommandRequest request, CancellationToken cancellationToken) => Fail<PreparedFormSubmissionExport>();

        public Task<PreparedFormSubmissionExport> PrepareAsync(int formId, FormSubmissionExportOptions options, CancellationToken cancellationToken) => Fail<PreparedFormSubmissionExport>();

        public Task<PreparedFormSubmissionExport> PrepareCurrentViewAsync(int formId, FormSubmissionCurrentViewExportRequest request, CancellationToken cancellationToken) => Fail<PreparedFormSubmissionExport>();

        public Task<int> WriteAsync(PreparedFormSubmissionExport export, Stream output, CancellationToken cancellationToken) => Fail<int>();

        private Task<T> Fail<T>()
        {
            WasCalled = true;
            throw new AssertionException("The export service must not run for an invalid token.");
        }
    }
}
