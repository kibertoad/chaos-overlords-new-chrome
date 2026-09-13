using System.Net.Http.Headers;
using System.Text.Json;

namespace Rechaos.Multiplayer.Http;

/// <summary>The official coordination service and its lightweight availability check.</summary>
public static class MultiplayerServiceEndpoint
{
    /// <summary>The public service shipped as the convenient online-play choice.</summary>
    public static Uri Central { get; } = new("https://chaos-overlords.dinorefurb.com");

    /// <summary>Long enough for a cold edge deployment without holding the Online screen up.</summary>
    public static readonly TimeSpan DefaultProbeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Answers whether the server's unversioned health endpoint responds with its expected payload.
    /// </summary>
    public static async Task<bool> IsHealthyAsync(
        HttpClient http,
        Uri baseAddress,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(baseAddress);

        var root = baseAddress.AbsolutePath.EndsWith('/')
            ? baseAddress
            : new Uri(baseAddress, $"{baseAddress.AbsolutePath}/");
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(root, "health"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout ?? DefaultProbeTimeout);
        try
        {
            using var response = await http.SendAsync(
                    request, HttpCompletionOption.ResponseContentRead, deadline.Token)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return false;
            var payload = await response.Content.ReadAsStringAsync(deadline.Token).ConfigureAwait(false);
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("ok", out var ok)
                && ok.ValueKind == JsonValueKind.True;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
