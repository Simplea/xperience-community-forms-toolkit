namespace XperienceCommunity.FormsToolkit.FormCloning;

public sealed class FormCloneDefaultsResponse
{
    public string? DisplayName { get; init; }

    public string? Error { get; init; }
}

public sealed class FormCloneCommandRequest
{
    public int SourceFormId { get; init; }

    public string DisplayName { get; init; } = string.Empty;
}

public sealed class FormCloneCommandResponse
{
    public int? ClonedFormId { get; init; }

    public string? Error { get; init; }
}

public sealed record FormCloneResult(int FormId, string DisplayName);
