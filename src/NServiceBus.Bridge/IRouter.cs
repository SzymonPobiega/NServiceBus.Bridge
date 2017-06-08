using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Raw;
using NServiceBus.Transport;

interface IRouter
{
    Task Route(MessageContext context, MessageIntentEnum intent, IRawEndpoint dispatcher);
}
