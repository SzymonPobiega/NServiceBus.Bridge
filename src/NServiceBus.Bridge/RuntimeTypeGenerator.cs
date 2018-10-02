using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

public class RuntimeTypeGenerator
{
    public void RegisterKnownType(Type knownType)
    {
        if (knownType == null)
        {
            throw new ArgumentNullException(nameof(knownType));
        }
        if (knownType.AssemblyQualifiedName == null)
        {
            throw new ArgumentException("Name of type is null", nameof(knownType));
        }

        var key = $"{knownType.FullName}, {knownType.Assembly.GetName().Name}"; //Do not include version number
        knownTypes[key] = knownType;
    }

    internal Type GetType(string messageType)
    {
        var parts = messageType.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        var nameAndNamespace = parts[0].Trim();
        var assembly = parts[1].Trim();

        var key = $"{nameAndNamespace}, {assembly}"; //Do not include version number
        if (knownTypes.TryGetValue(key, out var knownType))
        {
            return knownType;
        }

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
                    var path = string.Join("+", nestedParts.Take(i + 1));
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
        if (typeBuilders.TryGetValue(name, out var builder))
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
        if (typeBuilders.TryGetValue(path, out var builder))
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
    Dictionary<string, Type> knownTypes = new Dictionary<string, Type>();
    Dictionary<string, TypeBuilder> typeBuilders = new Dictionary<string, TypeBuilder>();
}