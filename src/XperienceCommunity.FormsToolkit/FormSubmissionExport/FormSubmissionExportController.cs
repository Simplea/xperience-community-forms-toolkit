using System.Diagnostics;

using Kentico.Membership;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

[ApiController]
[Route(FormSubmissionExportConstants.ControllerRoute)]
[Authorize(AuthenticationSchemes = $"{AdminIdentityConstants.APPLICATION_SCHEME},{AdminIdentityConstants.EXTERNAL_SCHEME}")]
public sealed class FormSubmissionExportController(
    IFormSubmissionExportUserAccessor userAccessor,
    IFormSubmissionExportPermissionEvaluator permissionEvaluator,
    IFormSubmissionExportTokenService tokenService,
    IFormSubmissionExportService exportService,
    IFormSubmissionExportConcurrencyGate concurrencyGate,
    TimeProvider timeProvider,
    ILogger<FormSubmissionExportController> logger) : ControllerBase
{
    [HttpGet("export")]
    public async Task<IActionResult> Download([FromQuery] string? token, CancellationToken cancellationToken)
    {
        if (token is null || !tokenService.TryRead(token, out var payload) || payload is null)
        {
            return BadRequest(new ProblemDetails { Detail = "The export request is invalid or has expired." });
        }

        int userId = await userAccessor.GetUserIdAsync();
        if (payload.UserId != userId)
        {
            return Forbid();
        }

        if (payload.ExpiresUtc < timeProvider.GetUtcNow())
        {
            return BadRequest(new ProblemDetails { Detail = "The export request is invalid or has expired." });
        }

        if (!string.Equals(payload.Permission, FormSubmissionExportConstants.Permission, StringComparison.Ordinal))
        {
            return Forbid();
        }

        if (!await permissionEvaluator.CanExportAsync())
        {
            return Forbid();
        }

        using var lease = concurrencyGate.TryAcquire(userId);
        if (lease is null)
        {
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new ProblemDetails { Detail = "Another export is already running or the export service is busy. Please try again shortly." });
        }

        PreparedFormSubmissionExport export;
        try
        {
            export = await exportService.PrepareAsync(payload.FormId, payload.Options, cancellationToken);
        }
        catch (FormSubmissionExportValidationException exception)
        {
            return BadRequest(new ProblemDetails { Detail = exception.Message });
        }
        catch (FormSubmissionExportNotFoundException)
        {
            return NotFound(new ProblemDetails { Detail = "The requested form was not found." });
        }

        return await StreamExportAsync(export, userId, cancellationToken);
    }

    [HttpPost("current-view/{formId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DownloadCurrentView(
        int formId,
        [FromBody] FormSubmissionCurrentViewExportRequest request,
        CancellationToken cancellationToken)
    {
        if (!await permissionEvaluator.CanExportAsync())
        {
            return Forbid();
        }

        int userId = await userAccessor.GetUserIdAsync();
        using var lease = concurrencyGate.TryAcquire(userId);
        if (lease is null)
        {
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new ProblemDetails { Detail = "Another export is already running or the export service is busy. Please try again shortly." });
        }

        PreparedFormSubmissionExport export;
        try
        {
            export = await exportService.PrepareCurrentViewAsync(formId, request, cancellationToken);
        }
        catch (FormSubmissionExportValidationException exception)
        {
            return BadRequest(new ProblemDetails { Detail = exception.Message });
        }
        catch (FormSubmissionExportNotFoundException)
        {
            return NotFound(new ProblemDetails { Detail = "The requested form was not found." });
        }

        return await StreamExportAsync(export, userId, cancellationToken);
    }

    private async Task<IActionResult> StreamExportAsync(
        PreparedFormSubmissionExport export,
        int userId,
        CancellationToken cancellationToken)
    {
        Response.ContentType = export.ContentType;
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = export.FileName,
        }.ToString();

        var stopwatch = Stopwatch.StartNew();
        try
        {
            int rowCount = await exportService.WriteAsync(export, Response.Body, cancellationToken);
            logger.LogInformation(
                "Exported form {FormId} for user {UserId}. Format: {Format}; Operation: {Operation}; Rows: {RowCount}; MaximumRecords: {MaximumRecords}; FromUtc: {FromUtc}; ToExclusiveUtc: {ToExclusiveUtc}; DurationMs: {DurationMs}",
                export.Definition.FormId,
                userId,
                export.Options.Format,
                export.Options.Operation,
                rowCount,
                export.Options.EffectiveMaximumRecords,
                export.Options.Range.FromUtc,
                export.Options.Range.ToExclusiveUtc,
                stopwatch.ElapsedMilliseconds);
            return new EmptyResult();
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            HttpContext.Abort();
            return new EmptyResult();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Form export failed for form {FormId}, user {UserId}, format {Format}, after {DurationMs} ms.",
                export.Definition.FormId,
                userId,
                export.Options.Format,
                stopwatch.ElapsedMilliseconds);

            if (Response.HasStarted)
            {
                HttpContext.Abort();
                return new EmptyResult();
            }

            return Problem(statusCode: StatusCodes.Status500InternalServerError, detail: "The export could not be completed.");
        }
    }
}
