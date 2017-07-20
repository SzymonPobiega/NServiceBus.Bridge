using System.Threading.Tasks;
using NServiceBus.Raw;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

interface ISubscriptionForwarder
{
    Task ForwardSubscribe(Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher);
    Task ForwardUnsubscribe(Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher);
}