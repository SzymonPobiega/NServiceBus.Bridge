namespace NServiceBus.Bridge
{
    using System;
    using Routing;
    using Transport;
    using Persistence;

    public class BridgeConfiguration<TLeft, TRight>
        where TLeft : TransportDefinition, new()
        where TRight : TransportDefinition, new()
    {
        internal string LeftName;
        internal string RightName;
        Action<TransportExtensions<TLeft>> leftCustomization;
        Action<TransportExtensions<TRight>> rightCustomization;
        Action<EndpointConfiguration> subscriptionPersistenceConfig;
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

        public void UseSubscriptionPersistence<TPersistence>(Action<EndpointConfiguration, PersistenceExtensions<TPersistence>> subscriptionPersistenceConfiguration)
            where TPersistence : PersistenceDefinition
        {
            subscriptionPersistenceConfig = e =>
            {
                var persistence = e.UsePersistence<TPersistence>();
                subscriptionPersistenceConfiguration(e, persistence);
            };
        }

        public void InterceptForwarding(InterceptMessageForwarding interceptMethod)
        {
            interceptForwarding = interceptMethod ?? throw new ArgumentNullException(nameof(interceptMethod));
        }

        public void AutoCreateQueues(string identity = null)
        {
            autoCreateQueues = true;
            autoCreateQueuesIdentity = identity;
        }

        public void LimitMessageProcessingConcurrencyTo(int maximumConcurrency)
        {
            this.maximumConcurrency = maximumConcurrency;
        }

        public int ImmediateRetries { get; set; } = 5;
        public int DelayedRetries { get; set; } = 5;
        public int CircuitBreakerThreshold { get; set; } = 5;

        public InterBridgeRoutingSettings Forwarding { get; } = new InterBridgeRoutingSettings();

        public RawDistributionPolicy DistributionPolicy { get; } = new RawDistributionPolicy();

        public EndpointInstances EndpointInstances { get; } = new EndpointInstances();

        public IBridge Create()
        {
            return new Bridge<TLeft,TRight>(LeftName, RightName, autoCreateQueues, autoCreateQueuesIdentity, 
                EndpointInstances, subscriptionPersistenceConfig, DistributionPolicy, "poison",
                leftCustomization, rightCustomization, maximumConcurrency, interceptForwarding, Forwarding,
                ImmediateRetries, DelayedRetries, CircuitBreakerThreshold);
        }
    }
}
