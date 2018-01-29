using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class NativeSubscriptionStorage : ISubscriptionStorage
{
    public Task Subscribe(Subscriber subscriber, MessageType messageType, ContextBag context)
    {
        //NOOP
        return Task.CompletedTask;
    }

    public Task Unsubscribe(Subscriber subscriber, MessageType messageType, ContextBag context)
    {
        //NOOP
        return Task.CompletedTask;
    }

    public Task<IEnumerable<Subscriber>> GetSubscriberAddressesForMessage(IEnumerable<MessageType> messageTypes, ContextBag context)
    {
        throw new NotImplementedException();
    }
}
