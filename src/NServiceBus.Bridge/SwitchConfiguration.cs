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
            PortFactories.Add(() => portConfig.Create(TypeGenerator, "poison", autoCreateQueues, autoCreateQueuesIdentity));
            return portConfig;
        }

        public void AutoCreateQueues(string identity = null)
        {
            autoCreateQueues = true;
            autoCreateQueuesIdentity = identity;
        }

        public void InterceptForawrding(InterceptMessageForwarding interceptMethod)
        {
            InterceptMethod = interceptMethod ?? throw new ArgumentNullException(nameof(interceptMethod));
        }

        public RuntimeTypeGenerator TypeGenerator { get; } = new RuntimeTypeGenerator();

        public Dictionary<string, string> PortTable { get; } = new Dictionary<string, string>();

        internal InterceptMessageForwarding InterceptMethod = (queue, message, forward) => forward();
        bool? autoCreateQueues;
        string autoCreateQueuesIdentity;
        internal List<Func<IPort>> PortFactories = new List<Func<IPort>>();
    }
}