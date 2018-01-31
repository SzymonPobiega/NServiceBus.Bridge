using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Raw;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

abstract class SubscriptionForwarder
{
    public async Task Forward(MessageContext context, MessageIntentEnum intent, IRawEndpoint dispatcher, InterBridgeRoutingSettings forwarding)
    {
        var messageTypeString = GetSubscriptionMessageTypeFrom(context);

        if (string.IsNullOrEmpty(messageTypeString))
        {
            throw new UnforwardableMessageException("Message intent is Subscribe, but the subscription message type header is missing.");
        }

        if (intent != MessageIntentEnum.Subscribe && intent != MessageIntentEnum.Unsubscribe)
        {
            throw new UnforwardableMessageException("Subscription messages need to have intent set to Subscribe/Unsubscribe.");
        }

        string subscriberAddress;
        string subscriberEndpoint = null;
        string publisherEndpoint;

        context.Headers.TryGetValue("NServiceBus.Bridge.DestinationEndpoint", out publisherEndpoint);

        if (context.Headers.TryGetValue(Headers.SubscriberTransportAddress, out subscriberAddress))
        {
            subscriberEndpoint = context.Headers[Headers.SubscriberEndpoint];
        }
        else
        {
            subscriberAddress = GetReplyToAddress(context);
        }

        if (subscriberAddress == null)
        {
            throw new UnforwardableMessageException("Subscription message arrived without a valid ReplyToAddress.");
        }

        var subscriber = new Subscriber(subscriberAddress, subscriberEndpoint);
        if (intent == MessageIntentEnum.Subscribe)
        {
            await ForwardSubscribe(subscriber, publisherEndpoint, messageTypeString, dispatcher, forwarding).ConfigureAwait(false);
        }
        else
        {
            await ForwardUnsubscribe(subscriber, publisherEndpoint, messageTypeString, dispatcher, forwarding).ConfigureAwait(false);
        }
    }

    static string GetSubscriptionMessageTypeFrom(MessageContext msg)
    {
        msg.Headers.TryGetValue(Headers.SubscriptionMessageType, out var value);
        return value;
    }

    static string GetReplyToAddress(MessageContext message)
    {
        return message.Headers.TryGetValue(Headers.ReplyToAddress, out var replyToAddress) ? replyToAddress : null;
    }

    public abstract Task ForwardSubscribe(Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher, InterBridgeRoutingSettings forwarding);
    public abstract Task ForwardUnsubscribe(Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher, InterBridgeRoutingSettings forwarding);
}