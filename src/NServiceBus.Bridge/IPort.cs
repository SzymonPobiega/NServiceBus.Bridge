using System;
using System.Threading.Tasks;
using NServiceBus.Transport;

interface IPort
{
    string Name { get; }
    Task Forward(MessageContext context, PubSubInfrastructure inboundPubSubInfra);
    Task Initialize(Func<MessageContext, PubSubInfrastructure, Task> onMessage);
    Task StartReceiving();
    Task StopReceiving();
    Task Stop();
}