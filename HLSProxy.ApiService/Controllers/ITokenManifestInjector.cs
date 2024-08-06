namespace HLSProxy.ApiService.Controllers;

public interface ITokenManifestInjector
{
    string InjectTokenToManifestChunks(string playbackUrl, string armoredAuthToken, string content);
}