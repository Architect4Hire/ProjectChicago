using System.ComponentModel.DataAnnotations;

namespace ProjectChicago.Identity.Core.Authorization.Contracts;

// GET /users query contract (SEC-004, SEC-010..016). Administrator-only list of users
// with support-safe metadata and pagination. Pagination is bounded to prevent
// unbounded result sets (CLIENT-024/API-005 principle).
public sealed record ListUsersRequest
{
    // Administrator-only pagination: 1-based page number.
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    // Bounded page size to prevent unbounded queries.
    [Range(1, 100)]
    public int PageSize { get; init; } = 10;
}
