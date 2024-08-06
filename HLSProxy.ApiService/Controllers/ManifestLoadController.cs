using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Cache;
using System.Web;

namespace HLSProxy.ApiService.Controllers
{
    [Area("app")]
    [Route("api/app/manifest")]
    public class ManifestLoadController : Controller
    {
        private const string ManifestProxyUrlTemplate = "http://{0}:{1}/api/app/manifest/manifestproxy";
        private readonly ITopLevelManifestRetriever _topLevelManifestRetriever;
        private readonly ITokenManifestInjector _tokenManifestInjector;

        public ManifestLoadController(ITopLevelManifestRetriever topLevelManifestRetriever, ITokenManifestInjector tokenManifestInjector)
        {
            _topLevelManifestRetriever = topLevelManifestRetriever;
            _tokenManifestInjector = tokenManifestInjector;
        }

        [HttpGet]
        [Route("manifestload")]
        public virtual IActionResult GetLoad(string playbackUrl, string webtoken)
        {
            if (playbackUrl == null || webtoken == null)
                return BadRequest("playbackUrl or webtoken cannot be empty");

            if (playbackUrl.Contains("&", StringComparison.OrdinalIgnoreCase))
            {
                playbackUrl = playbackUrl.Remove(playbackUrl.IndexOf("&"));
            }

            var token = webtoken;
            var modifiedTopLeveLManifest = _topLevelManifestRetriever.GetTopLevelManifestForToken(GetManifestProxyUrl(Request), playbackUrl, token);
            var response = new ContentResult
            {
                Content = modifiedTopLeveLManifest,
                ContentType = @"application/vnd.apple.mpegurl"
            };
            Response.Headers.Add("Access-Control-Allow-Origin", "*");
            Response.Headers.Add("X-Content-Type-Options", "nosniff");
            Response.Headers.Add("Cache-Control", "max-age=259200");

            return response;
        }

        [HttpGet]
        [Produces("application/vnd.apple.mpegurl")]
        [Route("manifestproxy")]
        public IActionResult GetProxy(string playbackUrl, string token)
        {
            var collection = HttpUtility.ParseQueryString(token);
            var authToken = collection[0];
            var armoredAuthToken = HttpUtility.UrlEncode(authToken);

            var httpRequest = (HttpWebRequest)WebRequest.Create(new Uri(playbackUrl));
            httpRequest.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);
            httpRequest.Timeout = 30000;
            var httpResponse = httpRequest.GetResponse();

            //var response = this.Request.ReadFormAsync().Result;
            var response = new ContentResult();
            try
            {
                var stream = httpResponse.GetResponseStream();
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        var content = reader.ReadToEnd();
                        response.Content =
                            _tokenManifestInjector.InjectTokenToManifestChunks(playbackUrl, armoredAuthToken, content);
                    }
                }
            }
            finally
            {
                httpResponse.Close();
            }
            Response.Headers.Add("Access-Control-Allow-Origin", "*");
            return response;
        }

        private static string GetManifestProxyUrl(HttpRequest request)
        {
            var hostPortion = request.Host.Host;
            var port = request.Host.Port.GetValueOrDefault(80);
            var manifestProxyUrl = string.Format(ManifestProxyUrlTemplate, hostPortion, port);

            return manifestProxyUrl;
        }
    }
}
