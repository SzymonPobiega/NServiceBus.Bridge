namespace NServiceBus.Bridge
{
    using System;
    using System.Collections.Generic;
    using Transport;

    public class SwitchConfiguration
    {
        public PortConfiguration<T> AddPort<T>(string name, Action<TransportExtensions<T>> customization) 
            where T : TransportDefinition, new()
        {
            var portConfig = new PortConfiguration<T>(name, customization);
            PortFactories.Add(() => portConfig.Create(typeGenerator, "poison", autoCreateQueues, autoCreateQueuesIdentity, InterceptMethod, ImmediateRetries, DelayedRetries, CircuitBreakerThreshold));
            return portConfig;
        }

        public void AutoCreateQueues(string identity = null)
        {
            autoCreateQueues = true;
            autoCreateQueuesIdentity = identity;
        }

        public int ImmediateRetries { get; set; } = 5;
        public int DelayedRetries { get; set; } = 5;
        public int CircuitBreakerThreshold { get; set; } = 5;

        public void InterceptForwarding(InterceptMessageForwarding interceptMethod)
        {
            InterceptMethod = interceptMethod ?? throw new ArgumentNullException(nameof(interceptMethod));
        }

        public Dictionary<string, string> PortTable { get; } = new Dictionary<string, string>();

        internal InterceptMessageForwarding InterceptMethod = (queue, message, dispatch, forward) => forward(dispatch);
        bool? autoCreateQueues;
        string autoCreateQueuesIdentity;
        RuntimeTypeGenerator typeGenerator = new RuntimeTypeGenerator();
        internal List<Func<IPort>> PortFactories = new List<Func<IPort>>();
    }
}