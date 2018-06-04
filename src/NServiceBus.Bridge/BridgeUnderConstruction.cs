using System;
using NServiceBus.Transport;

namespace NServiceBus.Bridge
{
    /// <summary>
    /// Constructs bridges.
    /// </summary>
    public static class Bridge
    {
        /// <summary>
        /// Starts the process of constructing a bridge from the left side.
        /// </summary>
        /// <typeparam name="TLeft">Type of transport to use for the left side.</typeparam>
        /// <param name="endpointName">Endpoint name of the left side of the bridge.</param>
        /// <param name="customization">Callback for configuring the transport.</param>
        /// <returns></returns>
        public static BridgeUnderConstruction<TLeft> Between<TLeft>(string endpointName, Action<TransportExtensions<TLeft>> customization = null)
            where TLeft : TransportDefinition, new()
        {
            return new BridgeUnderConstruction<TLeft>(endpointName, customization);
        }
    }

    /// <summary>
    /// A partially constructed bridge.
    /// </summary>
    /// <typeparam name="TLeft">Type of transport for the left side.</typeparam>
    public class BridgeUnderConstruction<TLeft>
        where TLeft : TransportDefinition, new()
    {
        string leftName;
        Action<TransportExtensions<TLeft>> leftCustomization;

        internal BridgeUnderConstruction(string leftName, Action<TransportExtensions<TLeft>> leftCustomization)
        {
            this.leftName = leftName;
            this.leftCustomization = leftCustomization;
        }

        /// <summary>
        /// Finishes construction of the bridge.
        /// </summary>
        /// <typeparam name="TRight">Type of transport to use for the right side.</typeparam>
        /// <param name="endpointName">Endpoint name of the right side of the bridge.</param>
        /// <param name="customization">Callback for configuring the transport.</param>
        /// <returns>A bridge configuration.</returns>
        public BridgeConfiguration<TLeft, TRight> And<TRight>(string endpointName,
            Action<TransportExtensions<TRight>> customization = null)
            where TRight : TransportDefinition, new()
        {
            return new BridgeConfiguration<TLeft, TRight>(leftName, endpointName, leftCustomization, customization);
        }
    }
}