namespace NServiceBus.Bridge
{
    using System.Threading.Tasks;

    /// <summary>
    /// An instance of a switch.
    /// </summary>
    public interface ISwitch
    {
        /// <summary>
        /// Starts the switch.
        /// </summary>
        Task Start();

        /// <summary>
        /// Stops the switch.
        /// </summary>
        Task Stop();
    }
}