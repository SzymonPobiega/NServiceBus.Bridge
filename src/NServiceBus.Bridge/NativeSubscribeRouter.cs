using System;
using System.Collections.Generic;
using System.Linq;
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
                var assemblyName = new AssemblyName(assembly);
                var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.ReflectionOnly);
                moduleBuilder = assemblyBuilder.DefineDynamicModule(assembly, assembly + ".dll");
                assemblies[assembly] = moduleBuilder;
            }
        }
        Type result;
        lock (types)
        {
            if (!types.TryGetValue(messageType, out result))
            {
                var nestedParts = nameAndNamespace.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);

                var typeBuilder = GetRootTypeBuilder(moduleBuilder, nestedParts[0]);

                for (var i = 1; i < nestedParts.Length; i++)
                {
                    var path = string.Join("+", nestedParts.Take(i+1));
                    typeBuilder = GetNestedTypeBuilder(typeBuilder, nestedParts[i], path);
                }
                result = typeBuilder.CreateType();
                types[messageType] = result;
            }
        }
        return result;
    }

    TypeBuilder GetRootTypeBuilder(ModuleBuilder moduleBuilder, string name)
    {
        if (typeBuilders.TryGetValue(name, out TypeBuilder builder))
        {
            return builder;
        }

        builder = moduleBuilder.DefineType(name, TypeAttributes.Public);
        typeBuilders[name] = builder;
        builder.CreateType();
        return builder;
    }

    TypeBuilder GetNestedTypeBuilder(TypeBuilder typeBuilder, string name, string path)
    {
        if (typeBuilders.TryGetValue(path, out TypeBuilder builder))
        {
            return builder;
        }

        builder = typeBuilder.DefineNestedType(name);
        typeBuilders[path] = builder;
        builder.CreateType();
        return builder;
    }

    Dictionary<string, ModuleBuilder> assemblies = new Dictionary<string, ModuleBuilder>();
    Dictionary<string, Type> types = new Dictionary<string, Type>();
    Dictionary<string, TypeBuilder> typeBuilders = new Dictionary<string, TypeBuilder>();
}