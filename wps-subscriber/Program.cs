using System.Net.Http.Json;
using Azure.Messaging.WebPubSub.Clients;

namespace subscriber
{
    record NegotiateResponse(string Url);
    record JobUpdate(string Name, string CorrelationId, string Step, string Status);

    class Program
    {
        static async Task Main(string[] args)
        {
            string userid = "random-user";
            string jobId = "job-pkarpala-2024-11-06-20-03-40";
            string webPubSubServerUrl = Environment.GetEnvironmentVariable("WEBPUBSUB_SERVER_URL")!;
            Uri webPubSubServerUri = new Uri($"{webPubSubServerUrl}/negotiate/{userid}/{jobId}");

            using HttpClient httpClient = new HttpClient();
            var response = await httpClient.GetFromJsonAsync<NegotiateResponse>(webPubSubServerUri);

            // Either generate the URL or fetch it from server or fetch a temp one from the portal
            var client = new WebPubSubClient(new Uri(response!.Url));

            await client.StartAsync();

            client.Connected += (args) =>
            {
                Console.WriteLine("Connected with id: " + args.ConnectionId);
                return Task.CompletedTask;
            };

            client.ServerMessageReceived += (args) =>
            {
                Console.WriteLine("Server message received: " + args.Message);
                return Task.CompletedTask;
            };

            client.Disconnected += (args) =>
            {
                Console.WriteLine("Disconnected with error: " + args.DisconnectedMessage);
                return Task.CompletedTask;
            };

            client.GroupMessageReceived += (args) =>
            {
                if(args.Message.FromUserId == userid)
                {
                    // Skip messages from self
                    return Task.CompletedTask;
                }

                var jobUpdate = args.Message.Data.ToObjectFromJson<JobUpdate>();
                Console.WriteLine("Group message received: " + jobUpdate);

                if(jobUpdate.Status == "Cancelled")
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

                if(string.IsNullOrEmpty(command))
                {                
                    await client.DisposeAsync();
                    break;
                }
                else
                {
                    var jobUpdate = new JobUpdate("?", jobId, command, $"Update on {DateTime.Now} for {command}");
                    await client.SendToGroupAsync(jobId, BinaryData.FromObjectAsJson(jobUpdate), WebPubSubDataType.Json);
                }
            }
        }
    }
}