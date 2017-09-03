using System;
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
        var hub = Switch.Create(config);

        return Task.FromResult<ComponentRunner>(new Runner(hub, "Switch"));
    }

    class Runner : ComponentRunner
    {
        ISwitch @switch;

        public Runner(ISwitch @switch, string name)
        {
            this.@switch = @switch;
            Name = name;
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