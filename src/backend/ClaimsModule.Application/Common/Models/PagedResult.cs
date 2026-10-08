namespace ClaimsModule.Application.Common.Models;

/// <summary>Shared server-side pagination envelope for list/log query handlers (ADR-0002).</summary>
public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);
