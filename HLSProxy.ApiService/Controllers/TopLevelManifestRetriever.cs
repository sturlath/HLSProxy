using System.Text.RegularExpressions;
using System.Web;

namespace HLSProxy.ApiService.Controllers;

/// <summary>
/// Retrieves and modifies the top-level manifest for a given token.
/// </summary>
public class TopLevelManifestRetriever : ITopLevelManifestRetriever
{
    private readonly ILogger<TopLevelManifestRetriever> logger;

    public TopLevelManifestRetriever(ILogger<TopLevelManifestRetriever> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Retrieves the top-level manifest for a given token and modifies it to include the token.
    /// </summary>
    /// <param name="manifestProxyUrl">The URL of the manifest proxy.</param>
    /// <param name="topLeveLManifestUrl">The URL of the top-level manifest.</param>
    /// <param name="token">The token to be included in the manifest.</param>
    /// <returns>The modified top-level manifest content.</returns>
    public async Task<string> GetTopLevelManifestForTokenAsync(string manifestProxyUrl, string topLeveLManifestUrl, string token)
    {
        logger.LogInformation("GetTopLevelManifestForTokenAsync called with manifestProxyUrl: {ManifestProxyUrl}, topLeveLManifestUrl: {TopLeveLManifestUrl}, token: {Token}", manifestProxyUrl, topLeveLManifestUrl, token);

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true
        };
        httpClient.Timeout = TimeSpan.FromMilliseconds(30000);

        try
        {
            var httpResponse = await httpClient.GetAsync(topLeveLManifestUrl);
            logger.LogInformation("HTTP request to {TopLeveLManifestUrl} completed with status code {StatusCode}", topLeveLManifestUrl, httpResponse.StatusCode);

            if (httpResponse.IsSuccessStatusCode)
            {
                var topLevelManifestContent = await httpResponse.Content.ReadAsStringAsync();
                logger.LogInformation("Top-level manifest content retrieved successfully");

                var uri = new Uri(topLeveLManifestUrl);
                var pathWithoutQuery = uri.AbsolutePath[..uri.AbsolutePath.LastIndexOf('/')];
                var topLevelManifestBaseUrl = $"{uri.Scheme}://{uri.Host}{pathWithoutQuery}/manifest.msi";
                var urlEncodedTopLeveLManifestBaseUrl = HttpUtility.UrlEncode(topLevelManifestBaseUrl);
                var urlEncodedToken = HttpUtility.UrlEncode(token);

                const string uriRegex = @"(URI=""[^""]+"")";
                var newContent = Regex.Replace(topLevelManifestContent,
                    uriRegex,
                    match =>
                    {
                        // Retrieves the original URI from the matched string without the "URI=" part and the quotes at the end.
                        var originalUri = match.Value.Substring(5, match.Value.Length - 6);
                        var encodedUri = HttpUtility.UrlEncode(originalUri);
                        return $"URI=\"{manifestProxyUrl}?playbackUrl={urlEncodedTopLeveLManifestBaseUrl}/{encodedUri}&token={urlEncodedToken}\"";
                    });

                logger.LogInformation("NewContent: {NewContent}", newContent); //TODO: Remove this line after testing

                logger.LogInformation("Top-level manifest content modified successfully");
                return newContent;
            }
            else
            {
                logger.LogWarning("Failed to retrieve top-level manifest content. Status code: {StatusCode}", httpResponse.StatusCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while retrieving or modifying the top-level manifest");
            throw;
        }

        return null;
    }
}