using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Extensibility;
using NServiceBus.Logging;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
using NServiceBus.Unicast.Transport;



class MessageDrivenSubscriptionForwarder : SubscriptionForwarder
{
    static ILog Logger = LogManager.GetLogger<MessageDrivenSubscriptionForwarder>();

    EndpointInstances endpointInstances;

    public MessageDrivenSubscriptionForwarder(EndpointInstances endpointInstances)
    {
        this.endpointInstances = endpointInstances;
    }

    public override Task ForwardSubscribe(MessageContext context, Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher, InterBridgeRoutingSettings forwarding)
    {
        return Send(context, subscriber, publisherEndpoint, messageType, MessageIntentEnum.Subscribe, dispatcher, forwarding);
    }

    public override Task ForwardUnsubscribe(MessageContext context, Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher, InterBridgeRoutingSettings forwarding)
    {
        return Send(context, subscriber, publisherEndpoint, messageType, MessageIntentEnum.Unsubscribe, dispatcher, forwarding);
    }

    async Task Send(MessageContext context, Subscriber subscriber, string publisherEndpoint, string messageType, MessageIntentEnum intent, IRawEndpoint dispatcher, InterBridgeRoutingSettings forwarding)
    {
        if (publisherEndpoint == null)
        {
            throw new UnforwardableMessageException("Subscription message does not contain the 'NServiceBus.Bridge.DestinationEndpoint' header.");
        }
        var typeFullName = messageType.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).First();

        TransportOperation[] ops;
        if (forwarding.TryGetDestination(context, typeFullName, out var nextHops))
        {
            ops = nextHops
                .SelectMany(h => CreateOperation(subscriber, h, messageType, intent, dispatcher))
                .Select(o => SetDestinationEndpoint(o, publisherEndpoint))
                .ToArray();
        }
        else
        {
            ops = CreateOperation(subscriber, publisherEndpoint, messageType, intent, dispatcher).ToArray();
        }
        await dispatcher.Dispatch(new TransportOperations(ops), new TransportTransaction(), new ContextBag()).ConfigureAwait(false);
    }

    static TransportOperation SetDestinationEndpoint(TransportOperation op, string publisherEndpoint)
    {
        op.Message.Headers["NServiceBus.Bridge.DestinationEndpoint"] = publisherEndpoint;
        return op;
    }

    IEnumerable<TransportOperation> CreateOperation(Subscriber subscriber, string nextHopEndpoint, string messageType, MessageIntentEnum intent, IRawEndpoint dispatcher)
    {
        var subscriptionMessage = ControlMessageFactory.Create(intent);

        subscriptionMessage.Headers[Headers.SubscriptionMessageType] = messageType;
        subscriptionMessage.Headers[Headers.ReplyToAddress] = dispatcher.TransportAddress;
        subscriptionMessage.Headers[Headers.SubscriberTransportAddress] = dispatcher.TransportAddress;
        subscriptionMessage.Headers[Headers.SubscriberEndpoint] = dispatcher.EndpointName;
        subscriptionMessage.Headers[Headers.TimeSent] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow);
        subscriptionMessage.Headers[Headers.NServiceBusVersion] = "6.3.1"; //The code has been copied from 6.3.1

        var publisherInstances = endpointInstances.FindInstances(nextHopEndpoint);
        var publisherAddresses = publisherInstances.Select(i => dispatcher.ToTransportAddress(LogicalAddress.CreateRemoteAddress(i))).ToArray();
        foreach (var publisherAddress in publisherAddresses)
        {
            Logger.Debug(
                $"Sending {intent} request for {messageType} to {publisherAddress} on behalf of {subscriber.TransportAddress}.");

            var transportOperation = new TransportOperation(subscriptionMessage, new UnicastAddressTag(publisherAddress));
            yield return transportOperation;
        }
    }
}
