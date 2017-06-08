using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Raw;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

abstract class SubscribeRouter : IRouter
{
    ISubscriptionStorage subscriptionStorage;

    protected SubscribeRouter(ISubscriptionStorage subscriptionStorage)
    {
        this.subscriptionStorage = subscriptionStorage;
    }

    public async Task Route(MessageContext context, MessageIntentEnum intent, IRawEndpoint dispatcher)
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
            await subscriptionStorage.Subscribe(subscriber, messageType, context.Extensions).ConfigureAwait(false);
            await ForwardSubscribe(subscriber, publisherEndpoint, messageTypeString, dispatcher).ConfigureAwait(false);
        }
        else
        {
            await subscriptionStorage.Unsubscribe(subscriber, messageType, context.Extensions).ConfigureAwait(false);
            await ForwardUnsubscribe(subscriber, publisherEndpoint, messageTypeString, dispatcher).ConfigureAwait(false);
        }
    }

    protected abstract Task ForwardSubscribe(Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher);
    protected abstract Task ForwardUnsubscribe(Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher);

    static string GetSubscriptionMessageTypeFrom(MessageContext msg)
    {
        msg.Headers.TryGetValue(Headers.SubscriptionMessageType, out string value);
        return value;
    }

    static string GetReplyToAddress(MessageContext message)
    {
        return message.Headers.TryGetValue(Headers.ReplyToAddress, out string replyToAddress) ? replyToAddress : null;
    }
}
