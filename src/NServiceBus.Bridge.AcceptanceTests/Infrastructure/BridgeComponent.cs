using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Bridge;
using NServiceBus.Transport;

static class BridgeComponentExtensions
{
    public static IScenarioWithEndpointBehavior<TContext> With<TContext, TLeft, TRight>(this IScenarioWithEndpointBehavior<TContext> scenario, BridgeConfiguration<TLeft, TRight> config)
        where TContext : ScenarioContext
        where TLeft : TransportDefinition, new()
        where TRight : TransportDefinition, new()
    {
        return scenario.WithComponent(new BridgeComponent<TLeft, TRight>(config));
    }
}

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
        config.UseSubscriptionPersistece<InMemoryPersistence>(c => { });
        config.AutoCreateQueues();
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