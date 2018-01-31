using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Extensibility;
using NServiceBus.Raw;
using NServiceBus.Settings;
using NServiceBus.Transport;

class InterceptingDispatcher : IRawEndpoint
{
    IRawEndpoint impl;
    Dispatch dispatchImpl;

    public InterceptingDispatcher(IRawEndpoint impl, Dispatch dispatchImpl)
    {
        this.impl = impl;
        this.dispatchImpl = dispatchImpl;
    }

    public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
    {
        return dispatchImpl(outgoingMessages, transaction, context);
    }

    public string ToTransportAddress(LogicalAddress logicalAddress) => impl.ToTransportAddress(logicalAddress);

    public string TransportAddress => impl.TransportAddress;

    public string EndpointName => impl.EndpointName;

    public ReadOnlySettings Settings => impl.Settings;
    public IManageSubscriptions SubscriptionManager => impl.SubscriptionManager;
}