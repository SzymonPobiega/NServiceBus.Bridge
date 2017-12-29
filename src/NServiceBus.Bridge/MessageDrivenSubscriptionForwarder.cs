using System;
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



class MessageDrivenSubscriptionForwarder : ISubscriptionForwarder
{
    static ILog Logger = LogManager.GetLogger<MessageDrivenSubscriptionForwarder>();

    EndpointInstances endpointInstances;

    public MessageDrivenSubscriptionForwarder(EndpointInstances endpointInstances)
    {
        this.endpointInstances = endpointInstances;
    }

    public bool RequiresPublisherEndpoint => true;

    public Task ForwardSubscribe(Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher, InterBridgeRoutingSettings forwarding)
    {
        return Send(subscriber, publisherEndpoint, messageType, MessageIntentEnum.Subscribe, dispatcher, forwarding);
    }

    public Task ForwardUnsubscribe(Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher, InterBridgeRoutingSettings forwarding)
    {
        return Send(subscriber, publisherEndpoint, messageType, MessageIntentEnum.Unsubscribe, dispatcher, forwarding);
    }

    async Task Send(Subscriber subscriber, string publisherEndpoint, string messageType, MessageIntentEnum intent, IRawEndpoint dispatcher, InterBridgeRoutingSettings forwarding)
    {
        var subscriptionMessage = ControlMessageFactory.Create(intent);

        subscriptionMessage.Headers[Headers.SubscriptionMessageType] = messageType;
        subscriptionMessage.Headers[Headers.ReplyToAddress] = dispatcher.TransportAddress;
        subscriptionMessage.Headers[Headers.SubscriberTransportAddress] = dispatcher.TransportAddress;
        subscriptionMessage.Headers[Headers.SubscriberEndpoint] = dispatcher.EndpointName;
        subscriptionMessage.Headers[Headers.TimeSent] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow);
        subscriptionMessage.Headers[Headers.NServiceBusVersion] = "6.3.1"; //The code has been copied from 6.3.1

        var typeFullName = messageType.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).First();

        if (forwarding.PublisherTable.TryGetValue(typeFullName, out var nextHopEndpoint))
        {
            subscriptionMessage.Headers["NServiceBus.Bridge.DestinationEndpoint"] = publisherEndpoint;
        }
        else
        {
            nextHopEndpoint = publisherEndpoint;
        }

        var publisherInstances = endpointInstances.FindInstances(nextHopEndpoint);
        var publisherAddresses = publisherInstances.Select(i => dispatcher.ToTransportAddress(LogicalAddress.CreateRemoteAddress(i))).ToArray();
        foreach (var publisherAddress in publisherAddresses)
        {
            Logger.Debug(
                $"Sending {intent} request for {messageType} to {publisherAddress} on behalf of {subscriber.TransportAddress}.");

            var transportOperation = new TransportOperation(subscriptionMessage, new UnicastAddressTag(publisherAddress));
            await dispatcher.Dispatch(new TransportOperations(transportOperation), new TransportTransaction(),
                new ContextBag()).ConfigureAwait(false);
        }
    }
}
