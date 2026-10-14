
namespace TrustFlow.Api.Options
{
    public class BlockchainOptions
    {
        public const string SectionName = "Blockchain";

        public long ChainId { get; set; }

        public string RpcUrl { get; set; } = string.Empty;

        public string TokenAddress { get; set; } = string.Empty;

        public string DeployerPrivateKey { get; set; } = string.Empty;

        // The one address allowed to settle a case between a client and a
        // freelancer, fixed into each escrow when it is deployed. It should
        // belong to a multisig or a person, never to this server.
        public string ArbiterAddress { get; set; } = string.Empty;

        // Public receiving address only. No signing key belongs on this server.
        public string FeeRecipientAddress { get; set; } = string.Empty;
    }
}
