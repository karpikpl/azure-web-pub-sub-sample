using System.Net.Http.Json;
using Azure.Messaging.WebPubSub.Clients;
using Microsoft.Extensions.Configuration;

namespace subscriber
{
    record NegotiateResponse(string Url);
    record JobUpdate(string Name, string CorrelationId, string Step, string Status);
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("----------------------------------------");
            Console.WriteLine("Azure Web PubSub Console Subscriber");
            Console.WriteLine("----------------------------------------");
            Console.WriteLine();
            Console.WriteLine("Startup instructions:");
            Console.WriteLine("  - Ensure you have the necessary configuration files (appsettings.json, appsettings.local.json) with the required settings.");
            Console.WriteLine("  - You can override the UserId and GroupName by passing them as command line arguments:");
            Console.WriteLine("      dotnet run -- WebPubSub:UserId=<your_user_id> WebPubSub:GroupName=<your_group_name>");
            Console.WriteLine("  - Alternatively, set the environment variables 'WebPubSub:UserId' and 'WebPubSub:GroupName'.");
            Console.WriteLine();
            Console.WriteLine("Usage instructions:");
            Console.WriteLine("  - To send a message to the group, type your message and press Enter.");
            Console.WriteLine("  - To send an event, use the format 'e:<eventName> m:<message> u:<userId>' and press Enter.");
            Console.WriteLine("  - To exit, press Enter without typing any message.");
            Console.WriteLine("----------------------------------------");
            Console.WriteLine();
            
            ConfigurationBuilder builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            builder.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
            builder.AddEnvironmentVariables();
            builder.AddCommandLine(args);

            var configuration = builder.Build();

            string userid = configuration["WebPubSub:UserId"] ?? "console-subscriber";
            string groupName = configuration["WebPubSub:GroupName"] ?? "group";

            Console.WriteLine($"Starting subscriber with user id {userid} and group name {groupName}");
            string webPubSubServerUrl = configuration["WebPubSub:ServerUrl"] ?? throw new InvalidOperationException("WebPubSub:ServerUrl setting is missing.");
            string apiKey = configuration["ApiKey"] ?? throw new InvalidOperationException("ApiKey setting is missing.");

            Uri webPubSubServerUri = new Uri($"{webPubSubServerUrl}/negotiate/{userid}/{groupName}");

            using HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            var response = await httpClient.GetFromJsonAsync<NegotiateResponse>(webPubSubServerUri);

            var client = new WebPubSubClient(new Uri(response!.Url));

            await client.StartAsync();

            client.Connected += (args) =>
            {
                Console.WriteLine("Connected with id: " + args.ConnectionId);
                return Task.CompletedTask;
            };

            client.ServerMessageReceived += (args) =>
            {
                Console.WriteLine("Server message received: " + args.Message.Data.ToString());
                return Task.CompletedTask;
            };

            client.Disconnected += (args) =>
            {
                Console.WriteLine("Disconnected with error: " + args.DisconnectedMessage.Reason);
                return Task.CompletedTask;
            };

            client.GroupMessageReceived += (args) =>
            {
                if (args.Message.FromUserId == userid)
                {
                    // Skip messages from self
                    Console.WriteLine("\tSkip message from self");
                    return Task.CompletedTask;
                }

                var jobUpdate = args.Message.Data.ToObjectFromJson<JobUpdate>();
                Console.WriteLine("Group message received: " + jobUpdate);

                if (jobUpdate.Status == "Cancelled")
                {
                    Console.WriteLine("Job cancelled, disconnecting...");
                    client.DisposeAsync();
                    Environment.Exit(0);
                }
                return Task.CompletedTask;
            };

            while (true)
            {
                var command = Console.ReadLine();

                if (string.IsNullOrEmpty(command))
                {
                    await client.DisposeAsync();
                    break;
                }
                else if (command.StartsWith("e:"))
                {
                    var commandData = command.Split(" ");
                    var eventName = commandData.FirstOrDefault(e => e.StartsWith("e:"))?.Substring(2);
                    var eventMessage = commandData.FirstOrDefault(e => e.StartsWith("m:"))?.Substring(2);
                    var eventUserId = commandData.FirstOrDefault(e => e.StartsWith("u:"))?.Substring(2);

                    var ack = await client.SendEventAsync(eventName, BinaryData.FromObjectAsJson(new { userId = eventUserId, message = eventMessage, eventName = eventName }), WebPubSubDataType.Json);
                    Console.WriteLine("Event sent to server with message: {0}, to user: {1}. Ack: {2}", eventMessage, eventUserId, ack.AckId);
                }
                else
                {
                    var jobUpdate = new JobUpdate("?", groupName, command, $"Update on {DateTime.Now} for {command}");
                    var ack = await client.SendToGroupAsync(groupName, BinaryData.FromObjectAsJson(jobUpdate), WebPubSubDataType.Json);
                    Console.WriteLine("Message sent. Ack: {0}", ack.AckId);
                }
            }
        }
    }
}