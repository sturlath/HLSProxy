using System.Text.RegularExpressions;

namespace HLSProxy.ApiService.Controllers;

/// <summary>
/// Provides functionality to inject tokens into HLS manifest chunks.
/// </summary>
public class TokenManifestInjector : ITokenManifestInjector
{
    private const string QualityLevelRegex = @"(QualityLevels\(\d+\))";
    private const string FragmentsRegex = @"(Fragments\([\w\d=-]+,[\w\d=-]+\))";
    private const string UrlRegex = @"("")(https?:\/\/[\da-z\.-]+\.[a-z\.]{2,6}[\/\w \.-]*\/?[\?&][^&=]+=[^&=#]*)("")";
    private readonly ILogger<TokenManifestInjector> logger;
    /// <summary>
    /// Initializes a new instance of the <see cref="TokenManifestInjector"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public TokenManifestInjector(ILogger<TokenManifestInjector> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Injects an authentication token into the manifest chunks of an HLS stream.
    /// </summary>
    /// <param name="playbackUrl">The URL of the playback stream.</param>
    /// <param name="armouredAuthToken">The authentication token to be injected.</param>
    /// <param name="content">The content of the manifest file.</param>
    /// <returns>The modified manifest content with the injected token.</returns>
    public string InjectTokenToManifestChunks(string playbackUrl, string armouredAuthToken, string content)
    {
        try
        {
            var baseUrl = string.Concat(playbackUrl.AsSpan(0, playbackUrl.IndexOf(".ism", StringComparison.OrdinalIgnoreCase)), ".ism");
            var newContent = Regex.Replace(content, UrlRegex, m => $"{m.Groups[1].Value}{m.Groups[2].Value}&token={armouredAuthToken}{m.Groups[3].Value}");

            var match = Regex.Match(playbackUrl, QualityLevelRegex);
            if (match.Success)
            {
                var qualityLevel = match.Groups[0].Value;
                newContent = Regex.Replace(newContent, FragmentsRegex, m => $"{baseUrl}/{qualityLevel}/{m.Value}");
            }

            return newContent;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while injecting the token into manifest chunks.");
            throw;
        }
    }
}
