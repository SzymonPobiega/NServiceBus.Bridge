using System.Threading.Tasks;

namespace NServiceBus.Bridge
{
    public interface IBridge
    {
        Task Start();
        Task Stop();
    }
}