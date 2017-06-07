using System.Threading.Tasks;
using NServiceBus.Transport;

interface IServiceControl
{
    Task Retry(IncomingMessage message);
}