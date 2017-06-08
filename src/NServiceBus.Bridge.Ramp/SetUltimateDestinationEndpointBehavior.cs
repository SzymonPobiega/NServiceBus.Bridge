using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using NServiceBus.Pipeline;

class SetUltimateDestinationEndpointBehavior : Behavior<IOutgoingSendContext>
{
    Dictionary<Type, string> routeTable;

    public SetUltimateDestinationEndpointBehavior(Dictionary<Type, string> routeTable)
    {
        this.routeTable = routeTable;
    }

    public override Task Invoke(IOutgoingSendContext context, Func<Task> next)
    {
        string ultimateDestination;
        if (routeTable.TryGetValue(context.Message.MessageType, out ultimateDestination))
        {
            context.Headers["NServiceBus.Bridge.DestinationEndpoint"] = ultimateDestination;
        }
        return next();
    }
}