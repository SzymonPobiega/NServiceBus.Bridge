using System;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;

class SendRouter : IRouter
{
    EndpointInstances endpointInstances;
    IDistributionPolicy distributionPolicy;
    string localPortName;

    public SendRouter(EndpointInstances endpointInstances, IDistributionPolicy distributionPolicy, string localPortName = null)
    {
        this.endpointInstances = endpointInstances;
        this.distributionPolicy = distributionPolicy;
        this.localPortName = localPortName;
    }

    public Task Route(MessageContext context, MessageIntentEnum intent, IRawEndpoint dispatcher)
    {
        if (!context.Headers.TryGetValue("NServiceBus.Bridge.DestinationEndpoint", out var destinationEndpoint))
        {
            throw new UnforwardableMessageException("Sent message does not contain the 'NServiceBus.Bridge.DestinationEndpoint' header.");
        }
        var address = SelectDestinationAddress(destinationEndpoint, i => dispatcher.ToTransportAddress(LogicalAddress.CreateRemoteAddress(i)));

        if (context.Headers.TryGetValue(Headers.ReplyToAddress, out var replyToHeader)
            && context.Headers.TryGetValue(Headers.CorrelationId, out var correlationId))
        {
            // pipe-separated TLV format
            var newCorrelationId = $"id|{correlationId.Length}|{correlationId}|reply-to|{replyToHeader.Length}|{replyToHeader}";
            if (localPortName != null)
            {
                newCorrelationId += $"|port|{localPortName.Length}|{localPortName}";
            }
            context.Headers[Headers.CorrelationId] = newCorrelationId;
        }
        context.Headers[Headers.ReplyToAddress] = dispatcher.TransportAddress;

        var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
        var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(address));
        return dispatcher.Dispatch(new TransportOperations(operation), context.TransportTransaction, context.Extensions);
    }

    string SelectDestinationAddress(string endpoint, Func<EndpointInstance, string> resolveTransportAddress)
    {
        var candidates = endpointInstances.FindInstances(endpoint).Select(resolveTransportAddress).ToArray();
        var selected = distributionPolicy.GetDistributionStrategy(endpoint, DistributionStrategyScope.Send).SelectReceiver(candidates);
        return selected;
    }
}
