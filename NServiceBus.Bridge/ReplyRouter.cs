using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;

class ReplyRouter : IRouter
{
    public Task Route(MessageContext context, MessageIntentEnum intent, IRawEndpoint dispatcher)
    {
        if (!context.Headers.TryGetValue("NServiceBus.Bridge.DestinationAddress", out string destinationAddress))
        {
            throw new UnforwardableMessageException("The reply has to contain a 'NServiceBus.Bridge.DestinationAddress' header.");
        }
        context.Headers.Remove("NServiceBus.Bridge.DestinationAddress");
        var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
        var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(destinationAddress));
        return dispatcher.Dispatch(new TransportOperations(operation), context.TransportTransaction, context.Extensions);
    }
}