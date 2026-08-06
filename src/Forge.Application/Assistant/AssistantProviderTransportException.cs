namespace Forge.Application.Assistant;

public sealed class AssistantProviderTransportException : Exception
{
    public string Code { get; }

    public AssistantProviderTransportException(string code, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code.Trim();
    }

    public AssistantProviderTransportException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code.Trim();
    }
}
