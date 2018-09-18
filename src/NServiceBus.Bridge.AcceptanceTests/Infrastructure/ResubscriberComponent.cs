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
    Resubscriber<TestTransport> resubscriber;

    public ResubscriberComponent(string inputQueue)
    {
        this.inputQueue = inputQueue;
    }

    public async Task<ComponentRunner> CreateRunner(RunDescriptor run)
    {
        resubscriber = await Resubscriber<TestTransport>.Create(inputQueue, TimeSpan.FromSeconds(3), extensions => { extensions.ConfigureNoNativePubSubBrokerA(); });
        return new Runner(resubscriber);
    }

    public Task InterceptMessageForwarding(string inputQueue, MessageContext message, Dispatch dispatchMethod, Func<Dispatch, Task> forwardMethod)
    {
        return resubscriber.InterceptMessageForwarding(inputQueue, message, dispatchMethod, forwardMethod);
    }

    class Runner : ComponentRunner
    {
        Resubscriber<TestTransport> resubscriber;

        public Runner(Resubscriber<TestTransport> resubscriber)
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
