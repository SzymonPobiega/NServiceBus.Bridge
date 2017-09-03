using System;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
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

    public Task ForwardSubscribe(Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher)
    {
        return Send(subscriber, publisherEndpoint, messageType, MessageIntentEnum.Subscribe, dispatcher);
    }

    public Task ForwardUnsubscribe(Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher)
    {
        return Send(subscriber, publisherEndpoint, messageType, MessageIntentEnum.Unsubscribe, dispatcher);
    }

    async Task Send(Subscriber subscriber, string publisherEndpoint, string messageType, MessageIntentEnum intent, IRawEndpoint dispatcher)
    {
        var publisherInstances = endpointInstances.FindInstances(publisherEndpoint);
        var publisherAddresses = publisherInstances.Select(i => dispatcher.ToTransportAddress(LogicalAddress.CreateRemoteAddress(i))).ToArray();
        foreach (var publisherAddress in publisherAddresses)
        {
            Logger.Debug(
                $"Sending {intent} request for {messageType} to {publisherAddress} on behalf of {subscriber.TransportAddress}.");

            var subscriptionMessage = ControlMessageFactory.Create(intent);

            subscriptionMessage.Headers[Headers.SubscriptionMessageType] = messageType;
            subscriptionMessage.Headers[Headers.ReplyToAddress] = dispatcher.TransportAddress;
            subscriptionMessage.Headers[Headers.SubscriberTransportAddress] = dispatcher.TransportAddress;
            subscriptionMessage.Headers[Headers.SubscriberEndpoint] = dispatcher.EndpointName;
            subscriptionMessage.Headers[Headers.TimeSent] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow);
            subscriptionMessage.Headers[Headers.NServiceBusVersion] = "6.3.1"; //The code has been copied from 6.3.1

            var transportOperation = new TransportOperation(subscriptionMessage, new UnicastAddressTag(publisherAddress));
            await dispatcher.Dispatch(new TransportOperations(transportOperation), new TransportTransaction(),
                new ContextBag()).ConfigureAwait(false);
        }
    }
}
