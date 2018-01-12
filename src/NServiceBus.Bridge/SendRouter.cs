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
    string localPortName;

    public SendRouter(EndpointInstances endpointInstances, RawDistributionPolicy distributionPolicy, string localPortName = null)
    {
        this.endpointInstances = endpointInstances;
        this.distributionPolicy = distributionPolicy;
        this.localPortName = localPortName;
    }

    public Task Route(MessageContext context, IRawEndpoint dispatcher, InterBridgeRoutingSettings routing)
    {
        if (!context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageTypes))
        {
            throw new UnforwardableMessageException($"Sent message does not contain the '{Headers.EnclosedMessageTypes}' header.");
        }
        var rootType = messageTypes.Split(new [] {';'}, StringSplitOptions.RemoveEmptyEntries).First();
        var rootTypeFullName = rootType.Split(new [] {','}, StringSplitOptions.RemoveEmptyEntries).First();

        if (routing.SendRouteTable.TryGetValue(rootTypeFullName, out var nextHop))
        {
            return Forward(context, dispatcher, nextHop);
        }

        if (!context.Headers.TryGetValue("NServiceBus.Bridge.DestinationEndpoint", out var destinationEndpoint))
        {
            throw new UnforwardableMessageException("Sent message does not contain the 'NServiceBus.Bridge.DestinationEndpoint' header.");
        }
        return Forward(context, dispatcher, destinationEndpoint);
    }

    Task Forward(MessageContext context, IRawEndpoint dispatcher, string destinationEndpoint)
    {
        var forwardedHeaders = new Dictionary<string, string>(context.Headers);

        var address = SelectDestinationAddress(destinationEndpoint, i => dispatcher.ToTransportAddress(LogicalAddress.CreateRemoteAddress(i)));

        if (forwardedHeaders.TryGetValue(Headers.ReplyToAddress, out var replyToHeader)
            && forwardedHeaders.TryGetValue(Headers.CorrelationId, out var correlationId))
        {
            // pipe-separated TLV format
            var newCorrelationId = $"id|{correlationId.Length}|{correlationId}|reply-to|{replyToHeader.Length}|{replyToHeader}";
            if (localPortName != null)
            {
                newCorrelationId += $"|port|{localPortName.Length}|{localPortName}";
            }
            forwardedHeaders[Headers.CorrelationId] = newCorrelationId;
        }
        forwardedHeaders[Headers.ReplyToAddress] = dispatcher.TransportAddress;

        var outgoingMessage = new OutgoingMessage(context.MessageId, forwardedHeaders, context.Body);
        var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(address));
        return dispatcher.Dispatch(new TransportOperations(operation), context.TransportTransaction, context.Extensions);
    }

    string SelectDestinationAddress(string endpoint, Func<EndpointInstance, string> resolveTransportAddress)
    {
        var candidates = endpointInstances.FindInstances(endpoint).Select(resolveTransportAddress).ToArray();
        var selected = distributionPolicy.GetDistributionStrategy(endpoint, DistributionStrategyScope.Send).SelectDestination(candidates);
        return selected;
    }
}
