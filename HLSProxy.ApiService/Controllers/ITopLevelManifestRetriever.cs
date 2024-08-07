namespace HLSProxy.ApiService.Controllers;

public interface ITopLevelManifestRetriever
{
    Task<string> GetTopLevelManifestForTokenAsync(string manifestProxyUrl, string topLeveLManifestUrl, string token);
}