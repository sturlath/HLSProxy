using HLSProxy.ApiService.Controllers;
using Microsoft.AspNetCore.Mvc;
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
            var modifiedTopLeveLManifest = await Task.Run(() => topLevelManifestRetriever.GetTopLevelManifestForTokenAsync(GetManifestProxyUrl(request), playbackUrl, token));
            var response = new ContentResult
            {
                Content = modifiedTopLeveLManifest,
                ContentType = @"application/vnd.apple.mpegurl"
            };
            request.HttpContext.Response.Headers.Append("Access-Control-Allow-Origin", "*");
            request.HttpContext.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            request.HttpContext.Response.Headers.Append("Cache-Control", "max-age=259200");

            return Results.Content(response.Content, response.ContentType);
        });

        // Endpoint to proxy the manifest and inject a token
        app.MapGet("/api/app/manifest/manifestproxy", async (HttpRequest request, string playbackUrl, string token, ITokenManifestInjector tokenManifestInjector, ILogger<Program> logger) =>
        {
            logger.LogInformation("GetProxy called with playbackUrl: {PlaybackUrl} and token: {Token}", playbackUrl, token);

            try
            {
                var collection = HttpUtility.ParseQueryString(token);
                var authToken = collection[0];
                if (authToken == null)
                {
                    return Results.BadRequest("Invalid token");
                }
                var armouredAuthToken = HttpUtility.UrlEncode(authToken);

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true
                };
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                var httpResponse = await httpClient.GetAsync(playbackUrl);
                httpResponse.EnsureSuccessStatusCode();

                var response = new ContentResult();
                var content = await httpResponse.Content.ReadAsStringAsync();
                response.Content = tokenManifestInjector.InjectTokenToManifestChunks(playbackUrl, armouredAuthToken, content);

                request.HttpContext.Response.Headers.Append("Access-Control-Allow-Origin", "*");

                logger.LogInformation("Proxy manifest successfully retrieved and modified");
                return Results.Content(response.Content, "application/vnd.apple.mpegurl");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while retrieving or modifying the proxy manifest");
                throw;
            }
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
