using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Raw;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class NativeSubscribeRouter : SubscribeRouter
{
    IManageSubscriptions subscriptionManager;

    public NativeSubscribeRouter(ISubscriptionStorage subscriptionStorage, IManageSubscriptions subscriptionManager)
        : base(subscriptionStorage)
    {
        this.subscriptionManager = subscriptionManager;
    }

    protected override Task ForwardSubscribe(Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher)
    {
        var type = GetType(messageType);
        return subscriptionManager.Subscribe(type, new ContextBag());
    }

    protected override Task ForwardUnsubscribe(Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher)
    {
        var type = GetType(messageType);
        return subscriptionManager.Unsubscribe(type, new ContextBag());
    }

    public Type GetType(string messageType)
    {
        var parts = messageType.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        var nameAndNamespace = parts[0];
        var assembly = parts[1];

        ModuleBuilder moduleBuilder;
        lock (assemblies)
        {
            if (!assemblies.TryGetValue(assembly, out moduleBuilder))
            {
                var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(assembly), AssemblyBuilderAccess.ReflectionOnly);
                moduleBuilder = assemblyBuilder.DefineDynamicModule(assembly, assembly + ".dll");
                assemblies[assembly] = moduleBuilder;
            }
        }
        Type result;
        lock (types)
        {
            if (!types.TryGetValue(messageType, out result))
            {
                var typeBuilder = moduleBuilder.DefineType(nameAndNamespace, TypeAttributes.Public);
                result = typeBuilder.CreateType();
                types[messageType] = result;
            }
        }
        return result;
    }

    Dictionary<string, ModuleBuilder> assemblies = new Dictionary<string, ModuleBuilder>();
    Dictionary<string, Type> types = new Dictionary<string, Type>();
}