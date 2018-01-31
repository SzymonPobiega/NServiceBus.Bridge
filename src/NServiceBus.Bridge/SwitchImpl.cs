using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Transport;

class SwitchImpl : IBridge
{
    public SwitchImpl(IPort[] ports, Func<string, MessageContext, string> resolveDestinationPort)
    {
        this.resolveDestinationPort = resolveDestinationPort;
        this.ports = ports.ToDictionary(x => x.Name, x => x);
    }

    public async Task Start()
    {
        await Task.WhenAll(ports.Values.Select(p => p.Initialize(ctx => Forward(p.Name, ctx)))).ConfigureAwait(false);
        await Task.WhenAll(ports.Values.Select(p => p.StartReceiving())).ConfigureAwait(false);
    }

    Task Forward(string incomingPort, MessageContext msg)
    {
        var intent = GetMesssageIntent(msg);
        string destinationPortName;
        IPort destinationPort;
        switch (intent)
        {
            case MessageIntentEnum.Subscribe:
            case MessageIntentEnum.Unsubscribe:
            case MessageIntentEnum.Send:
                destinationPortName = resolveDestinationPort(incomingPort, msg);
                if (!ports.TryGetValue(destinationPortName, out destinationPort))
                {
                    throw new UnforwardableMessageException($"Port '{destinationPortName}' is not configured");
                }
                return destinationPort.Forward(incomingPort, msg);
            case MessageIntentEnum.Publish:
                return Task.WhenAll(ports.Values.Where(p => p.Name != incomingPort).Select(x => x.Forward(incomingPort, msg)));
            case MessageIntentEnum.Reply:
                destinationPortName = ResolveReplyDestinationPort(msg);
                if (!ports.TryGetValue(destinationPortName, out destinationPort))
                {
                    throw new UnforwardableMessageException($"Port '{destinationPortName}' is not configured");
                }
                return destinationPort.Forward(incomingPort, msg);
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

    

    Dictionary<string, IPort> ports;
    Func<string, MessageContext, string> resolveDestinationPort;
}