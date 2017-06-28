using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Settings;
using NServiceBus.Transport;

class Hub
{

}

class SpokeConfig<T>
    where T : TransportDefinition, new()
{
    public string Name { get; }
    public Action<TransportExtensions<T>> TransportCustomization { get; }
    public Action<EndpointConfiguration> PersistenceCustomization { get; }
    public EndpointInstances EndpointInstances { get; }
    public IDistributionPolicy DistributionPolicy { get; }

}

class Spoke<T>
    where T : TransportDefinition, new()
{
    public Spoke(SpokeConfig<T> config, RuntimeTypeGenerator typeGenerator, string poisonQueue, bool autoCreateQueues, string autoCreateQueuesIdentity,
        Func<MessageContext, PubSubInfrastructure, Task> onMessage)
    {
        pubSubInfra = new PubSubInfrastructure(config.EndpointInstances, config.DistributionPolicy, typeGenerator);

        rawConfig = RawEndpointConfiguration.Create(config.Name, (context, _) => onMessage(context, pubSubInfra), poisonQueue);

        var transport = rawConfig.UseTransport<T>();
        SetTransportSpecificFlags(transport.GetSettings(), poisonQueue);
        config.TransportCustomization?.Invoke(transport);
        if (autoCreateQueues)
        {
            rawConfig.AutoCreateQueue(autoCreateQueuesIdentity);
        }

        routerEndpointConfig = CreateDispatcherConfig(config.Name, config.PersistenceCustomization, poisonQueue, config.TransportCustomization, pubSubInfra, autoCreateQueues, autoCreateQueuesIdentity);
    }

    static EndpointConfiguration CreateDispatcherConfig<TTransport>(string name, Action<EndpointConfiguration> subscriptionPersistenceConfig, string poisonQueue, Action<TransportExtensions<TTransport>> transportCustomization, PubSubInfrastructure pubSubInfrastructure, bool autoCreateQueues, string autoCreateQueuesIdentity)
        where TTransport : TransportDefinition, new()
    {
        var dispatcherConfig = new EndpointConfiguration(name);
        dispatcherConfig.SendOnly();
        dispatcherConfig.EnableFeature<PubSubInfrastructureBuilderFeature>();
        dispatcherConfig.RegisterComponents(c =>
        {
            c.RegisterSingleton(pubSubInfrastructure);
        });
        var transport = dispatcherConfig.UseTransport<TTransport>();
        var settings = transport.GetSettings();
        SetTransportSpecificFlags(settings, poisonQueue);
        transportCustomization?.Invoke(transport);
        subscriptionPersistenceConfig?.Invoke(dispatcherConfig);
        if (autoCreateQueues)
        {
            dispatcherConfig.EnableInstallers(autoCreateQueuesIdentity);
        }
        dispatcherConfig.AssemblyScanner().ScanAppDomainAssemblies = false;
        dispatcherConfig.AssemblyScanner().ExcludeAssemblies("NServiceBus.AcceptanceTesting");
        return dispatcherConfig;
    }

    static void SetTransportSpecificFlags(SettingsHolder settings, string poisonQueue)
    {
        settings.Set("errorQueue", poisonQueue);
        settings.Set("RabbitMQ.RoutingTopologySupportsDelayedDelivery", true);
    }

    public Task Forward(MessageContext context, PubSubInfrastructure inboundPubSubInfra)
    {
        var intent = GetMesssageIntent(context);

        switch (intent)
        {
            case MessageIntentEnum.Subscribe:
            case MessageIntentEnum.Unsubscribe:
                return SubscribeRouter.Route(context, intent, sender, pubSubInfra.SubscribeForwarder, inboundPubSubInfra.SubscriptionStorage);
            case MessageIntentEnum.Publish:
                return pubSubInfra.PublishRouter.Route(context, intent, sender);
            default:
                throw new UnforwardableMessageException("Unroutable message intent: " + intent);
        }
    }

    static MessageIntentEnum GetMesssageIntent(MessageContext message)
    {
        var messageIntent = default(MessageIntentEnum);
        if (message.Headers.TryGetValue(Headers.MessageIntent, out string messageIntentString))
        {
            Enum.TryParse(messageIntentString, true, out messageIntent);
        }
        return messageIntent;
    }

    public Task Initialize()
    {
        
    }

    public Task StartReceiving()
    {
        
    }

    public Task StopReceiving()
    {
        
    }

    public Task Stop()
    {
    }

    IEndpointInstance router;
    IReceivingRawEndpoint receiver;
    IStartableRawEndpoint sender;

    RawEndpointConfiguration rawConfig;
    EndpointConfiguration routerEndpointConfig;
    PubSubInfrastructure pubSubInfra;
}
