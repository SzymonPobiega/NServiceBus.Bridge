using System;
using NServiceBus.Transport;

namespace NServiceBus.Bridge
{
    public static class Bridge
    {
        public static BridgeUnderConstruction<TLeft> Between<TLeft>(string endpointBridgeEndName, Action<TransportExtensions<TLeft>> customization = null)
            where TLeft : TransportDefinition, new()
        {
            return new BridgeUnderConstruction<TLeft>(endpointBridgeEndName, customization);
        }
    }

    public class BridgeUnderConstruction<TLeft>
        where TLeft : TransportDefinition, new()
    {
        string leftName;
        Action<TransportExtensions<TLeft>> leftCustomization;

        public BridgeUnderConstruction(string leftName, Action<TransportExtensions<TLeft>> leftCustomization)
        {
            this.leftName = leftName;
            this.leftCustomization = leftCustomization;
        }

        public BridgeConfiguration<TLeft, TRight> And<TRight>(string endpointBridgeEndName,
            Action<TransportExtensions<TRight>> customization = null)
            where TRight : TransportDefinition, new()
        {
            return new BridgeConfiguration<TLeft, TRight>(leftName, endpointBridgeEndName, leftCustomization, customization);
        }
    }
}