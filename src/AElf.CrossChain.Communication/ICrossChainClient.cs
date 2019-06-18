using System.Threading.Tasks;
using Acs7;

namespace AElf.CrossChain.Communication
{
    public interface ICrossChainClient
    {
        int RemoteChainId { get; }
        string TargetUriString { get; }
        
        bool IsConnected { get; set; }
        Task RequestCrossChainDataAsync(long targetHeight);
        Task<ChainInitializationData> RequestChainInitializationDataAsync(int chainId);
        Task<bool> ConnectAsync();
        Task CloseAsync();
    }
}