namespace XperienceCommunity.FormsToolkit.FormCloning;

public sealed class FormCloneNotFoundException : Exception
{
    public FormCloneNotFoundException()
        : base("The requested form was not found.")
    {
    }
}

public sealed class FormCloneValidationException(string message) : Exception(message);

public sealed class FormCloneOperationException : Exception
{
    public FormCloneOperationException(string message)
        : base(message)
    {
    }

    public FormCloneOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
