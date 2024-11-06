using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.Identity.Web;
using Azure.Messaging.WebPubSub;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
builder.Services.AddAuthorization();

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
        clientBuilder.UseCredential(new DefaultAzureCredential(includeInteractiveCredentials: true));
    }

    var pubsubEndpoint = builder.Configuration.GetValue<string>("WebPubSub:Endpoint");
    var hubName = builder.Configuration.GetValue<string>("WebPubSub:HubName");

    if (string.IsNullOrEmpty(pubsubEndpoint))
        throw new Exception("Web PubSub connection string (WebPubSub:Endpoint) is missing");

    if (string.IsNullOrEmpty(hubName))
        throw new Exception("Web PubSub hub name (WebPubSub:HubName) is missing");

    // using Identity: https://learn.microsoft.com/en-us/azure/azure-web-pubsub/howto-create-serviceclient-with-net-and-azure-identity
    clientBuilder.AddWebPubSubServiceClient(new Uri(pubsubEndpoint), hubName, new DefaultAzureCredential(includeInteractiveCredentials: true));
});

builder.Services.AddHostedService<WpsServer.OtherServices.ServiceBusHostedService>();
builder.Services
    .AddWebPubSub(options =>
    {
        var pubsubEndpoint = builder.Configuration.GetValue<string>("WebPubSub:Endpoint")!;
        options.ServiceEndpoint = new Microsoft.Azure.WebPubSub.AspNetCore.WebPubSubServiceEndpoint(new Uri(pubsubEndpoint), new DefaultAzureCredential(includeInteractiveCredentials: true));
    })
    .AddWebPubSubServiceClient<WpsServer.WebPubSub.AspHub>();

builder.Services.AddApplicationInsightsTelemetry();
builder.Logging.AddApplicationInsights();
builder.Logging.AddFilter<ApplicationInsightsLoggerProvider>("WpsServer", LogLevel.Trace);


// Add services to the container.

var app = builder.Build();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Configure REST API
app.MapWebPubSubHub<WpsServer.WebPubSub.AspHub>("/eventhandler/{*path}");

app.MapGet("/", () => "Hello");

app.MapGet("/negotiate/{userId}", async (string userId, WebPubSubServiceClient client) =>
{
    var uri = await client.GetClientAccessUriAsync(userId, roles: new[] { "webpubsub.sendToGroup.clients", "webpubsub.joinLeaveGroup.clients" }, groups: new[] { "clients" });
    return new { Url = uri.AbsoluteUri };
});

IWebHostEnvironment env = app.Environment;

if (env.IsDevelopment())
{
    Console.WriteLine("Development mode");
    Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
}

app.Run();
