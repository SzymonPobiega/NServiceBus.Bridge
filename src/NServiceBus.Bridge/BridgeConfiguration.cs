namespace NServiceBus.Bridge
{
    using System;
    using Routing;
    using Transport;
    using Persistence;
    using Unicast.Subscriptions.MessageDrivenSubscriptions;

    /// <summary>
    /// Configures the bridge that can forward messages between different transports. A bridge is almost symmetrical in the sense that messages
    /// can be moved both from left to right and from right to left. The only exception is the forwarding <see cref="Forwarding"/> which is only applied
    /// when moving messages from left to right.
    /// </summary>
    /// <typeparam name="TLeft">Type of transport for the left side of the bridge.</typeparam>
    /// <typeparam name="TRight">Type of transport for the right side of the bridge.</typeparam>
    public class BridgeConfiguration<TLeft, TRight>
        where TLeft : TransportDefinition, new()
        where TRight : TransportDefinition, new()
    {
        internal string LeftName;
        internal string RightName;
        Action<TransportExtensions<TLeft>> leftCustomization;
        Action<TransportExtensions<TRight>> rightCustomization;
        ISubscriptionStorage subscriptionStorage;
        bool autoCreateQueues;
        string autoCreateQueuesIdentity;
        int? maximumConcurrency;
        InterceptMessageForwarding interceptForwarding = (queue, message, dispatch, forward) => forward(dispatch);

        internal BridgeConfiguration(string leftName, string rightName, Action<TransportExtensions<TLeft>> leftCustomization, Action<TransportExtensions<TRight>> rightCustomization)
        {
            LeftName = leftName;
            RightName = rightName;
            this.leftCustomization = leftCustomization;
            this.rightCustomization = rightCustomization;
        }

        /// <summary>
        /// Configures the bridge to use specified subscription persistence.
        /// </summary>
        public void UseSubscriptionPersistence(ISubscriptionStorage subscriptionStorage)
        {
            this.subscriptionStorage = subscriptionStorage;
        }

        /// <summary>
        /// Configures the bridge to invoke a provided callback when processing messages.
        /// </summary>
        /// <param name="interceptMethod">Callback to be invoked.</param>
        public void InterceptForwarding(InterceptMessageForwarding interceptMethod)
        {
            interceptForwarding = interceptMethod ?? throw new ArgumentNullException(nameof(interceptMethod));
        }

        /// <summary>
        /// Configures the bridge to automatically create a queue when starting up.
        /// </summary>
        /// <param name="identity">Identity to use when creating the queue.</param>
        public void AutoCreateQueues(string identity = null)
        {
            autoCreateQueues = true;
            autoCreateQueuesIdentity = identity;
        }

        /// <summary>
        /// Limits the processing concurrency of the bridge to a given value. Applies to both sides.
        /// </summary>
        /// <param name="maximumConcurrency">Maximum level of concurrency for the port's transport.</param>
        public void LimitMessageProcessingConcurrencyTo(int maximumConcurrency)
        {
            this.maximumConcurrency = maximumConcurrency;
        }

        /// <summary>
        /// Gets or sets the number of immediate retries to use when resolving failures during forwarding. Applies to both sides.
        /// </summary>
        public int ImmediateRetries { get; set; } = 5;

        /// <summary>
        /// Gets or sets the number of delayed retries to use when resolving failures during forwarding. Applies to both sides.
        /// </summary>
        public int DelayedRetries { get; set; } = 5;

        /// <summary>
        /// Gets or sets the number of consecutive failures required to trigger the throttled mode. Applies to both sides.
        /// </summary>
        public int CircuitBreakerThreshold { get; set; } = 5;

        /// <summary>
        /// Configures forwarding of messages when sending from left to the right. If configured, messages of certain types are forwarded to another
        /// bridge instead of being delivered to the destination endpoint on the right side of this bridge.
        /// </summary>
        public InterBridgeRoutingSettings Forwarding { get; } = new InterBridgeRoutingSettings();

        /// <summary>
        /// Distribution policy of the bridge. Applies to both sides.
        /// </summary>
        public RawDistributionPolicy DistributionPolicy { get; } = new RawDistributionPolicy();

        /// <summary>
        /// Physical routing settings of the bridge. Applies to both sides.
        /// </summary>
        public EndpointInstances EndpointInstances { get; } = new EndpointInstances();

        /// <summary>
        /// Creates a new instance of a bridge based on this configuration.
        /// </summary>
        public IBridge Create()
        {
            return new Bridge<TLeft,TRight>(LeftName, RightName, autoCreateQueues, autoCreateQueuesIdentity, 
                EndpointInstances, subscriptionStorage, DistributionPolicy, "poison",
                leftCustomization, rightCustomization, maximumConcurrency, interceptForwarding, Forwarding,
                ImmediateRetries, DelayedRetries, CircuitBreakerThreshold);
        }
    }
}
