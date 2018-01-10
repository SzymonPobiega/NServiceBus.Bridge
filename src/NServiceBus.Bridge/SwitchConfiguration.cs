namespace NServiceBus.Bridge
{
    using System;
    using System.Collections.Generic;
    using Transport;

    /// <summary>
    /// Configures the switch.
    /// </summary>
    public class SwitchConfiguration
    {
        /// <summary>
        /// Adds a new port to the switch.
        /// </summary>
        /// <typeparam name="T">Transport to use for this port.</typeparam>
        /// <param name="name">Name of the port.</param>
        /// <param name="customization">A callback for customizing the transport settings.</param>
        public PortConfiguration<T> AddPort<T>(string name, Action<TransportExtensions<T>> customization) 
            where T : TransportDefinition, new()
        {
            var portConfig = new PortConfiguration<T>(name, customization);
            PortFactories.Add(() => portConfig.Create(typeGenerator, "poison", autoCreateQueues, autoCreateQueuesIdentity, InterceptMethod, ImmediateRetries, DelayedRetries, CircuitBreakerThreshold));
            return portConfig;
        }

        /// <summary>
        /// Configures the switch to automatically create a queue when starting up.
        /// </summary>
        /// <param name="identity">Identity to use when creating the queue.</param>
        public void AutoCreateQueues(string identity = null)
        {
            autoCreateQueues = true;
            autoCreateQueuesIdentity = identity;
        }

        /// <summary>
        /// Gets or sets the number of immediate retries to use when resolving failures during forwarding.
        /// </summary>
        public int ImmediateRetries { get; set; } = 5;

        /// <summary>
        /// Gets or sets the number of delayed retries to use when resolving failures during forwarding.
        /// </summary>
        public int DelayedRetries { get; set; } = 5;

        /// <summary>
        /// Gets or sets the number of consecutive failures required to trigger the throttled mode.
        /// </summary>
        public int CircuitBreakerThreshold { get; set; } = 5;

        /// <summary>
        /// Configures the switch to invoke a provided callback when processing messages.
        /// </summary>
        /// <param name="interceptMethod">Callback to be invoked.</param>
        public void InterceptForwarding(InterceptMessageForwarding interceptMethod)
        {
            InterceptMethod = interceptMethod ?? throw new ArgumentNullException(nameof(interceptMethod));
        }

        /// <summary>
        /// Routing table of the switch. Maps endpoints to ports.
        /// </summary>
        public Dictionary<string, string> PortTable { get; } = new Dictionary<string, string>();

        internal InterceptMessageForwarding InterceptMethod = (queue, message, dispatchLocal, dispatchForward, forward) => forward(dispatchForward);
        bool? autoCreateQueues;
        string autoCreateQueuesIdentity;
        RuntimeTypeGenerator typeGenerator = new RuntimeTypeGenerator();
        internal List<Func<IPort>> PortFactories = new List<Func<IPort>>();
    }
}