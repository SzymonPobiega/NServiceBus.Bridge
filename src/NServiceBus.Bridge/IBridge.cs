using System.Threading.Tasks;

namespace NServiceBus.Bridge
{
    /// <summary>
    /// An instance of a bridge.
    /// </summary>
    public interface IBridge
    {
        /// <summary>
        /// Starts the bridge.
        /// </summary>
        Task Start();

        /// <summary>
        /// Stops the bridge.
        /// </summary>
        Task Stop();
    }
}