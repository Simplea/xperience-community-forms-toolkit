using System.Text.Json;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

public interface IFormSubmissionExportTokenService
{
    public string Create(FormSubmissionExportTokenPayload payload);

    public bool TryRead(string token, out FormSubmissionExportTokenPayload? payload);
}

internal sealed class FormSubmissionExportTokenService : IFormSubmissionExportTokenService
{
    private readonly IDataProtector protector;

    public FormSubmissionExportTokenService(IDataProtectionProvider provider) => protector = provider.CreateProtector("XperienceCommunity.FormsToolkit.FormSubmissionExport.v2");

    public string Create(FormSubmissionExportTokenPayload payload)
    {
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload);
        return WebEncoders.Base64UrlEncode(protector.Protect(serialized));
    }

    public bool TryRead(string token, out FormSubmissionExportTokenPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            byte[] protectedBytes = WebEncoders.Base64UrlDecode(token);
            payload = JsonSerializer.Deserialize<FormSubmissionExportTokenPayload>(protector.Unprotect(protectedBytes));
            return payload is not null;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }
}
