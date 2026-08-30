using System.Threading.Tasks;

namespace GameplayProtocol
{
    public interface IProtocolIngress
    {
        Task<ProtocolResponse> Enqueue(ProtocolClient client, ProtocolRequest request);
    }

    public interface IProtocolPump { int Drain(int maxRequests); }

    public delegate string ProtocolHandler(ProtocolClient client, ProtocolRequest request);
}
