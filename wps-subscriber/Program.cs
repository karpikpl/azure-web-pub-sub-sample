using System;
using System.Threading.Tasks;

using Azure.Messaging.WebPubSub;

using Websocket.Client;

namespace subscriber
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var connectionString = "Endpoint=https://webpubsub-r2mfegit63lgk.webpubsub.azure.com;AccessKey=DdrHVOxJJc0n33k7hKVwCkqHSVy4AJxyWW40xjTF83NNICIYpqoHJQQJ99AJACHYHv6XJ3w3AAAAAWPSTCts;Version=1.0;";
            var hub = "asp";

            // Either generate the URL or fetch it from server or fetch a temp one from the portal
            var serviceClient = new WebPubSubServiceClient(connectionString, hub);
            var url = serviceClient.GetClientAccessUri();

            using (var client = new WebsocketClient(url))
            {
                // Disable the auto disconnect and reconnect because the sample would like the client to stay online even no data comes in
                client.ReconnectTimeout = null;
                client.MessageReceived.Subscribe(msg => Console.WriteLine($"Message received: {msg}"));
                await client.Start();
                Console.WriteLine("Connected.");
                Console.Read();
            }
        }
    }
}