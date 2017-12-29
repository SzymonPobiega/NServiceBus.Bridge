using System.Threading.Tasks;
using NServiceBus.Bridge;
using NServiceBus.Raw;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

interface ISubscriptionForwarder
{
    bool RequiresPublisherEndpoint { get; }
    Task ForwardSubscribe(Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher, InterBridgeRoutingSettings forwarding);
    Task ForwardUnsubscribe(Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher, InterBridgeRoutingSettings forwarding);
}