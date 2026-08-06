using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

internal static class AssistantProviderTransport
{
    internal const string NetworkDeniedCode = "assistant.provider.network_denied";
    internal const string CredentialMissingCode = "assistant.provider.credential_missing";
    internal const string RequestFailedCode = "assistant.provider.request_failed";
    internal const string ResponseInvalidCode = "assistant.provider.response_invalid";
    private const int MaximumResponseBytes = 4 * 1024 * 1024;

    internal static HttpRequestMessage CreateJsonRequest(
        HttpMethod method,
        Uri endpoint,
        object payload,
        AssistantProviderDefinition definition,
        AssistantProviderRequest request,
        Func<string, string?> readEnvironmentVariable)
    {
        if (!request.NetworkAuthorized)
        {
            throw new AssistantProviderTransportException(
                NetworkDeniedCode,
                "Assistant provider network access requires explicit authorization.");
        }
        if (definition.LocalOnly && !endpoint.IsLoopback)
        {
            throw new AssistantProviderTransportException(
                NetworkDeniedCode,
                "A local-only assistant provider must use a loopback endpoint.");
        }

        var message = new HttpRequestMessage(method, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        message.Headers.UserAgent.ParseAdd("OpenRPG-Forge-Assistant/1.0");
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(definition.CredentialEnvironmentVariable))
        {
            var secret = readEnvironmentVariable(definition.CredentialEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(secret))
            {
                message.Dispose();
                throw new AssistantProviderTransportException(
                    CredentialMissingCode,
                    $"Assistant credential environment variable '{definition.CredentialEnvironmentVariable}' is not set.");
            }
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        }
        return message;
    }

    internal static async Task<JsonDocument> SendAsync(
        HttpClient httpClient,
        HttpRequestMessage message,
        TimeSpan timeout,
        bool localOnly,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            using var response = await httpClient.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token).ConfigureAwait(false);
            if (localOnly && response.RequestMessage?.RequestUri is { IsLoopback: false })
            {
                throw new AssistantProviderTransportException(
                    NetworkDeniedCode,
                    "A local-only assistant request was redirected outside the loopback interface.");
            }
            if (response.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices)
            {
                throw new AssistantProviderTransportException(
                    RequestFailedCode,
                    $"Assistant provider returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }
            if (response.Content.Headers.ContentLength > MaximumResponseBytes)
            {
                throw new AssistantProviderTransportException(
                    ResponseInvalidCode,
                    "Assistant provider response exceeds the four MiB limit.");
            }
            await using var responseStream = await response.Content.ReadAsStreamAsync(timeoutSource.Token).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[64 * 1024];
            int read;
            while ((read = await responseStream.ReadAsync(chunk, timeoutSource.Token).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > MaximumResponseBytes)
                {
                    throw new AssistantProviderTransportException(
                        ResponseInvalidCode,
                        "Assistant provider response exceeds the four MiB limit.");
                }
                buffer.Write(chunk, 0, read);
            }
            var bytes = buffer.ToArray();
            if (bytes.Length == 0)
            {
                throw new AssistantProviderTransportException(ResponseInvalidCode, "Assistant provider response is empty.");
            }
            return JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 });
        }
        catch (AssistantProviderTransportException)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AssistantProviderTransportException(
                RequestFailedCode,
                $"Assistant provider request exceeded the {timeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)} second timeout.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new AssistantProviderTransportException(
                RequestFailedCode,
                $"Assistant provider request failed: {exception.Message}",
                exception);
        }
        catch (JsonException exception)
        {
            throw new AssistantProviderTransportException(
                ResponseInvalidCode,
                "Assistant provider returned invalid JSON.",
                exception);
        }
    }

    internal static Uri AppendEndpoint(string endpoint, string suffix)
    {
        var normalized = endpoint.TrimEnd('/');
        if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(normalized, UriKind.Absolute);
        }
        return new Uri(normalized + suffix, UriKind.Absolute);
    }

    internal static decimal EstimateCost(
        AssistantProviderDefinition definition,
        int inputTokens,
        int outputTokens) =>
        decimal.Round(
            inputTokens * definition.InputCostPerMillionTokens / 1_000_000m +
            outputTokens * definition.OutputCostPerMillionTokens / 1_000_000m,
            8,
            MidpointRounding.AwayFromZero);

    internal static string RequiredString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new AssistantProviderTransportException(
                ResponseInvalidCode,
                $"Assistant provider response is missing string property '{propertyName}'.");
        }
        return value.GetString()!;
    }

    internal static int OptionalInt(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var result))
        {
            return 0;
        }
        return Math.Max(0, result);
    }
}
