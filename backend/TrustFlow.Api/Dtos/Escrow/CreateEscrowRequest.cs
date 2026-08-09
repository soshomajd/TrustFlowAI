namespace TrustFlow.Api.Dtos.Escrows;

public sealed class CreateEscrowRequest
{
    public long ChainId { get; set; }

    public string TokenAddress { get; set; } =
        string.Empty;
}