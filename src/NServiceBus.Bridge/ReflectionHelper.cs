using System;
using System.Reflection;
using NServiceBus;
using NServiceBus.Settings;
using NServiceBus.Transport;

static class ReflectionHelper
{
    public static IManageSubscriptions CreateSubscriptionManager(this TransportInfrastructure transportInfra)
    {
        var subscriptionInfra = transportInfra.ConfigureSubscriptionInfrastructure();
        var factoryProperty = typeof(TransportSubscriptionInfrastructure).GetProperty("SubscriptionManagerFactory", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var factoryInstance = (Func<IManageSubscriptions>)factoryProperty.GetValue(subscriptionInfra, new object[0]);
        return factoryInstance();
    }

    public static void RegisterReceivingComponent(this SettingsHolder settings, string localAddress)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance;
        var parameters = new[]
        {
            typeof(LogicalAddress),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(TransportTransactionMode),
            typeof(PushRuntimeSettings),
            typeof(bool)
        };
        var ctor = typeof(Endpoint).Assembly.GetType("NServiceBus.ReceiveConfiguration", true).GetConstructor(flags, null, parameters, null);

        var receiveConfig = ctor.Invoke(new object[] { null, localAddress, localAddress, null, null, null, false });
        settings.Set("NServiceBus.ReceiveConfiguration", receiveConfig);
    }
}