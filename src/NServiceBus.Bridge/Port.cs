using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Settings;
using NServiceBus.Transport;

class Port<T> : IPort
    where T : TransportDefinition, new()
{
    public string Name { get; }
    public Port(string name, Action<TransportExtensions<T>> transportCustomization, Action<EndpointConfiguration> subscriptionPersistenceConfig, EndpointInstances endpointInstances, IDistributionPolicy distributionPolicy, RuntimeTypeGenerator typeGenerator, string poisonQueue, int? maximumConcurrency, bool autoCreateQueues, string autoCreateQueuesIdentity)
    {
        this.typeGenerator = typeGenerator;
        Name = name;
        sendRouter = new SendRouter(endpointInstances, distributionPolicy, Name);
        replyRouter = new ReplyRouter();
        pubSubInfra = new PubSubInfrastructure(endpointInstances, distributionPolicy, typeGenerator);

        rawConfig = RawEndpointConfiguration.Create(name, (context, _) => onMessage(context, pubSubInfra), poisonQueue);

        var transport = rawConfig.UseTransport<T>();
        SetTransportSpecificFlags(transport.GetSettings(), poisonQueue);
        transportCustomization?.Invoke(transport);
        if (autoCreateQueues)
        {
            rawConfig.AutoCreateQueue(autoCreateQueuesIdentity);
        }
        if (maximumConcurrency.HasValue)
        {
            rawConfig.LimitMessageProcessingConcurrencyTo(maximumConcurrency.Value);
        }

        if (new T() is IMessageDrivenSubscriptionTransport)
        {
            routerEndpointConfig = CreatePubSubRoutingEndpoint(name, subscriptionPersistenceConfig, poisonQueue, transportCustomization, pubSubInfra);
        }
    }

    static EndpointConfiguration CreatePubSubRoutingEndpoint<TTransport>(string name, Action<EndpointConfiguration> subscriptionPersistenceConfig, string poisonQueue, Action<TransportExtensions<TTransport>> transportCustomization, PubSubInfrastructure pubSubInfrastructure)
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
            case MessageIntentEnum.Send:
                return sendRouter.Route(context, intent, sender);
            case MessageIntentEnum.Reply:
                return replyRouter.Route(context, intent, sender);
            default:
                throw new UnforwardableMessageException("Unroutable message intent: " + intent);
        }
    }

    static MessageIntentEnum GetMesssageIntent(MessageContext message)
    {
        var messageIntent = default(MessageIntentEnum);
        if (message.Headers.TryGetValue(Headers.MessageIntent, out var messageIntentString))
        {
            Enum.TryParse(messageIntentString, true, out messageIntent);
        }
        return messageIntent;
    }

    public async Task Initialize(Func<MessageContext, PubSubInfrastructure, Task> onMessage)
    {
        this.onMessage = onMessage;
        sender = await RawEndpoint.Create(rawConfig).ConfigureAwait(false);

        if (routerEndpointConfig != null)
        {
            pubSubRoutingEndpoint = await Endpoint.Start(routerEndpointConfig).ConfigureAwait(false);
        }
        else
        {
            var subscriptionManager = SubscriptionManagerHelper.CreateSubscriptionManager(sender.Settings.Get<TransportInfrastructure>());
            var forwarder = new NativeSubscriptionForwarder(subscriptionManager, typeGenerator);
            var publishRouter = new NativePublishRouter(typeGenerator);
            pubSubInfra.Set(publishRouter, forwarder, new NativeSubscriptionStorage());
        }
    }

    public async Task StartReceiving()
    {
        receiver = await sender.Start().ConfigureAwait(false);
    }

    public async Task StopReceiving()
    {
        if (pubSubRoutingEndpoint != null)
        {
            await pubSubRoutingEndpoint.Stop().ConfigureAwait(false);
        }
        if (receiver != null)
        {
            stoppable = await receiver.StopReceiving().ConfigureAwait(false);
        }
        else
        {
            stoppable = null;
        }
    }

    public async Task Stop()
    {
        if (stoppable != null)
        {
            await stoppable.Stop().ConfigureAwait(false);
            stoppable = null;
        }
    }

    RuntimeTypeGenerator typeGenerator;
    Func<MessageContext, PubSubInfrastructure, Task> onMessage;
    IEndpointInstance pubSubRoutingEndpoint;
    IReceivingRawEndpoint receiver;
    IStartableRawEndpoint sender;
    IStoppableRawEndpoint stoppable;

    RawEndpointConfiguration rawConfig;
    EndpointConfiguration routerEndpointConfig;
    PubSubInfrastructure pubSubInfra;
    SendRouter sendRouter;
    ReplyRouter replyRouter;
}
