using System;
using System.Threading.Tasks;
using Azure.Messaging.WebPubSub;

namespace publisher
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var connectionString = "Endpoint=https://webpubsub-r2mfegit63lgk.webpubsub.azure.com;AccessKey=DdrHVOxJJc0n33k7hKVwCkqHSVy4AJxyWW40xjTF83NNICIYpqoHJQQJ99AJACHYHv6XJ3w3AAAAAWPSTCts;Version=1.0;";
            var hub = "asp";
            var message = args[0];

            // Either generate the token or fetch it from server or fetch a temp one from the portal
            var serviceClient = new WebPubSubServiceClient(connectionString, hub);
            await serviceClient.SendToAllAsync(message);
        }
    }
}