using HLSProxy.ApiService.Controllers;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Cache;
using System.Web;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add service defaults & Aspire components.
        builder.AddServiceDefaults();

        // Add services to the container.
        builder.Services.AddProblemDetails();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // Register dependencies
        builder.Services.AddSingleton<ITopLevelManifestRetriever, TopLevelManifestRetriever>();
        builder.Services.AddSingleton<ITokenManifestInjector, TokenManifestInjector>();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        app.UseExceptionHandler();

        // Endpoint to load the manifest with a token
        app.MapGet("/api/app/manifest/manifestload", async (HttpRequest request, string playbackUrl, string webtoken, ITopLevelManifestRetriever topLevelManifestRetriever) =>
        {
            if (string.IsNullOrEmpty(playbackUrl) || string.IsNullOrEmpty(webtoken))
                return Results.BadRequest("playbackUrl or webtoken cannot be empty");

            if (playbackUrl.Contains("&", StringComparison.OrdinalIgnoreCase))
            {
                playbackUrl = playbackUrl.Remove(playbackUrl.IndexOf("&"));
            }

            var token = webtoken;
            var modifiedTopLeveLManifest = topLevelManifestRetriever.GetTopLevelManifestForToken(GetManifestProxyUrl(request), playbackUrl, token);
            var response = new ContentResult
            {
                Content = modifiedTopLeveLManifest,
                ContentType = @"application/vnd.apple.mpegurl"
            };
            request.HttpContext.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            request.HttpContext.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            request.HttpContext.Response.Headers.Add("Cache-Control", "max-age=259200");

            return Results.Content(response.Content, response.ContentType);
        });

        // Endpoint to proxy the manifest and inject a token
        app.MapGet("/api/app/manifest/manifestproxy", async (string playbackUrl, string token, ITokenManifestInjector tokenManifestInjector) =>
        {
            var collection = HttpUtility.ParseQueryString(token);
            var authToken = collection[0];
            var armouredAuthToken = HttpUtility.UrlEncode(authToken);

            var httpRequest = (HttpWebRequest)WebRequest.Create(new Uri(playbackUrl));
            httpRequest.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);
            httpRequest.Timeout = 30000;
            var httpResponse = httpRequest.GetResponse();

            var response = new ContentResult();
            try
            {
                var stream = httpResponse.GetResponseStream();
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        var content = reader.ReadToEnd();
                        response.Content = tokenManifestInjector.InjectTokenToManifestChunks(playbackUrl, armouredAuthToken, content);
                    }
                }
            }
            finally
            {
                httpResponse.Close();
            }
            return Results.Content(response.Content, "application/vnd.apple.mpegurl");
        });

        app.MapDefaultEndpoints();

        app.Run();

        static string GetManifestProxyUrl(HttpRequest request)
        {
            var hostPortion = request.Host.Host;
            var port = request.Host.Port.GetValueOrDefault(80);
            var manifestProxyUrl = string.Format("http://{0}:{1}/api/app/manifest/manifestproxy", hostPortion, port);

            return manifestProxyUrl;
        }
    }
}
