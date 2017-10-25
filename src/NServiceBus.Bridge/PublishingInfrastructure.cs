
using NServiceBus.Bridge;
using NServiceBus.Routing;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class PubSubInfrastructure
{
    public PubSubInfrastructure(EndpointInstances endpointInstances, RawDistributionPolicy distributionPolicy, RuntimeTypeGenerator typeGenerator)
    {
        EndpointInstances = endpointInstances;
        DistributionPolicy = distributionPolicy;
        TypeGenerator = typeGenerator;
    }

    public void Set(IRouter publishRouter, ISubscriptionForwarder subscribeForwarder, ISubscriptionStorage subscriptionStorage)
    {
        PublishRouter = publishRouter;
        SubscribeForwarder = subscribeForwarder;
        SubscriptionStorage = subscriptionStorage;
    }
    
    public IRouter PublishRouter { get; private set; }
    public ISubscriptionForwarder SubscribeForwarder { get; private set; }
    public ISubscriptionStorage SubscriptionStorage { get; private set; }
    public EndpointInstances EndpointInstances { get; }
    public RawDistributionPolicy DistributionPolicy { get; }
    public RuntimeTypeGenerator TypeGenerator { get; }
}