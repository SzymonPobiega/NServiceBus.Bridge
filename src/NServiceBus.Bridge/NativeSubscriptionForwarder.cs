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

class NativeSubscriptionForwarder : SubscriptionForwarder
{
    static ILog Logger = LogManager.GetLogger<NativeSubscriptionForwarder>();

    IManageSubscriptions subscriptionManager;
    RuntimeTypeGenerator typeGenerator;
    EndpointInstances endpointInstances;

    public NativeSubscriptionForwarder(IManageSubscriptions subscriptionManager, RuntimeTypeGenerator typeGenerator, EndpointInstances endpointInstances)
    {
        this.subscriptionManager = subscriptionManager;
        this.typeGenerator = typeGenerator;
        this.endpointInstances = endpointInstances;
    }

    public override async Task ForwardSubscribe(MessageContext context, Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher, InterBridgeRoutingSettings forwarding)
    {
        var type = typeGenerator.GetType(messageType);
        await subscriptionManager.Subscribe(type, new ContextBag()).ConfigureAwait(false);
        await Send(context, subscriber, publisherEndpoint, messageType, MessageIntentEnum.Subscribe, dispatcher, forwarding).ConfigureAwait(false);
    }

    public override async Task ForwardUnsubscribe(MessageContext context, Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher, InterBridgeRoutingSettings forwarding)
    {
        var type = typeGenerator.GetType(messageType);
        await subscriptionManager.Unsubscribe(type, new ContextBag()).ConfigureAwait(false);
        await Send(context,subscriber, publisherEndpoint, messageType, MessageIntentEnum.Unsubscribe, dispatcher, forwarding).ConfigureAwait(false);
    }

    async Task Send(MessageContext context, Subscriber subscriber, string publisherEndpoint, string messageType, MessageIntentEnum intent, IRawEndpoint dispatcher, InterBridgeRoutingSettings forwarding)
    {
        var typeFullName = messageType.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).First();

        if (!forwarding.TryGetDestination(context, typeFullName, out var nextHops))
        {
            return;
        }

        var ops = nextHops
            .SelectMany(h => CreateOperations(subscriber, publisherEndpoint, messageType, intent, dispatcher, h))
            .ToArray();

        await dispatcher.Dispatch(new TransportOperations(ops), new TransportTransaction(), new ContextBag()).ConfigureAwait(false);
    }

    IEnumerable<TransportOperation> CreateOperations(Subscriber subscriber, string publisherEndpoint, string messageType, MessageIntentEnum intent, IRawEndpoint dispatcher, string nextHop)
    {
        var subscriptionMessage = ControlMessageFactory.Create(intent);
        if (publisherEndpoint != null)
        {
            subscriptionMessage.Headers["NServiceBus.Bridge.DestinationEndpoint"] = publisherEndpoint;
        }
        subscriptionMessage.Headers[Headers.SubscriptionMessageType] = messageType;
        subscriptionMessage.Headers[Headers.ReplyToAddress] = dispatcher.TransportAddress;
        subscriptionMessage.Headers[Headers.SubscriberTransportAddress] = dispatcher.TransportAddress;
        subscriptionMessage.Headers[Headers.SubscriberEndpoint] = dispatcher.EndpointName;
        subscriptionMessage.Headers[Headers.TimeSent] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow);
        subscriptionMessage.Headers[Headers.NServiceBusVersion] = "6.3.1"; //The code has been copied from 6.3.1

        var publisherInstances = endpointInstances.FindInstances(nextHop);
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