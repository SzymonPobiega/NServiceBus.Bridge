using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Settings;
using NServiceBus.Transport;

class Port<T> : IPort
    where T : TransportDefinition, new()
{
    public string Name { get; }
    public Port(string name, Action<TransportExtensions<T>> transportCustomization, Action<EndpointConfiguration> subscriptionPersistenceConfig, EndpointInstances endpointInstances, RawDistributionPolicy distributionPolicy, RuntimeTypeGenerator typeGenerator, string poisonQueue, int? maximumConcurrency, InterceptMessageForwarding interceptMethod, bool autoCreateQueues, string autoCreateQueuesIdentity)
    {
        this.interceptMethod = interceptMethod;
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
        routerEndpointConfig = CreatePubSubRoutingEndpoint(name, subscriptionPersistenceConfig, poisonQueue, transportCustomization, pubSubInfra);
    }

    static EndpointConfiguration CreatePubSubRoutingEndpoint<TTransport>(string name, Action<EndpointConfiguration> subscriptionPersistenceConfig, string poisonQueue, Action<TransportExtensions<TTransport>> transportCustomization, PubSubInfrastructure pubSubInfrastructure)
        where TTransport : TransportDefinition, new()
    {
        var dispatcherConfig = new EndpointConfiguration(name);
        dispatcherConfig.SendOnly();
        dispatcherConfig.GetSettings().Set("NServiceBus.Bridge.LocalAddress", name);
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

    public Task Forward(string source, MessageContext context, PubSubInfrastructure inboundPubSubInfra)
    {
        return interceptMethod(source, context, sender.Dispatch, 
            dispatch => Forward(source, context, inboundPubSubInfra, new InterceptingDispatcher(sender, dispatch)));
    }

    Task Forward(string source, MessageContext context, PubSubInfrastructure inboundPubSubInfra, IRawEndpoint dispatcher)
    {
        var intent = GetMesssageIntent(context);

        switch (intent)
        {
            case MessageIntentEnum.Subscribe:
            case MessageIntentEnum.Unsubscribe:
                return SubscribeRouter.Route(context, intent, dispatcher, pubSubInfra.SubscribeForwarder, inboundPubSubInfra.SubscriptionStorage, nullForwarding);
            case MessageIntentEnum.Publish:
                return pubSubInfra.PublishRouter.Route(context, intent, dispatcher);
            case MessageIntentEnum.Send:
                return sendRouter.Route(context, dispatcher, nullForwarding);
            case MessageIntentEnum.Reply:
                return replyRouter.Route(context, intent, dispatcher);
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
        pubSubRoutingEndpoint = await Endpoint.Start(routerEndpointConfig).ConfigureAwait(false);
        sender = await RawEndpoint.Create(rawConfig).ConfigureAwait(false);
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

    InterceptMessageForwarding interceptMethod;
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
    InterBridgeRoutingSettings nullForwarding = new InterBridgeRoutingSettings();
}
