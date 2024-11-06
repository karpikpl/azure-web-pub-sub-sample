using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.Identity.Web;
using Azure.Messaging.WebPubSub;

var builder = WebApplication.CreateBuilder(args);

// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//     .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
// builder.Services.AddAuthorization();

var managedIdentityId = builder.Configuration.GetValue<string>("azureClientId");
Azure.Core.TokenCredential credential = managedIdentityId == null
    ? new DefaultAzureCredential(includeInteractiveCredentials: true)
    : new ManagedIdentityCredential(managedIdentityId);

builder.Services.AddAzureClients(clientBuilder =>
{
    var connectionString = builder.Configuration.GetValue<string>("ServiceBus:ConnectionString");

    if (string.IsNullOrEmpty(connectionString))
        throw new Exception("Service Bus connection string (ServiceBus:ConnectionString) is missing");


    if (connectionString.Contains("SharedAccessKey", StringComparison.InvariantCultureIgnoreCase))
    {
        clientBuilder.AddServiceBusClient(connectionString);
    }
    else
    {
        clientBuilder.AddServiceBusClientWithNamespace(connectionString);
        clientBuilder.UseCredential(credential);
    }

    var pubsubEndpoint = builder.Configuration.GetValue<string>("WebPubSub:Endpoint");
    var hubName = builder.Configuration.GetValue<string>("WebPubSub:HubName");

    if (string.IsNullOrEmpty(pubsubEndpoint))
        throw new Exception("Web PubSub connection string (WebPubSub:Endpoint) is missing");

    if (string.IsNullOrEmpty(hubName))
        throw new Exception("Web PubSub hub name (WebPubSub:HubName) is missing");

    // using Identity: https://learn.microsoft.com/en-us/azure/azure-web-pubsub/howto-create-serviceclient-with-net-and-azure-identity
    clientBuilder.AddWebPubSubServiceClient(new Uri(pubsubEndpoint), hubName, credential);
});

builder.Services.AddHostedService<WpsServer.OtherServices.ServiceBusHostedService>();
builder.Services
    .AddWebPubSub(options =>
    {
        var pubsubEndpoint = builder.Configuration.GetValue<string>("WebPubSub:Endpoint")!;
        options.ServiceEndpoint = new Microsoft.Azure.WebPubSub.AspNetCore.WebPubSubServiceEndpoint(new Uri(pubsubEndpoint), credential);
    })
    .AddWebPubSubServiceClient<WpsServer.WebPubSub.AspHub>();

builder.Services.AddApplicationInsightsTelemetry();
builder.Logging.AddApplicationInsights();
builder.Logging.AddFilter<ApplicationInsightsLoggerProvider>("WpsServer", LogLevel.Trace);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin();
    });
});

// Add services to the container.

var app = builder.Build();

app.UseRouting();
app.UseCors();

// app.UseAuthentication();
// app.UseAuthorization();

// Configure REST API
app.MapWebPubSubHub<WpsServer.WebPubSub.AspHub>("/eventhandler/{*path}");

app.MapGet("/", () => "Hello");

app.MapGet("/negotiate/{userId}/{groupName}", async (string userId, string groupName, WebPubSubServiceClient client) =>
{
    var uri = await client.GetClientAccessUriAsync(userId: userId, 
    roles: new[] { $"webpubsub.sendToGroup.{groupName}", $"webpubsub.joinLeaveGroup.{groupName}" }, 
    groups: new[] { groupName }, clientProtocol: WebPubSubClientProtocol.Default);
    return new { Url = uri.AbsoluteUri };
});

// Abuse protection: https://learn.microsoft.com/en-us/azure/azure-web-pubsub/howto-troubleshoot-common-issues#abuseprotectionresponsemissingallowedorigin
// From cloud events: https://github.com/cloudevents/spec/blob/v1.0/http-webhook.md#4-abuse-protection
app.Use(async (context, next) =>
{
    if (context.Request.Method == HttpMethods.Options && context.Request.Headers.ContainsKey("WebHook-Request-Origin"))
    {
        var origin = context.Request.Headers["WebHook-Request-Origin"].First();
        var pubsubEndpoint = builder.Configuration.GetValue<string>("WebPubSub:Endpoint")?.Replace("http://", "").Replace("https://", "");

        if(origin?.Equals(pubsubEndpoint, StringComparison.OrdinalIgnoreCase) == false)
        {
            context.RequestServices.GetService<ILogger<Program>>()!.LogWarning($"Request origin {origin} is not allowed. It doesn't match {pubsubEndpoint}");
            context.Response.StatusCode = 403;
            return;
        }
        
        context.Response.Headers["WebHook-Allowed-Origin"] = "*";
        context.Response.StatusCode = 200;
        return;
    }

    await next();
});

IWebHostEnvironment env = app.Environment;

if (env.IsDevelopment())
{
    Console.WriteLine("Development mode");
    Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
}

app.Run();
