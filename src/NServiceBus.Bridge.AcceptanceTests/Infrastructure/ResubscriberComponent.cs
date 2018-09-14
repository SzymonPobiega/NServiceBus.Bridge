using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Bridge;
using NServiceBus.Transport;

class ResubscriberComponent : IComponentBehavior
{
    string inputQueue;
    Resubscriber<MsmqTransport> resubscriber;

    public ResubscriberComponent(string inputQueue)
    {
        this.inputQueue = inputQueue;
    }

    public async Task<ComponentRunner> CreateRunner(RunDescriptor run)
    {
        resubscriber = await Resubscriber<MsmqTransport>.Create(inputQueue, TimeSpan.FromSeconds(3), extensions => { });
        return new Runner(resubscriber);
    }

    public Task InterceptMessageForwarding(string inputQueue, MessageContext message, Func<Task> forwardMethod)
    {
        return resubscriber.InterceptMessageForwarding(inputQueue, message, forwardMethod);
    }

    class Runner : ComponentRunner
    {
        Resubscriber<MsmqTransport> resubscriber;

        public Runner(Resubscriber<MsmqTransport> resubscriber)
        {
            this.resubscriber = resubscriber;
        }

        public override string Name => "Resubscriber";

        public override Task Start(CancellationToken token)
        {
            return resubscriber.Start();
        }

        public override Task Stop()
        {
            return resubscriber.Stop();
        }
    }
}
