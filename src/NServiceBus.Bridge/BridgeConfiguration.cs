namespace NServiceBus.Bridge
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
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
        InterceptMessageForwarding interceptMethod = (queue, message, forward) => forward();

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
            this.subscriptionPersistenceConfig = e =>
            {
                var persistence = e.UsePersistence<TPersistence>();
                subscriptionPersistenceConfiguration(e, persistence);
            };
        }

        public void InterceptForwarding(InterceptMessageForwarding interceptMethod)
        {
            this.interceptMethod = interceptMethod ?? throw new ArgumentNullException(nameof(interceptMethod));
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

        public RawDistributionPolicy DistributionPolicy { get; } = new RawDistributionPolicy();

        public EndpointInstances EndpointInstances { get; } = new EndpointInstances();

        public IBridge Create()
        {
            return new Bridge<TLeft,TRight>(LeftName, RightName, autoCreateQueues, autoCreateQueuesIdentity, 
                EndpointInstances, subscriptionPersistenceConfig, DistributionPolicy, "poison",
                leftCustomization, rightCustomization, maximumConcurrency, interceptMethod);
        }
    }

    /// <summary>
    /// Determines which instance of a given endpoint a message is to be sent.
    /// </summary>
    public abstract class RawDistributionStrategy
    {
        /// <summary>
        /// Creates a new <see cref="DistributionStrategy"/>.
        /// </summary>
        /// <param name="endpoint">The name of the endpoint this distribution strategy resolves instances for.</param>
        /// <param name="scope">The scope for this strategy.</param>
        protected RawDistributionStrategy(string endpoint, DistributionStrategyScope scope)
        {
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            Scope = scope;
        }

        /// <summary>
        /// Selects a destination instance for a message from all known addresses of a logical endpoint.
        /// </summary>
        public abstract string SelectDestination(string[] candidates);

        /// <summary>
        /// The name of the endpoint this distribution strategy resolves instances for.
        /// </summary>
        public string Endpoint { get; }

        /// <summary>
        /// The scope of this strategy.
        /// </summary>
        public DistributionStrategyScope Scope { get; }
    }

    public class RawDistributionPolicy
    {
        private ConcurrentDictionary<Tuple<string, DistributionStrategyScope>, RawDistributionStrategy> configuredStrategies = new ConcurrentDictionary<Tuple<string, DistributionStrategyScope>, RawDistributionStrategy>();

        /// <summary>Sets the distribution strategy for a given endpoint.</summary>
        /// <param name="distributionStrategy">Distribution strategy to be used.</param>
        public void SetDistributionStrategy(RawDistributionStrategy distributionStrategy)
        {
            if (distributionStrategy == null)
            {
                throw new ArgumentNullException(nameof(distributionStrategy));
            }
            this.configuredStrategies[Tuple.Create(distributionStrategy.Endpoint, distributionStrategy.Scope)] = distributionStrategy;
        }

        internal RawDistributionStrategy GetDistributionStrategy(string endpointName, DistributionStrategyScope scope)
        {
            return configuredStrategies.GetOrAdd(Tuple.Create(endpointName, scope), key => new SingleInstanceRoundRobinRawDistributionStrategy(key.Item1, key.Item2));
        }
    }

    public class SingleInstanceRoundRobinRawDistributionStrategy : RawDistributionStrategy
    {
        long index = -1;

        /// <summary>
        /// Creates a new <see cref="T:NServiceBus.Routing.SingleInstanceRoundRobinDistributionStrategy" /> instance.
        /// </summary>
        /// <param name="endpoint">The name of the endpoint this distribution strategy resolves instances for.</param>
        /// <param name="scope">The scope for this strategy.</param>
        public SingleInstanceRoundRobinRawDistributionStrategy(string endpoint, DistributionStrategyScope scope)
            : base(endpoint, scope)
        {
        }

        /// <summary>
        /// Selects a destination instance for a message from all known addresses of a logical endpoint.
        /// </summary>
        public override string SelectDestination(string[] candidates)
        {
            if (candidates.Length == 0)
            {
                return null;
            }
            var i = Interlocked.Increment(ref index);
            var result = candidates[(int)(i % candidates.Length)];
            return result;
        }
    }
}
