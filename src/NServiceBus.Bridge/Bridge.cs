using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Settings;
using NServiceBus.Transport;

class Bridge<TLeft, TRight> : IBridge
    where TLeft : TransportDefinition, new()
    where TRight : TransportDefinition, new()
{
    RuntimeTypeGenerator typeGenerator;
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

    PubSubInfrastructure rightPubSubInfrastructure;
    PubSubInfrastructure leftPubSubInfrastructure;

    public Bridge(string leftName, string rightName, bool autoCreateQueues, string autoCreateQueuesIdentity, EndpointInstances endpointInstances, 
        Action<EndpointConfiguration> subscriptionPersistenceConfig, IDistributionPolicy distributionPolicy, string poisonQueue, 
        Action<TransportExtensions<TLeft>> leftCustomization, Action<TransportExtensions<TRight>> rightCustomization, int? maximumConcurrency,
        InterceptMessageForwarding interceptForward, RuntimeTypeGenerator typeGenerator)
    {
        this.typeGenerator = typeGenerator;
        sendRouter = new SendRouter(endpointInstances, distributionPolicy);
        replyRouter = new ReplyRouter();

        leftPubSubInfrastructure = new PubSubInfrastructure(endpointInstances, distributionPolicy, typeGenerator);
        rightPubSubInfrastructure = new PubSubInfrastructure(endpointInstances, distributionPolicy, typeGenerator);

        leftConfig = RawEndpointConfiguration.Create(leftName, (context, _) => interceptForward(leftName, context, () => Forward(context, rightStartable, leftPubSubInfrastructure, rightPubSubInfrastructure)), poisonQueue);
        var leftTransport = leftConfig.UseTransport<TLeft>();
        SetTransportSpecificFlags(leftTransport.GetSettings(), poisonQueue);
        leftCustomization?.Invoke(leftTransport);
        if (autoCreateQueues)
        {
            leftConfig.AutoCreateQueue(autoCreateQueuesIdentity);
        }

        if (new TLeft() is IMessageDrivenSubscriptionTransport)
        {
            leftDispatcherConfig = CreateDispatcherConfig(leftName, subscriptionPersistenceConfig, poisonQueue, leftCustomization, leftPubSubInfrastructure, autoCreateQueues);
        }

        rightConfig = RawEndpointConfiguration.Create(rightName, (context, _) => interceptForward(rightName, context, () => Forward(context, leftStartable, rightPubSubInfrastructure, leftPubSubInfrastructure)), poisonQueue);
        var rightTransport = rightConfig.UseTransport<TRight>();
        SetTransportSpecificFlags(rightTransport.GetSettings(), poisonQueue);
        rightCustomization?.Invoke(rightTransport);
        if (autoCreateQueues)
        {
            rightConfig.AutoCreateQueue(autoCreateQueuesIdentity);
        }

        if (new TRight() is IMessageDrivenSubscriptionTransport)
        {
            rightDispatcherConfig = CreateDispatcherConfig(rightName, subscriptionPersistenceConfig, poisonQueue, rightCustomization, rightPubSubInfrastructure, autoCreateQueues);
        }

        if (maximumConcurrency.HasValue)
        {
            leftConfig.LimitMessageProcessingConcurrencyTo(maximumConcurrency.Value);
            rightConfig.LimitMessageProcessingConcurrencyTo(maximumConcurrency.Value);
        }
    }

    

    EndpointConfiguration CreateDispatcherConfig<TTransport>(string name, Action<EndpointConfiguration> subscriptionPersistenceConfig, string poisonQueue, Action<TransportExtensions<TTransport>> transportCustomization, PubSubInfrastructure pubSubInfrastructure, bool autoCreateQueues)
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
        //At this stage the pubsub infrastructure is set up.

        leftStartable = await RawEndpoint.Create(leftConfig).ConfigureAwait(false);
        rightStartable = await RawEndpoint.Create(rightConfig).ConfigureAwait(false);

        if (rightDispatcherConfig != null)
        {
            rightDispatcher = await Endpoint.Start(rightDispatcherConfig).ConfigureAwait(false);
        }
        else
        {
            var subscriptionManager = SubscriptionManagerHelper.CreateSubscriptionManager(rightStartable.Settings.Get<TransportInfrastructure>());
            var forwarder = new NativeSubscriptionForwarder(subscriptionManager, typeGenerator);
            var publishRouter = new NativePublishRouter(typeGenerator);
            rightPubSubInfrastructure.Set(publishRouter, forwarder, new NativeSubscriptionStorage());
        }

        if (leftDispatcherConfig != null)
        {
            leftDispatcher = await Endpoint.Start(leftDispatcherConfig).ConfigureAwait(false);
        }
        else
        {
            var subscriptionManager = SubscriptionManagerHelper.CreateSubscriptionManager(leftStartable.Settings.Get<TransportInfrastructure>());
            var forwarder = new NativeSubscriptionForwarder(subscriptionManager, typeGenerator);
            var publishRouter = new NativePublishRouter(typeGenerator);
            leftPubSubInfrastructure.Set(publishRouter, forwarder, new NativeSubscriptionStorage());
        }

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