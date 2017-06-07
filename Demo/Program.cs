using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Bridge;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Settings;

namespace Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            Start().GetAwaiter().GetResult();
        }

        static async Task Start()
        {
            var bridgeConfig = Bridge.Between<MsmqTransport>("Left").And<RabbitMQTransport>("Right", t => t.ConnectionString("host=localhost"));
            bridgeConfig.AutoCreateQueues();

            var bridge = bridgeConfig.Create();
            await bridge.Start();

            var someEndpoint = await Endpoint.Start(CreateSomeEndpointConfig());
            var otherEndpoint = await Endpoint.Start(CreateOtherEndpointConfig());

            while (true)
            {
                Console.WriteLine("Press <enter> to send a message and publish a command");
                Console.ReadLine();

                await someEndpoint.Send(new MyCommand());
                await otherEndpoint.Send(new MyCommand());

                await someEndpoint.Publish(new SomeEvent());
                await otherEndpoint.Publish(new OtherEvent());
            }
        }

        static EndpointConfiguration CreateSomeEndpointConfig()
        {
            var config = new EndpointConfiguration("Some");

            config.UsePersistence<InMemoryPersistence>();
            config.SendFailedMessagesTo("error");

            config.GetSettings().Set("RabbitMQ.RoutingTopologySupportsDelayedDelivery", true);

            var routing = config.UseTransport<MsmqTransport>().Routing();
            var ramp = routing.UseBridgeRamp("Left");
            ramp.RouteToEndpoint(typeof(MyCommand), "Other");
            ramp.RegisterPublisher(typeof(OtherEvent), "Other");

            return config;
        }

        static EndpointConfiguration CreateOtherEndpointConfig()
        {
            var config = new EndpointConfiguration("Other");

            config.UsePersistence<InMemoryPersistence>();
            config.SendFailedMessagesTo("error");

            //config.GetSettings().Set("RabbitMQ.RoutingTopologySupportsDelayedDelivery", true);
            //var routing = config.UseTransport<MsmqTransport>().Routing();

            var routing = config.UseTransport<RabbitMQTransport>().ConnectionString("host=localhost").Routing();

            var ramp = routing.UseBridgeRamp("Right");
            ramp.RouteToEndpoint(typeof(MyCommand), "Some");
            ramp.RegisterPublisher(typeof(SomeEvent), "Some");

            return config;
        }
    }

    class MyCommandHandler : IHandleMessages<MyCommand>
    {
        public ReadOnlySettings Settings { get; set; }

        public Task Handle(MyCommand message, IMessageHandlerContext context)
        {
            Console.WriteLine($"{Settings.EndpointName()}: Got a command");
            return context.Reply(new MyReply());
        }
    }

    class MyReplyHandler : IHandleMessages<MyReply>
    {
        public ReadOnlySettings Settings { get; set; }

        public Task Handle(MyReply message, IMessageHandlerContext context)
        {
            Console.WriteLine($"{Settings.EndpointName()}: Got a reply");
            return Task.CompletedTask;
        }
    }

    class SomeEventHandler : IHandleMessages<SomeEvent>
    {
        public ReadOnlySettings Settings { get; set; }

        public Task Handle(SomeEvent message, IMessageHandlerContext context)
        {
            Console.WriteLine($"{Settings.EndpointName()}: Got some event");
            return Task.CompletedTask;
        }
    }

    class OtherEventHandler : IHandleMessages<OtherEvent>
    {
        public ReadOnlySettings Settings { get; set; }

        public Task Handle(OtherEvent message, IMessageHandlerContext context)
        {
            Console.WriteLine($"{Settings.EndpointName()}: Got other event");
            return Task.CompletedTask;
        }
    }

    class MyCommand : ICommand
    {
    }

    class MyReply : IMessage
    {
    }

    class SomeEvent : MyBaseEvent
    {
    }

    class OtherEvent : MyBaseEvent
    {
    }

    class MyBaseEvent : IEvent
    {
    }
}