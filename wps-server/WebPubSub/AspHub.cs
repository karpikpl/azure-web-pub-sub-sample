using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Azure.WebPubSub.AspNetCore;
using Microsoft.Azure.WebPubSub.Common;
using Microsoft.Identity.Web.Resource;

namespace WpsServer.WebPubSub;

// [Authorize]
// [RequiredScopeOrAppPermission(AcceptedScope = new[] { "e4cb7572-1af6-4329-8f7c-234857704aba" }, AcceptedAppPermission = new[] { "user_impersonation" })]
public class AspHub : WebPubSubHub
{
    private readonly ILogger<AspHub> _logger;
    private readonly IConfiguration _configuration;
    private readonly Azure.Messaging.WebPubSub.WebPubSubServiceClient _webPubSubServiceClient;

    public AspHub(ILogger<AspHub> logger, IConfiguration configuration, Azure.Messaging.WebPubSub.WebPubSubServiceClient webPubSubServiceClient)
    {
        _logger = logger;
        _configuration = configuration;
        _webPubSubServiceClient = webPubSubServiceClient;
    }


    public override ValueTask<ConnectEventResponse> OnConnectAsync(ConnectEventRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("OnConnectAsync userId:{UserId}", request.ConnectionContext.UserId);
        return base.OnConnectAsync(request, cancellationToken);
    }

    public override Task OnDisconnectedAsync(DisconnectedEventRequest request)
    {
        _logger.LogInformation("OnDisconnectedAsync reason:{Reason} userId:{UserId}", request.Reason, request.ConnectionContext.UserId);
        return base.OnDisconnectedAsync(request);
    }

    public override Task OnConnectedAsync(ConnectedEventRequest request)
    {
        _logger.LogInformation("OnConnectedAsync userId:{UserId}", request.ConnectionContext.UserId);
        return base.OnConnectedAsync(request);
    }

    public override async ValueTask<UserEventResponse> OnMessageReceivedAsync(UserEventRequest request, CancellationToken cancellationToken)
    {
        var eventName = request.ConnectionContext.EventName;
        var userId = request.ConnectionContext.UserId;
        UserEventResponse? response = null;
        Exception? exception = null;

        try
        {
            if (request.DataType == WebPubSubDataType.Text)
            {
                var data = request.DataType == WebPubSubDataType.Text ? request.Data.ToString() : string.Empty;
                _logger.LogInformation("OnMessageReceivedAsync WebPubSubDataType.Text eventName:{EventName}, userId:{UserId} message:{Message}", eventName, userId, data);
                response = request.CreateResponse($"Hello user {userId} from server. Got your event {eventName} with data {data}");
            }
            else
            {
                _logger.LogInformation("OnMessageReceivedAsync WebPubSubDataType.{type} eventName:{EventName}, userId:{UserId} message:{Message}",request.DataType, eventName, userId, request.Data.ToString());
                var msg = request.Data.ToObjectFromJson() as Dictionary<string, object>;
                if (msg != null && msg.TryGetValue("userId", out object? eventUserId) && msg.TryGetValue("message", out object? eventMessage) && eventUserId != null && eventMessage != null)
                {
                    var ack = await _webPubSubServiceClient.SendToUserAsync(eventUserId.ToString(), $"Message from user {userId}: {eventMessage}");
                    _logger.LogInformation("SendToUserAsync userId:{UserId} message:{Message} status:{Status}", eventMessage, eventMessage, ack.Status);
                    response = request.CreateResponse($"Hello user {userId} from server. Your message was sent to user {eventUserId} with status {ack.Status} ack: {ack.Content} requestId: {ack.ClientRequestId}");
                }
            }
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        return response ?? request.CreateResponse($"Hello user {userId} from server. Something went wrong: {exception?.Message}");
    }
}
