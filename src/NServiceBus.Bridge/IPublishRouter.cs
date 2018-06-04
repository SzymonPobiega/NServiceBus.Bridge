using System.Threading.Tasks;
using NServiceBus.Raw;
using NServiceBus.Transport;

interface IPublishRouter
{
    Task Route(MessageContext context, IRawEndpoint dispatcher);
}
