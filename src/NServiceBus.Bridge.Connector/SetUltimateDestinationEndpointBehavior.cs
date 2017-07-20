using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus.Pipeline;

class RoutingHeadersBehavior : Behavior<IOutgoingSendContext>
{
    Dictionary<Type, string> routeTable;
    Dictionary<string, string> portTable;

    public RoutingHeadersBehavior(Dictionary<Type, string> routeTable, Dictionary<string, string> portTable)
    {
        this.routeTable = routeTable;
        this.portTable = portTable;
    }

    public override Task Invoke(IOutgoingSendContext context, Func<Task> next)
    {
        if (routeTable.TryGetValue(context.Message.MessageType, out string ultimateDestination))
        {
            context.Headers["NServiceBus.Bridge.DestinationEndpoint"] = ultimateDestination;
            if (portTable.TryGetValue(ultimateDestination, out string portName))
            {
                context.Headers["NServiceBus.Bridge.DestinationPort"] = portName;
            }
        }
        return next();
    }
}