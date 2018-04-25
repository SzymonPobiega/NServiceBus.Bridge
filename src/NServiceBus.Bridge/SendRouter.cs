using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;

class SendRouter
{
    EndpointInstances endpointInstances;
    RawDistributionPolicy distributionPolicy;

    public SendRouter(EndpointInstances endpointInstances, RawDistributionPolicy distributionPolicy)
    {
        this.endpointInstances = endpointInstances;
        this.distributionPolicy = distributionPolicy;
    }

    public Task Route(MessageContext context, IRawEndpoint dispatcher, InterBridgeRoutingSettings routing, string sourcePort)
    {
        if (!context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageTypes))
        {
            throw new UnforwardableMessageException($"Sent message does not contain the '{Headers.EnclosedMessageTypes}' header.");
        }
        var rootType = messageTypes.Split(new [] {';'}, StringSplitOptions.RemoveEmptyEntries).First();
        var rootTypeFullName = rootType.Split(new [] {','}, StringSplitOptions.RemoveEmptyEntries).First();

        if (routing.TryGetDestination(context, rootTypeFullName, out var nextHops))
        {
            var ops = nextHops.Select(h => CreateTransportOperation(context, dispatcher, h, sourcePort)).ToArray();
            return dispatcher.Dispatch(new TransportOperations(ops), context.TransportTransaction, context.Extensions);
        }

        if (!context.Headers.TryGetValue("NServiceBus.Bridge.DestinationEndpoint", out var destinationEndpoint))
        {
            throw new UnforwardableMessageException("Sent message does not contain the 'NServiceBus.Bridge.DestinationEndpoint' header.");
        }

        var operation = CreateTransportOperation(context, dispatcher, destinationEndpoint, sourcePort);
        return dispatcher.Dispatch(new TransportOperations(operation), context.TransportTransaction, context.Extensions);
    }

    TransportOperation CreateTransportOperation(MessageContext context, IRawEndpoint dispatcher, string destinationEndpoint, string sourcePort)
    {
        var forwardedHeaders = new Dictionary<string, string>(context.Headers);

        var address = SelectDestinationAddress(destinationEndpoint, i => dispatcher.ToTransportAddress(LogicalAddress.CreateRemoteAddress(i)));

        if (forwardedHeaders.TryGetValue(Headers.ReplyToAddress, out var replyToHeader)
            && forwardedHeaders.TryGetValue(Headers.CorrelationId, out var correlationId))
        {
            // pipe-separated TLV format
            var newCorrelationId = $"id|{correlationId.Length}|{correlationId}|reply-to|{replyToHeader.Length}|{replyToHeader}";
            if (sourcePort != null)
            {
                newCorrelationId += $"|port|{sourcePort.Length}|{sourcePort}";
            }
            forwardedHeaders[Headers.CorrelationId] = newCorrelationId;
        }
        forwardedHeaders[Headers.ReplyToAddress] = dispatcher.TransportAddress;

        var outgoingMessage = new OutgoingMessage(context.MessageId, forwardedHeaders, context.Body);
        var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(address));
        return operation;
    }

    string SelectDestinationAddress(string endpoint, Func<EndpointInstance, string> resolveTransportAddress)
    {
        var candidates = endpointInstances.FindInstances(endpoint).Select(resolveTransportAddress).ToArray();
        var selected = distributionPolicy.GetDistributionStrategy(endpoint, DistributionStrategyScope.Send).SelectDestination(candidates);
        return selected;
    }
}
