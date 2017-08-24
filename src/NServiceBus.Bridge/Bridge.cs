using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Settings;
using NServiceBus.Transport;

class Bridge<TLeft, TRight> : IBridge
    where TLeft : TransportDefinition, new()
    where TRight : TransportDefinition, new()
{
    IEndpointInstance leftDispatcher;
    IEndpointInstance rightDispatcher;

    IReceivingRawEndpoint rightEndpoint;
    IStartableRawEndpoint rightStartable;
    IReceivingRawEndpoint leftEndpoint;
    IStartableRawEndpoint leftStartable;

    RawEndpointConfiguration leftConfig;
    EndpointConfiguration leftDispatcherConfig;
    RawEndpointConfiguration rightConfig;
    EndpointConfiguration rightDispatcherConfig;

    IRouter sendRouter;
    IRouter replyRouter;

    public Bridge(string leftName, string rightName, bool autoCreateQueues, string autoCreateQueuesIdentity, EndpointInstances endpointInstances, 
        Action<EndpointConfiguration> subscriptionPersistenceConfig, IDistributionPolicy distributionPolicy, string poisonQueue, 
        Action<TransportExtensions<TLeft>> leftCustomization, Action<TransportExtensions<TRight>> rightCustomization, int? maximumConcurrency)
    {
        sendRouter = new SendRouter(endpointInstances, distributionPolicy);
        replyRouter = new ReplyRouter();

        var typeGenerator = new RuntimeTypeGenerator();
        var leftPubSubInfrastructure = new PubSubInfrastructure(endpointInstances, distributionPolicy, typeGenerator);
        var rightPubSubInfrastructure = new PubSubInfrastructure(endpointInstances, distributionPolicy, typeGenerator);

        leftConfig = RawEndpointConfiguration.Create(leftName, (context, _) => Forward(context, rightStartable, leftPubSubInfrastructure, rightPubSubInfrastructure), poisonQueue);
        var leftTransport = leftConfig.UseTransport<TLeft>();
        SetTransportSpecificFlags(leftTransport.GetSettings(), poisonQueue);
        leftCustomization?.Invoke(leftTransport);
        if (autoCreateQueues)
        {
            leftConfig.AutoCreateQueue(autoCreateQueuesIdentity);
        }

        leftDispatcherConfig = CreateDispatcherConfig(leftName, subscriptionPersistenceConfig, poisonQueue, leftCustomization, leftPubSubInfrastructure, autoCreateQueues);

        rightConfig = RawEndpointConfiguration.Create(rightName, (context, _) => Forward(context, leftStartable, rightPubSubInfrastructure, leftPubSubInfrastructure), poisonQueue);
        var rightTransport = rightConfig.UseTransport<TRight>();
        SetTransportSpecificFlags(rightTransport.GetSettings(), poisonQueue);
        rightCustomization?.Invoke(rightTransport);
        if (autoCreateQueues)
        {
            rightConfig.AutoCreateQueue(autoCreateQueuesIdentity);
        }

        rightDispatcherConfig = CreateDispatcherConfig(rightName, subscriptionPersistenceConfig, poisonQueue, rightCustomization, rightPubSubInfrastructure, autoCreateQueues);

        if (maximumConcurrency.HasValue)
        {
            leftConfig.LimitMessageProcessingConcurrencyTo(maximumConcurrency.Value);
            rightConfig.LimitMessageProcessingConcurrencyTo(maximumConcurrency.Value);
        }
    }

    static EndpointConfiguration CreateDispatcherConfig<TTransport>(string name, Action<EndpointConfiguration> subscriptionPersistenceConfig, string poisonQueue, Action<TransportExtensions<TTransport>> transportCustomization, PubSubInfrastructure pubSubInfrastructure, bool autoCreateQueues)
        where TTransport : TransportDefinition, new()
    {
        var dispatcherConfig = new EndpointConfiguration(name);
        dispatcherConfig.SendOnly();
        dispatcherConfig.EnableFeature<PubSubInfrastructureBuilderFeature>();
        dispatcherConfig.RegisterComponents(c =>
        {
            c.RegisterSingleton(pubSubInfrastructure);
        });
        dispatcherConfig.EnableInstallers();
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

    Task Forward(MessageContext context, IRawEndpoint dispatcher, PubSubInfrastructure inboundPubSubInfra, PubSubInfrastructure outboundPubSubInfra)
    {
        var intent = GetMesssageIntent(context);

        switch (intent)
        {
            case MessageIntentEnum.Subscribe:
            case MessageIntentEnum.Unsubscribe:
                return SubscribeRouter.Route(context, intent, dispatcher, outboundPubSubInfra.SubscribeForwarder, inboundPubSubInfra.SubscriptionStorage);
            case MessageIntentEnum.Send:
                return sendRouter.Route(context, intent, dispatcher);
            case MessageIntentEnum.Publish:
                return outboundPubSubInfra.PublishRouter.Route(context, intent, dispatcher);
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

    public async Task Start()
    {
        leftDispatcher = await Endpoint.Start(leftDispatcherConfig).ConfigureAwait(false);
        rightDispatcher = await Endpoint.Start(rightDispatcherConfig).ConfigureAwait(false);

        //At this stage the pubsub infrastructure is set up.

        leftStartable = await RawEndpoint.Create(leftConfig).ConfigureAwait(false);
        rightStartable = await RawEndpoint.Create(rightConfig).ConfigureAwait(false);

        leftEndpoint = await leftStartable.Start().ConfigureAwait(false);
        rightEndpoint = await rightStartable.Start().ConfigureAwait(false);
    }

    public async Task Stop()
    {
        if (leftDispatcher != null)
        {
            await leftDispatcher.Stop().ConfigureAwait(false);
        }
        if (rightDispatcher != null)
        {
            await rightDispatcher.Stop().ConfigureAwait(false);
        }

        IStoppableRawEndpoint leftStoppable = null;
        IStoppableRawEndpoint rightStoppable = null;

        if (leftEndpoint != null)
        {
            leftStoppable = await leftEndpoint.StopReceiving().ConfigureAwait(false);
        }
        if (rightEndpoint != null)
        {
            rightStoppable = await rightEndpoint.StopReceiving().ConfigureAwait(false);
        }
        if (leftStoppable != null)
        {
            await leftStoppable.Stop().ConfigureAwait(false);
        }
        if (rightStoppable != null)
        {
            await rightStoppable.Stop().ConfigureAwait(false);
        }
    }
}
