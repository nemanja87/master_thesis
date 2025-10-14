using System.Collections.Immutable;

namespace OrderService.Models;

public sealed record OrderRecord(
    string OrderId,
    string CustomerId,
    ImmutableArray<string> ItemSkus,
    decimal TotalAmount,
    string Status,
    DateTimeOffset CreatedAt);
