namespace XperienceCommunity.FormsToolkit.FormCloning;

public static class FormCloneName
{
    public static string GetDefault(string sourceDisplayName)
    {
        string normalizedSource = Normalize(sourceDisplayName);
        const string suffix = " (copy)";
        int maximumSourceLength = FormCloneConstants.MaximumDisplayNameLength - suffix.Length;
        if (normalizedSource.Length > maximumSourceLength)
        {
            normalizedSource = normalizedSource[..maximumSourceLength].TrimEnd();
        }

        return normalizedSource + suffix;
    }

    public static string Normalize(string? displayName)
    {
        string normalized = displayName?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new FormCloneValidationException("Form name is required.");
        }

        if (normalized.Length > FormCloneConstants.MaximumDisplayNameLength)
        {
            throw new FormCloneValidationException(
                $"Form name must be {FormCloneConstants.MaximumDisplayNameLength} characters or fewer.");
        }

        return normalized;
    }
}
