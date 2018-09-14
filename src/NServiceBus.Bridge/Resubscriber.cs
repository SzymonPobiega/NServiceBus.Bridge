namespace NServiceBus.Bridge
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Logging;
    using NServiceBus;
    using Raw;
    using Routing;
    using Transport;

    /// <summary>
    /// Helper class that allows Azure Service Bus Endpoint Oriented Topology to keep subscribed between restrarts.
    /// </summary>
    public class Resubscriber<T>
        where T : TransportDefinition, new()
    {
        static ILog logger = LogManager.GetLogger<Resubscriber<T>>();

        readonly string inputQueueName;
        const string ResubscriptionIdHeader = "NServiceBus.Bridge.ResubscriptionId";
        const string ResubscriptionTimestampHeader = "NServiceBus.Bridge.ResubscriptionTimestamp";

        Dictionary<Tuple<string, string>, Tuple<string, DateTime>> idMap = new Dictionary<Tuple<string, string>, Tuple<string, DateTime>>();
        RawEndpointConfiguration config;
        IStartableRawEndpoint endpoint;
        IReceivingRawEndpoint stoppable;

        Resubscriber(string inputQueueName, TimeSpan delay, Action<TransportExtensions<T>> configureTransport)
        {
            this.inputQueueName = inputQueueName;
            config = RawEndpointConfiguration.Create(inputQueueName + ".Resubscriber",
                async (context, dispatcher) =>
                {
                    context.Headers.TryGetValue(Headers.SubscriptionMessageType, out var messageTypeString);
                    if (!context.Headers.TryGetValue(Headers.SubscriberTransportAddress, out var subscriberAddress))
                    {
                        subscriberAddress = context.Headers[Headers.ReplyToAddress];
                    }

                    var key = Tuple.Create(subscriberAddress, messageTypeString);
                    var resubscriptionId = context.Headers[ResubscriptionIdHeader];
                    var resubscriptionTimestamp = DateTime.Parse(context.Headers[ResubscriptionTimestampHeader]);
                    if (idMap.ContainsKey(key))
                    {
                        var valuePair = idMap[key];
                        if (valuePair.Item1 != resubscriptionId && resubscriptionTimestamp < valuePair.Item2)
                        {
                            //If we already processed a newer subscribe/unsubscribe message for this -> ignore
                            return;
                        }
                        //We've seen that same message before. Let's pause the resubscription for some time
                        await Task.Delay(delay);
                    }

                    //Send it to the bridge to re-subscribe
                    var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
                    var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(inputQueueName));

                    await dispatcher.Dispatch(new TransportOperations(operation), context.TransportTransaction, context.Extensions).ConfigureAwait(false);
                    logger.Debug("Moved subscription message back to the queue.");
                    idMap[key] = Tuple.Create(resubscriptionId, resubscriptionTimestamp);
                }, "poison");
            config.AutoCreateQueue();
            config.LimitMessageProcessingConcurrencyTo(1);
            var transport = config.UseTransport<T>();
            configureTransport(transport);
        }

        public static async Task<Resubscriber<T>> Create(string inputQueueName, TimeSpan delay, Action<TransportExtensions<T>> configureTransport)
        {
            var resubscriber = new Resubscriber<T>(inputQueueName, delay, configureTransport);
            await resubscriber.Create().ConfigureAwait(false);
            return resubscriber;
        }

        async Task Create()
        {
            endpoint = await RawEndpoint.Create(config).ConfigureAwait(false);
        }

        public async Task InterceptMessageForwarding(string inputQueue, MessageContext message, Func<Task> forwardMethod)
        {
            await forwardMethod().ConfigureAwait(false);

            if (inputQueue == inputQueueName
                && message.Headers.TryGetValue(Headers.MessageIntent, out var intent)
                && (intent == MessageIntentEnum.Subscribe.ToString() || intent == MessageIntentEnum.Unsubscribe.ToString()))
            {
                logger.Debug("Detected subscription message. ");
                await Enqueue(message).ConfigureAwait(false);
            }
        }

        async Task Enqueue(MessageContext message)
        {
            var outgoingHeaders = new Dictionary<string, string>(message.Headers);
            if (!outgoingHeaders.ContainsKey(ResubscriptionIdHeader))
            {
                outgoingHeaders[ResubscriptionIdHeader] = Guid.NewGuid().ToString();
                outgoingHeaders[ResubscriptionTimestampHeader] = DateTime.UtcNow.ToString("O");
            }
            var outgoingMessage = new OutgoingMessage(message.MessageId, outgoingHeaders, message.Body);
            var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(endpoint.TransportAddress));

            await endpoint.Dispatch(new TransportOperations(operation), message.TransportTransaction, message.Extensions)
                .ConfigureAwait(false);
            logger.Debug("Moved subscription message back to the resubscriber queue.");
        }

        public async Task Start()
        {
            stoppable = await endpoint.Start().ConfigureAwait(false);
        }

        public async Task Stop()
        {
            await stoppable.Stop().ConfigureAwait(false);
        }
    }
}