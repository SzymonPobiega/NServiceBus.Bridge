using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Extensibility;
using NServiceBus.Raw;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;


class SubscribeRouter
{
    public static async Task Route(MessageContext context, MessageIntentEnum intent, IRawEndpoint dispatcher, ISubscriptionForwarder forwarder, ISubscriptionStorage subscriptionStorage)
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

        if (!context.Headers.TryGetValue("NServiceBus.Bridge.DestinationEndpoint", out publisherEndpoint))
        {
            throw new UnforwardableMessageException("Subscription message does not contain the 'NServiceBus.Bridge.DestinationEndpoint' header.");
        }

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
        var messageType = new MessageType(messageTypeString);
        if (intent == MessageIntentEnum.Subscribe)
        {
            await subscriptionStorage.Subscribe(subscriber, messageType, new ContextBag()).ConfigureAwait(false);
            await forwarder.ForwardSubscribe(subscriber, publisherEndpoint, messageTypeString, dispatcher).ConfigureAwait(false);
        }
        else
        {
            await subscriptionStorage.Unsubscribe(subscriber, messageType, new ContextBag()).ConfigureAwait(false);
            await forwarder.ForwardUnsubscribe(subscriber, publisherEndpoint, messageTypeString, dispatcher).ConfigureAwait(false);
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
}
