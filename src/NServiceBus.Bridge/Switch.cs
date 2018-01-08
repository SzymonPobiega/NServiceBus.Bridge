namespace NServiceBus.Bridge
{
    using System.Linq;

    /// <summary>
    /// Allows creating switches.
    /// </summary>
    public static class Switch
    {
        /// <summary>
        /// Creates a new instance of a switch based on the provided configuration.
        /// </summary>
        /// <param name="config">Switch configuration.</param>
        public static ISwitch Create(SwitchConfiguration config)
        {
            var ports = config.PortFactories.Select(x => x()).ToArray();
            return new SwitchImpl(ports, config.PortTable);
        }
    }
}