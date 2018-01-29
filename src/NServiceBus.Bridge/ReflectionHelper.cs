using System;
using System.Reflection;
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
}