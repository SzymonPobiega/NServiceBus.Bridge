using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Raw;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class NativeSubscriptionForwarder : ISubscriptionForwarder
{
    IManageSubscriptions subscriptionManager;
    RuntimeTypeGenerator typeGenerator;

    public NativeSubscriptionForwarder(IManageSubscriptions subscriptionManager, RuntimeTypeGenerator typeGenerator)
    {
        this.subscriptionManager = subscriptionManager;
        this.typeGenerator = typeGenerator;
    }

    public Task ForwardSubscribe(Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher)
    {
        var type = typeGenerator.GetType(messageType);
        return subscriptionManager.Subscribe(type, new ContextBag());
    }

    public Task ForwardUnsubscribe(Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher)
    {
        var type = typeGenerator.GetType(messageType);
        return subscriptionManager.Unsubscribe(type, new ContextBag());
    }
}