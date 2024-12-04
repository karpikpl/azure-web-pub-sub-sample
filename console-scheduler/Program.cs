using System.Globalization;
using System.Net.Http.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.WebPubSub.Clients;
using Microsoft.Extensions.Configuration;

var isDoneEvent = new ManualResetEventSlim(false);

ConfigurationBuilder builder = new ConfigurationBuilder();
builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
builder.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
builder.AddEnvironmentVariables();
builder.AddCommandLine(args);

var configuration = builder.Build();

// Configuration
// Replace with your Service Bus namespace and queue or topic name
string serviceBusConnectionString = configuration["ServiceBus:Namespace"] ?? configuration["ServiceBus:ConnectionString"] ?? throw new InvalidOperationException("ServiceBus:Namespace and ServiceBus:ConnectionString are missing. Provide either of them.");
string queueOrTopicName = configuration["ServiceBus:TopicName"] ?? throw new InvalidOperationException("ServiceBus:TopicName setting is missing.");
string webpubsubServerUrl = configuration["WebPubSub:ServerUrl"] ?? throw new InvalidOperationException("WebPubSub:ServerUrl setting is missing.");
string apiKey = configuration["ApiKey"] ?? throw new InvalidOperationException("ApiKey setting is missing.");

string jobId = $"job-{Environment.UserName}-{DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture)}";
string userid = $"scheduler-{jobId}";

Uri webPubSubServerUri = new Uri($"{webpubsubServerUrl}/negotiate/{userid}/{jobId}");

// get connection string for WebPubSub
using HttpClient httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
var response = await httpClient.GetFromJsonAsync<NegotiateResponse>(webPubSubServerUri);

// Create a ServiceBusClient using DefaultAzureCredential or connection string
var client = serviceBusConnectionString.Contains("SharedAccessKey")
    ? new ServiceBusClient(serviceBusConnectionString)
    : new ServiceBusClient(serviceBusConnectionString, new DefaultAzureCredential());

// Create a WebPubSub client
var webPubSubClient = new WebPubSubClient(new Uri(response!.Url));
await webPubSubClient.StartAsync();
Console.WriteLine($"WebPubSub client started for job {jobId} 🔥");

webPubSubClient.GroupMessageReceived+= (args) =>
{
    if(args.Message.DataType == WebPubSubDataType.Json)
    {
        var jobUpdate = args.Message.Data.ToObjectFromJson<JobUpdate>()
            ?? throw new InvalidOperationException("Failed to deserialize JobUpdate message.");

        Console.WriteLine($"Message received in group {args.Message.Group}: {jobUpdate.Name} - {jobUpdate.Step} - {jobUpdate.Status}");

        if(jobUpdate.Step == "Done" && jobUpdate.Status == "Completed")
        {
            isDoneEvent.Set();
            Console.WriteLine("Job is done. 🔥🔥🔥");
        }
    }
    else
    Console.WriteLine($"Uknown Message received in group {args.Message.Group}: {args.Message.Data.ToString()}");

    return Task.CompletedTask;
};

// Create a sender for the queue or topic
ServiceBusSender sender = client.CreateSender(queueOrTopicName);

var job = new Job("Job 1", jobId, new string[] { "Read all the data", "Build in-memory model", "Train the model", "Evaluate the model", "Gather results", "Send Response", "Done" });
// Create a message to send
ServiceBusMessage message = new ServiceBusMessage(BinaryData.FromObjectAsJson(job));

// Send the message
await sender.SendMessageAsync(message);

// Send the event to WebPubSub
var submittedResponse = await webPubSubClient.SendEventAsync("asp_job_submitted", BinaryData.FromObjectAsJson(job), WebPubSubDataType.Json, fireAndForget: true);

Console.WriteLine("Message sent.");

Console.CancelKeyPress += async (sender, e) => {
    e.Cancel = true; // Prevent the process from terminating immediately
    Console.WriteLine("Are you sure you want to cancel? (y/n)");
    var response = Console.ReadKey(intercept: true).Key;
    if (response == ConsoleKey.Y) {
        // letting the solver know that the job has been cancelled
        await webPubSubClient.SendToGroupAsync(jobId, BinaryData.FromObjectAsJson(new JobUpdate(job.Name, job.CorrelationId, "Cancelled", "Cancelled")), WebPubSubDataType.Json);
        Console.WriteLine("Cancellation requested for the job.");
        isDoneEvent.Set();
    } else {
        Console.WriteLine("Continuing execution.");
    }
};

await Task.Run(() => isDoneEvent.Wait());

Console.WriteLine("Job is done. 👋👋👋");

record Job(string Name, string CorrelationId, string[] Steps);
record JobUpdate(string Name, string CorrelationId, string Step, string Status);
record NegotiateResponse(string Url);