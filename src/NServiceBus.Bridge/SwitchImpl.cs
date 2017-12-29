using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Transport;

class SwitchImpl : ISwitch
{
    public SwitchImpl(IPort[] ports, Dictionary<string, string> routeTable, InterceptMessageForwarding configInterceptMethod)
    {
        this.routeTable = routeTable;
        this.configInterceptMethod = configInterceptMethod;
        this.ports = ports.ToDictionary(x => x.Name, x => x);
    }

    public async Task Start()
    {
        await Task.WhenAll(ports.Values.Select(p => p.Initialize((ctx, infra) => Forward(p.Name, ctx, infra)))).ConfigureAwait(false);
        await Task.WhenAll(ports.Values.Select(p => p.StartReceiving())).ConfigureAwait(false);
    }

    Task Forward(string incomingPort, MessageContext msg, PubSubInfrastructure inboundPubSubInfrastructure)
    {
        var intent = GetMesssageIntent(msg);
        string destinationPortName;
        IPort destinationPort;
        switch (intent)
        {
            case MessageIntentEnum.Subscribe:
            case MessageIntentEnum.Unsubscribe:
            case MessageIntentEnum.Send:
                destinationPortName = ResolveDestinationPort(msg);
                if (!ports.TryGetValue(destinationPortName, out destinationPort))
                {
                    throw new UnforwardableMessageException($"Port '{destinationPortName}' is not configured");
                }
                return destinationPort.Forward(incomingPort, msg, inboundPubSubInfrastructure);
            case MessageIntentEnum.Publish:
                return Task.WhenAll(ports.Values.Where(p => p.Name != incomingPort).Select(x => x.Forward(incomingPort, msg, inboundPubSubInfrastructure)));
            case MessageIntentEnum.Reply:
                destinationPortName = ResolveReplyDestinationPort(msg);
                if (!ports.TryGetValue(destinationPortName, out destinationPort))
                {
                    throw new UnforwardableMessageException($"Port '{destinationPortName}' is not configured");
                }
                return destinationPort.Forward(incomingPort, msg, inboundPubSubInfrastructure);
            default:
                throw new UnforwardableMessageException("Unroutable message intent: " + intent);
        }
    }

    static MessageIntentEnum GetMesssageIntent(MessageContext message)
    {
        var messageIntent = default(MessageIntentEnum);
        if (message.Headers.TryGetValue(Headers.MessageIntent, out var messageIntentString))
        {
            Enum.TryParse(messageIntentString, true, out messageIntent);
        }
        return messageIntent;
    }

    public async Task Stop()
    {
        await Task.WhenAll(ports.Values.Select(s => s.StopReceiving())).ConfigureAwait(false);
        await Task.WhenAll(ports.Values.Select(s => s.Stop())).ConfigureAwait(false);
    }

    string ResolveReplyDestinationPort(MessageContext context)
    {
        string destinationPort = null;
        if (!context.Headers.TryGetValue(Headers.CorrelationId, out var correlationId))
        {
            throw new UnforwardableMessageException($"The reply has to contain a '{Headers.CorrelationId}' header set by the bridge ramp when sending out the initial message.");
        }
        correlationId.DecodeTLV((t, v) =>
        {
            if (t == "port")
            {
                destinationPort = v;
            }
        });

        if (destinationPort == null)
        {
            throw new UnforwardableMessageException("The reply message does not contain \'port\' correlation parameter required to route the message.");
        }
        return destinationPort;
    }

    string ResolveDestinationPort(MessageContext context)
    {
        if (context.Headers.TryGetValue("NServiceBus.Bridge.DestinationPort", out var destinationPort))
        {
            return destinationPort;
        }
        string destinationEndpoint;
        if (!context.Headers.TryGetValue("NServiceBus.Bridge.DestinationEndpoint", out destinationEndpoint))
        {
            throw new UnforwardableMessageException("The message does not contain neither 'NServiceBus.Bridge.DestinationPort' header nor 'NServiceBus.Bridge.DestinationEndpoint' header.");
        }
        if (!routeTable.TryGetValue(destinationEndpoint, out destinationPort))
        {
            throw new UnforwardableMessageException($"The message does not contain 'NServiceBus.Bridge.DestinationPort' header and routing configuration does not have entry for endpoint '{destinationEndpoint}'.");
        }
        return destinationPort;
    }

    Dictionary<string, IPort> ports;
    Dictionary<string, string> routeTable;
    InterceptMessageForwarding configInterceptMethod;
}