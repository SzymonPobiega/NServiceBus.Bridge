namespace NServiceBus.Bridge
{
    using System.Threading.Tasks;

    public interface ISwitch
    {
        Task Start();
        Task Stop();
    }
}