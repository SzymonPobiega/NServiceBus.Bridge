using System;
using System.Threading.Tasks;
using NServiceBus.Transport;

interface IPort
{
    string Name { get; }
    Task Forward(string source, MessageContext context);
    Task Initialize(Func<MessageContext, Task> onMessage);
    Task StartReceiving();
    Task StopReceiving();
    Task Stop();
}