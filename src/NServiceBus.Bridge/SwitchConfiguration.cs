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
            PortFactories.Add(() => portConfig.Create(typeGenerator, "poison", autoCreateQueues, autoCreateQueuesIdentity));
            return portConfig;
        }

        public void AutoCreateQueues(string identity = null)
        {
            autoCreateQueues = true;
            autoCreateQueuesIdentity = identity;
        }

        public Dictionary<string, string> PortTable { get; } = new Dictionary<string, string>();

        bool? autoCreateQueues;
        string autoCreateQueuesIdentity;
        RuntimeTypeGenerator typeGenerator = new RuntimeTypeGenerator();
        internal List<Func<IPort>> PortFactories = new List<Func<IPort>>();
    }
}