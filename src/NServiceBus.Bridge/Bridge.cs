using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Settings;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class Bridge<TLeft, TRight> : IBridge
    where TLeft : TransportDefinition, new()
    where TRight : TransportDefinition, new()
{
    IReceivingRawEndpoint rightEndpoint;
    IStartableRawEndpoint rightStartable;
    IReceivingRawEndpoint leftEndpoint;
    IStartableRawEndpoint leftStartable;

    ThrottlingRawEndpointConfig<TLeft> leftConfig;
    ThrottlingRawEndpointConfig<TRight> rightConfig;

    SendRouter sendRouter;
    IRouter replyRouter;

    public Bridge(string leftName, string rightName, bool autoCreateQueues, string autoCreateQueuesIdentity, EndpointInstances endpointInstances, ISubscriptionStorage subscriptionPersistence, RawDistributionPolicy distributionPolicy, string poisonQueue, Action<TransportExtensions<TLeft>> leftCustomization, Action<TransportExtensions<TRight>> rightCustomization, int? maximumConcurrency, InterceptMessageForwarding interceptForward, InterBridgeRoutingSettings forwarding, int immediateRetries, int delayedRetries, int circuitBreakerThreshold)
    {
        sendRouter = new SendRouter(endpointInstances, distributionPolicy);
        replyRouter = new ReplyRouter();

        var typeGenerator = new RuntimeTypeGenerator();
        var leftPubSubInfrastructure = new PubSubInfrastructure(endpointInstances, distributionPolicy, typeGenerator);
        var rightPubSubInfrastructure = new PubSubInfrastructure(endpointInstances, distributionPolicy, typeGenerator);

        leftConfig = new ThrottlingRawEndpointConfig<TLeft>(leftName, poisonQueue, ext =>
            {
                SetTransportSpecificFlags(ext.GetSettings(), poisonQueue);
                leftCustomization?.Invoke(ext);
            },
            (context, _) => interceptForward(leftName, context, rightStartable.Dispatch, 
                dispatch => Forward(context, Intercept(rightStartable, dispatch), leftPubSubInfrastructure, rightPubSubInfrastructure, forwarding)),
            (context, dispatcher) => context.MoveToErrorQueue(poisonQueue),
            maximumConcurrency,
            immediateRetries, delayedRetries, circuitBreakerThreshold, autoCreateQueues, autoCreateQueuesIdentity);


        var nullForwarding = new InterBridgeRoutingSettings(); //Messages from right to left are not forwarded by design.
        rightConfig = new ThrottlingRawEndpointConfig<TRight>(rightName, poisonQueue, ext =>
            {
                SetTransportSpecificFlags(ext.GetSettings(), poisonQueue);
                rightCustomization?.Invoke(ext);
            },
            (context, _) => interceptForward(rightName, context, leftStartable.Dispatch, 
                dispatch => Forward(context, Intercept(leftStartable, dispatch), rightPubSubInfrastructure, leftPubSubInfrastructure, nullForwarding)),
            (context, dispatcher) => null,
            maximumConcurrency,
            immediateRetries, delayedRetries, circuitBreakerThreshold, autoCreateQueues, autoCreateQueuesIdentity);
    }

    static IRawEndpoint Intercept(IRawEndpoint impl, Dispatch intercept)
    {
        return new InterceptingDispatcher(impl, intercept);
    }

    static void SetTransportSpecificFlags(SettingsHolder settings, string poisonQueue)
    {
        settings.Set("errorQueue", poisonQueue);
        settings.Set("RabbitMQ.RoutingTopologySupportsDelayedDelivery", true);
    }

    Task Forward(MessageContext context, IRawEndpoint dispatcher, PubSubInfrastructure inboundPubSubInfra, PubSubInfrastructure outboundPubSubInfra, InterBridgeRoutingSettings forwarding)
    {
        var intent = GetMesssageIntent(context);

        switch (intent)
        {
            case MessageIntentEnum.Subscribe:
            case MessageIntentEnum.Unsubscribe:
                return SubscribeRouter.Route(context, intent, dispatcher, outboundPubSubInfra.SubscribeForwarder, inboundPubSubInfra.SubscriptionStorage, forwarding);
            case MessageIntentEnum.Send:
                return sendRouter.Route(context, dispatcher, forwarding);
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

        leftStartable = await leftConfig.Create().ConfigureAwait(false);
        rightStartable = await rightConfig.Create().ConfigureAwait(false);

        leftEndpoint = await leftStartable.Start().ConfigureAwait(false);
        rightEndpoint = await rightStartable.Start().ConfigureAwait(false);
    }

    public async Task Stop()
    {
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
