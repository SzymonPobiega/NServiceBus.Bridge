namespace NServiceBus.Bridge
{
    using System;
    using System.Threading.Tasks;
    using Transport;

    /// <summary>
    /// Allows to execute arbitrary code before forwarding a message.
    /// </summary>
    /// <returns></returns>
    public delegate Task InterceptMessageForwarding(string inputQueue, MessageContext message, Func<Task> forwardMethod);
}