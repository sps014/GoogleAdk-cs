using Google.Apis.Auth.OAuth2;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using System.Net.Http.Headers;
namespace GoogleAdk.Core.Telemetry;

/// <summary>
/// Extension methods for configuring Google Cloud tracing using OpenTelemetry.
/// </summary>
public static class GoogleCloudTelemetry
{
    /// <summary>
    /// Adds a Google Cloud Trace exporter (via OTLP) to the TracerProviderBuilder.
    /// Uses Application Default Credentials for authentication.
    /// </summary>
    public static TracerProviderBuilder AddGoogleCloudTracing(this TracerProviderBuilder builder)
    {
        builder.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("https://telemetry.googleapis.com/v1/traces");
            options.Protocol = OtlpExportProtocol.HttpProtobuf;

            // Initialize credentials
            var credential = GoogleCredential.GetApplicationDefault()
                .CreateScoped("https://www.googleapis.com/auth/trace.append");

            options.HttpClientFactory = () =>
            {
                var handler = new GoogleAuthHandler(credential, new HttpClientHandler());
                return new HttpClient(handler);
            };
        });

        return builder;
    }
}

public class GoogleAuthHandler : DelegatingHandler
{
    private readonly GoogleCredential _credential;

    public GoogleAuthHandler(GoogleCredential credential, HttpMessageHandler innerHandler) 
        : base(innerHandler)
    {
        _credential = credential;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Obtain OAuth token dynamically for each request
        var token = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync(null, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}
