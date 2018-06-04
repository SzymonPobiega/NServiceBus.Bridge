﻿using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Raw;
using NServiceBus.Settings;
using NServiceBus.Transport;

class Port<T> : IPort
    where T : TransportDefinition, new()
{
    public string Name { get; }
    public Port(string name, Action<TransportExtensions<T>> transportCustomization, RoutingConfiguration routingConfiguration, string poisonQueue, int? maximumConcurrency, InterceptMessageForwarding interceptMethod, bool autoCreateQueues, string autoCreateQueuesIdentity, int immediateRetries, int delayedRetries, int circuitBreakerThreshold, InterBridgeRoutingSettings forwarding)
    {
        this.routingConfiguration = routingConfiguration;
        this.interceptMethod = interceptMethod;
        this.forwarding = forwarding;
        Name = name;
        sendRouter = routingConfiguration.PrepareSending();
        replyRouter = new ReplyRouter();

        rawConfig = new ThrottlingRawEndpointConfig<T>(name, poisonQueue, ext =>
            {
                SetTransportSpecificFlags(ext.GetSettings(), poisonQueue, name);
                transportCustomization?.Invoke(ext);
            },
            async (context, _) =>
            {
                var intent = GetMesssageIntent(context);
                if (intent == MessageIntentEnum.Subscribe || intent == MessageIntentEnum.Unsubscribe)
                {
                    await subscriptionReceiver.Receive(context, intent);
                }
                await onMessage(context);
            },
            (context, dispatcher) => context.MoveToErrorQueue(poisonQueue),
            maximumConcurrency,
            immediateRetries, delayedRetries, circuitBreakerThreshold, autoCreateQueues, autoCreateQueuesIdentity);
    }

    static void SetTransportSpecificFlags(SettingsHolder settings, string poisonQueue, string localAddress)
    {
        settings.Set("errorQueue", poisonQueue);
        settings.Set("RabbitMQ.RoutingTopologySupportsDelayedDelivery", true);
    }

    public Task Forward(string source, MessageContext context)
    {
        return interceptMethod(source, context, sender.Dispatch, 
            dispatch => Forward(source, context, new InterceptingDispatcher(sender, dispatch)));
    }

    Task Forward(string source, MessageContext context, IRawEndpoint dispatcher)
    {
        var intent = GetMesssageIntent(context);

        switch (intent)
        {
            case MessageIntentEnum.Subscribe:
            case MessageIntentEnum.Unsubscribe:
                return subscriptionForwarder.Forward(context, intent, dispatcher, forwarding);
            case MessageIntentEnum.Publish:
                return publishRouter.Route(context, dispatcher);
            case MessageIntentEnum.Send:
                return sendRouter.Route(context, dispatcher, forwarding, source);
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

    public async Task Initialize(Func<MessageContext, Task> onMessage)
    {
        this.onMessage = onMessage;
        sender = await rawConfig.Create().ConfigureAwait(false);
        routingConfiguration.PreparePubSub(sender, out publishRouter, out subscriptionReceiver, out subscriptionForwarder);
    }

    public async Task StartReceiving()
    {
        receiver = await sender.Start().ConfigureAwait(false);
    }

    public async Task StopReceiving()
    {
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

    RoutingConfiguration routingConfiguration;
    InterceptMessageForwarding interceptMethod;
    InterBridgeRoutingSettings forwarding;
    Func<MessageContext, Task> onMessage;
    IReceivingRawEndpoint receiver;
    IStartableRawEndpoint sender;
    IStoppableRawEndpoint stoppable;

    SubscriptionReceiver subscriptionReceiver;
    SubscriptionForwarder subscriptionForwarder;
    IPublishRouter publishRouter;

    ThrottlingRawEndpointConfig<T> rawConfig;
    SendRouter sendRouter;
    ReplyRouter replyRouter;
}
