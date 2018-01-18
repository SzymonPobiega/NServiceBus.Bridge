using System;
using System.Reflection;
using NServiceBus.Transport;

static class SubscriptionManagerHelper
{
    public static IManageSubscriptions CreateSubscriptionManager(TransportInfrastructure transportInfra)
    {
        var subscriptionInfra = transportInfra.ConfigureSubscriptionInfrastructure();
        var factoryProperty = typeof(TransportSubscriptionInfrastructure).GetProperty("SubscriptionManagerFactory", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var factoryInstance = (Func<IManageSubscriptions>)factoryProperty.GetValue(subscriptionInfra, new object[0]);
        return factoryInstance();
    }
}