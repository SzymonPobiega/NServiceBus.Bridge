using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Bridge;


class SwitchComponent : IComponentBehavior
{
    SwitchConfiguration config;

    public SwitchComponent(Func<SwitchConfiguration> config)
    {
        this.config = config();
    }

    public Task<ComponentRunner> CreateRunner(RunDescriptor run)
    {
        config.AutoCreateQueues();

        var newFactories = new List<Func<IPort>>();

        foreach (var factory in config.PortFactories)
        {
            IPort NewFactory()
            {
                var port = factory();
                var portType = port.GetType();
                var portTransportType = portType.GetGenericArguments()[0];
                run.OnTestCompleted(summary => CleanUpHelper.CleanupQueue(summary, port.Name, portTransportType));
                return port;
            }
            newFactories.Add(NewFactory);
        }

        config.PortFactories = newFactories;
        var @switch = Switch.Create(config);
        return Task.FromResult<ComponentRunner>(new Runner(@switch, "Switch"));
    }

    class Runner : ComponentRunner
    {
        IBridge @switch;

        public Runner(IBridge @switch, string name)
        {
            this.@switch = @switch;
            this.Name = name;
        }

        public override Task Start(CancellationToken token)
        {
            return @switch.Start();
        }

        public override Task Stop()
        {
            return @switch != null 
                ? @switch.Stop() 
                : Task.CompletedTask;
        }

        public override string Name { get; }
    }
}