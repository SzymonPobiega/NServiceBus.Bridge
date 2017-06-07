using System;
using System.Reflection;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class Bridge<TLeft, TRight> : IBridge
    where TLeft : TransportDefinition, new()
    where TRight : TransportDefinition, new()
{
    EndpointInstances endpointInstances;
    ISubscriptionStorage subscriptionStorage;
    IStartableRawEndpoint leftStartable;
    IStartableRawEndpoint rightStartable;

    IReceivingRawEndpoint rightEndpoint;
    IReceivingRawEndpoint leftEndpoint;

    RawEndpointConfiguration leftConfig;
    RawEndpointConfiguration rightConfig;

    IRouter publishRouter;
    IRouter sendRouter;
    IRouter replyRouter;
    IRouter leftSubscribeRouter;
    IRouter rightSubscribeRouter;

    public Bridge(string leftName, string rightName, bool autoCreateQueues, string autoCreateQueuesIdentity, 
        EndpointInstances endpointInstances, ISubscriptionStorage subscriptionStorage, IDistributionPolicy distributionPolicy, string poisonQueue,
        Action<TransportExtensions<TLeft>> leftCustomization, Action<TransportExtensions<TRight>> rightCustomization)
    {
        this.endpointInstances = endpointInstances;
        this.subscriptionStorage = subscriptionStorage;
        publishRouter = new PublishRouter(subscriptionStorage, distributionPolicy);
        sendRouter = new SendRouter(endpointInstances, distributionPolicy);
        replyRouter = new ReplyRouter();

        leftConfig = RawEndpointConfiguration.Create(leftName, (context, _) => Forward(context, rightStartable, rightSubscribeRouter), poisonQueue);
        var leftTransport = leftConfig.UseTransport<TLeft>();
        leftTransport.GetSettings().Set("errorQueue", poisonQueue);
        leftCustomization?.Invoke(leftTransport);
        if (autoCreateQueues)
        {
            leftConfig.AutoCreateQueue(autoCreateQueuesIdentity);
        }

        rightConfig = RawEndpointConfiguration.Create(rightName, (context, _) => Forward(context, leftStartable, leftSubscribeRouter), poisonQueue);
        var rightTransport = rightConfig.UseTransport<TRight>();
        rightTransport.GetSettings().Set("errorQueue", poisonQueue);
        rightCustomization?.Invoke(rightTransport);
        if (autoCreateQueues)
        {
            rightConfig.AutoCreateQueue(autoCreateQueuesIdentity);
        }
    }

    Task Forward(MessageContext context, IRawEndpoint dispatcher, IRouter subscribeRouter)
    {
        var intent = GetMesssageIntent(context);

        switch (intent)
        {
            case MessageIntentEnum.Subscribe:
            case MessageIntentEnum.Unsubscribe:
                return subscribeRouter.Route(context, intent, dispatcher);
            case MessageIntentEnum.Send:
                return sendRouter.Route(context, intent, dispatcher);
            case MessageIntentEnum.Publish:
                return publishRouter.Route(context, intent, dispatcher);
            case MessageIntentEnum.Reply:
                return replyRouter.Route(context, intent, dispatcher);
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

    public async Task Start()
    {
        leftStartable = await RawEndpoint.Create(leftConfig).ConfigureAwait(false);
        rightStartable = await RawEndpoint.Create(rightConfig).ConfigureAwait(false);

        leftSubscribeRouter = CreateSubscribeRouter(leftStartable.Settings.Get<TransportInfrastructure>());
        rightSubscribeRouter = CreateSubscribeRouter(rightStartable.Settings.Get<TransportInfrastructure>());
        
        leftEndpoint = await leftStartable.Start().ConfigureAwait(false);
        rightEndpoint = await rightStartable.Start().ConfigureAwait(false);
    }

    IRouter CreateSubscribeRouter(TransportInfrastructure transportInfrastructure)
    {
        if (transportInfrastructure.OutboundRoutingPolicy.Publishes == OutboundRoutingType.Unicast)
        {
            return new MessageDrivenSubscribeRouter(subscriptionStorage, endpointInstances);
        }
        var subscriptionInfra = transportInfrastructure.ConfigureSubscriptionInfrastructure();
        var factoryProperty = typeof(TransportSubscriptionInfrastructure).GetProperty("SubscriptionManagerFactory", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var factoryInstance = (Func<IManageSubscriptions>)factoryProperty.GetValue(subscriptionInfra, new object[0]);
        var subscriptionManager = factoryInstance();
        return new NativeSubscribeRouter(subscriptionStorage, subscriptionManager);
    }

    public async Task Stop()
    {
        IStoppableRawEnedpoint leftStoppable = null;
        IStoppableRawEnedpoint rightStoppable = null;

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
