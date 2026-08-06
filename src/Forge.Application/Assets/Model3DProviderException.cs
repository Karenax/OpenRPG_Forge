namespace Forge.Application.Assets;

public sealed class Model3DProviderException : Exception
{
    public Model3DProviderException(string code, string message) : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public Model3DProviderException(string code, string message, Exception innerException) : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
}
