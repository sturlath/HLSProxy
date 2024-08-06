namespace HLSProxy.ApiService.Controllers;

public interface ITopLevelManifestRetriever
{
    string GetTopLevelManifestForToken(string manifestProxyUrl, string topLeveLManifestUrl, string token);
}