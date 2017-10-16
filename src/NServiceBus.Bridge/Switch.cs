namespace NServiceBus.Bridge
{
    using System.Linq;

    public static class Switch
    {
        public static ISwitch Create(SwitchConfiguration config)
        {
            var ports = config.PortFactories.Select(x => x()).ToArray();
            return new SwitchImpl(ports, config.PortTable, config.InterceptMethod);
        }
    }
}