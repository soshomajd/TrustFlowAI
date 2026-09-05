
namespace TrustFlow.Api.Options
{
    public class BlockchainOptions
    {
        public const string SectionName = "Blockchain";

        public long ChainId { get; set; }

        public string RpcUrl { get; set; } = string.Empty;

        public string DeployerPrivateKey { get; set; } = string.Empty;
    }
}
