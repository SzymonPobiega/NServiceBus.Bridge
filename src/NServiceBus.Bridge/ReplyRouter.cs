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
        string replyTo = null;
        if (!context.Headers.TryGetValue(Headers.CorrelationId, out var correlationId))
        {
            throw new UnforwardableMessageException($"The reply has to contain a '{Headers.CorrelationId}' header set by the bridge ramp when sending out the initial message.");
        }

        correlationId.DecodeTLV((t, v) =>
        {
            if (t == "reply-to")
            {
                replyTo = v;
            }
            if (t == "id")
            {
                context.Headers[Headers.CorrelationId] = v;
            }
        });

        if (replyTo == null)
        {
            throw new UnforwardableMessageException("The reply message does not contain \'reply-to\' correlation parameter required to route the message.");
        }

        var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
        var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(replyTo));
        return dispatcher.Dispatch(new TransportOperations(operation), context.TransportTransaction, context.Extensions);
    }
}