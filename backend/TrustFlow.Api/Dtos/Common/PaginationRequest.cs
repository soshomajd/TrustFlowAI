using System.ComponentModel.DataAnnotations;

namespace TrustFlow.Api.Dtos.Common;

public sealed class PaginationRequest
{
    [Range(1, 100000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}