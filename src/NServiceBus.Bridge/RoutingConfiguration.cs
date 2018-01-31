using System;
using NServiceBus.Bridge;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class RoutingConfiguration
{
    RuntimeTypeGenerator typeGenerator;
    EndpointInstances endpointInstances;
    ISubscriptionStorage subscriptionPersistence;
    RawDistributionPolicy distributionPolicy;

    public RoutingConfiguration(RuntimeTypeGenerator typeGenerator, EndpointInstances endpointInstances, ISubscriptionStorage subscriptionPersistence, RawDistributionPolicy distributionPolicy)
    {
        this.typeGenerator = typeGenerator;
        this.endpointInstances = endpointInstances;
        this.subscriptionPersistence = subscriptionPersistence;
        this.distributionPolicy = distributionPolicy;
    }

    public void PreparePubSub(IRawEndpoint endpoint, out IPublishRouter publishRouter, out SubscriptionReceiver subscriptionReceiver, out SubscriptionForwarder subscriptionForwarder)
    {
        var transport = endpoint.Settings.Get<TransportInfrastructure>();
        if (transport.OutboundRoutingPolicy.Publishes == OutboundRoutingType.Multicast)
        {
            publishRouter = new NativePublishRouter(typeGenerator);
            subscriptionReceiver = new NullSubscriptionReceiver();
            subscriptionForwarder = new NativeSubscriptionForwarder(endpoint.SubscriptionManager, typeGenerator, endpointInstances);
        }
        else
        {
            if (subscriptionPersistence == null)
            {
                throw new Exception("Subscription storage has not been configured. Use 'UseSubscriptionPersistence' method to configure it.");
            }
            publishRouter = new MessageDrivenPublishRouter(subscriptionPersistence, distributionPolicy);
            subscriptionReceiver = new StorageDrivenSubscriptionReceiver(subscriptionPersistence);
            subscriptionForwarder = new MessageDrivenSubscriptionForwarder(endpointInstances);
        }
    }

    public SendRouter PrepareSending()
    {
        return new SendRouter(endpointInstances, distributionPolicy);
    }
}