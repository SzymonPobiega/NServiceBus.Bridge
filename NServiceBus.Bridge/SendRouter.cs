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

    public SendRouter(EndpointInstances endpointInstances, IDistributionPolicy distributionPolicy)
    {
        this.endpointInstances = endpointInstances;
        this.distributionPolicy = distributionPolicy;
    }

    public Task Route(MessageContext context, MessageIntentEnum intent, IRawEndpoint dispatcher)
    {
        string destinationEndpoint;
        if (!context.Headers.TryGetValue("NServiceBus.Bridge.DestinationEndpoint", out destinationEndpoint))
        {
            throw new UnforwardableMessageException("Sent message does not contain the 'NServiceBus.Bridge.DestinationEndpoint' header.");
        }
        var address = SelectDestinationAddress(destinationEndpoint, i => dispatcher.ToTransportAddress(LogicalAddress.CreateRemoteAddress(i)));

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
