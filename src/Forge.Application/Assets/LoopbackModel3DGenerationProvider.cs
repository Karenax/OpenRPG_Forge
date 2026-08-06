using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed class LoopbackModel3DGenerationProvider : IModel3DGenerationProvider
{
    public const string NetworkDeniedCode = "assistant.model3d.network_denied";
    public const string CredentialMissingCode = "assistant.model3d.credential_missing";
    public const string RequestFailedCode = "assistant.model3d.request_failed";
    public const string ResponseInvalidCode = "assistant.model3d.response_invalid";
    private readonly Model3DProviderDefinition _definition;
    private readonly HttpClient _httpClient;
    private readonly Func<string, string?> _readEnvironmentVariable;

    public LoopbackModel3DGenerationProvider(
        Model3DProviderDefinition definition,
        HttpClient httpClient,
        Func<string, string?> readEnvironmentVariable)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _readEnvironmentVariable = readEnvironmentVariable ?? throw new ArgumentNullException(nameof(readEnvironmentVariable));
        var diagnostic = Model3DGenerationValidator.ValidateProvider(definition).FirstOrDefault();
        if (diagnostic is not null || definition.Kind == Model3DProviderKind.DeterministicOffline)
            throw new ArgumentException(diagnostic?.Message ?? "Loopback provider kind is required.", nameof(definition));
    }

    public async Task<Model3DProviderResponse> GenerateAsync(
        Model3DProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.NetworkAuthorized)
            throw new Model3DProviderException(NetworkDeniedCode, "Local provider access requires explicit network authorization.");

        var endpoint = new Uri(_definition.Endpoint!, UriKind.Absolute);
        var payload = new
        {
            schemaVersion = 1,
            requestId = request.RequestId,
            provider = _definition.Kind.ToString(),
            inputKind = request.InputKind.ToString(),
            prompt = request.Prompt,
            imageBase64 = request.ImageContent is null ? null : Convert.ToBase64String(request.ImageContent),
            imageSha256 = request.ImageSha256,
            seed = request.Seed,
            maximumTriangles = request.MaximumTriangles,
        };
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        message.Headers.UserAgent.ParseAdd("OpenRPG-Forge-3D-Provider/1.0");
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (_definition.CredentialEnvironmentVariable is not null)
        {
            var secret = _readEnvironmentVariable(_definition.CredentialEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(secret))
                throw new Model3DProviderException(CredentialMissingCode,
                    $"Credential environment variable '{_definition.CredentialEnvironmentVariable}' is not set.");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_definition.TimeoutSeconds));
        try
        {
            using var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            if (response.RequestMessage?.RequestUri is not { IsLoopback: true })
                throw new Model3DProviderException(NetworkDeniedCode, "Provider redirect escaped the loopback interface.");
            if (response.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices)
                throw new Model3DProviderException(RequestFailedCode,
                    $"3D provider returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
            if (response.Content.Headers.ContentLength > _definition.MaximumArtifactBytes * 2)
                throw new Model3DProviderException(ResponseInvalidCode, "3D provider response exceeds its configured limit.");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[64 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > _definition.MaximumArtifactBytes * 2)
                    throw new Model3DProviderException(ResponseInvalidCode, "3D provider response exceeds its configured limit.");
                buffer.Write(chunk, 0, read);
            }
            using var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
            var root = document.RootElement;
            var encoded = RequiredString(root, "objBase64");
            byte[] obj;
            try { obj = Convert.FromBase64String(encoded); }
            catch (FormatException exception)
            {
                throw new Model3DProviderException(ResponseInvalidCode, "3D provider OBJ payload is not valid base64.", exception);
            }
            var license = RequiredString(root, "licenseSpdx");
            var provenance = RequiredString(root, "provenance");
            var triangles = root.TryGetProperty("triangleCount", out var count) && count.TryGetInt32(out var value) ? value : 0;
            if (obj.Length is < 64 || obj.Length > _definition.MaximumArtifactBytes || triangles is < 4 ||
                triangles > request.MaximumTriangles || !_definition.AllowedLicenses.Contains(license) || license != "CC0-1.0" ||
                !Encoding.UTF8.GetString(obj.AsSpan(0, Math.Min(obj.Length, 256))).Contains("v ", StringComparison.Ordinal))
                throw new Model3DProviderException(ResponseInvalidCode, "3D provider output failed size, topology, license or OBJ validation.");
            return new(_definition.Key, obj, triangles, license, provenance, 0m);
        }
        catch (Model3DProviderException) { throw; }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new Model3DProviderException(RequestFailedCode, "3D provider request timed out.", exception);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            throw new Model3DProviderException(ResponseInvalidCode, "3D provider returned an invalid response.", exception);
        }
    }

    private static string RequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
            throw new Model3DProviderException(ResponseInvalidCode, $"3D provider response is missing '{name}'.");
        return value.GetString()!;
    }
}
