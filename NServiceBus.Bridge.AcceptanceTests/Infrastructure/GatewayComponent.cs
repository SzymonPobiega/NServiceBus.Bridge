using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Bridge;
using NServiceBus.Transport;

class BridgeComponent<TLeft, TRight> : IComponentBehavior
    where TLeft : TransportDefinition, new()
    where TRight : TransportDefinition, new()
{
    BridgeConfiguration<TLeft, TRight> config;

    public BridgeComponent(BridgeConfiguration<TLeft, TRight> config)
    {
        this.config = config;
    }

    public Task<ComponentRunner> CreateRunner(RunDescriptor run)
    {
        var bridge = config.Create();
        
        return Task.FromResult<ComponentRunner>(new Runner(bridge, $"{config.LeftName}<->{config.RightName}"));
    }

    class Runner : ComponentRunner
    {
        IBridge bridge;

        public Runner(IBridge bridge, string name)
        {
            this.bridge = bridge;
            this.Name = name;
        }

        public override Task Start(CancellationToken token)
        {
            return bridge.Start();
        }

        public override Task Stop()
        {
            return bridge != null 
                ? bridge.Stop() 
                : Task.CompletedTask;
        }

        public override string Name { get; }
    }
}