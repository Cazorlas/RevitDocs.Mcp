using System.Net.Http.Headers;

namespace Paper.RevitDocs.Mcp.Http;

public sealed class BoundedHttpClient(HttpClient client, int maxResponseBytes = 2_000_000)
{
    public async Task<string> GetStringAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        return await SendAsync(request, cancellationToken);
    }

    public async Task<string> PostJsonAsync(Uri uri, string json, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        return await SendAsync(request, cancellationToken);
    }

    private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Paper-RevitDocs-MCP", "1.0"));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var finalUri = response.RequestMessage?.RequestUri;
        if (finalUri is not null && request.RequestUri is not null
            && (!finalUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || !finalUri.Host.Equals(request.RequestUri.Host, StringComparison.OrdinalIgnoreCase)))
            throw new HttpRequestException("HTTP redirect changed the approved source host.");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > 0 && response.Content.Headers.ContentLength > maxResponseBytes)
            throw new InvalidDataException($"Response exceeds {maxResponseBytes} bytes.");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0) break;
            if (buffer.Length + read > maxResponseBytes)
                throw new InvalidDataException($"Response exceeds {maxResponseBytes} bytes.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }
}
