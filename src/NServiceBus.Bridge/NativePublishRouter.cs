using System;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;

class NativePublishRouter : IRouter
{
    RuntimeTypeGenerator typeGenerator;

    public NativePublishRouter(RuntimeTypeGenerator typeGenerator)
    {
        this.typeGenerator = typeGenerator;
    }

    public Task Route(MessageContext context, MessageIntentEnum intent, IRawEndpoint dispatcher)
    {
        if (!context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageTypes))
        {
            throw new UnforwardableMessageException("Message need to have 'NServiceBus.EnclosedMessageTypes' header in order to be routed.");
        }
        var types = messageTypes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

        var addressTag = new MulticastAddressTag(typeGenerator.GetType(types.First()));
        var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
        var operation = new TransportOperation(outgoingMessage, addressTag);

        return dispatcher.Dispatch(new TransportOperations(operation), context.TransportTransaction, context.Extensions);
    }
}