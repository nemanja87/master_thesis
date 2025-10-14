using System.Collections.Generic;

namespace Shared.Contracts.Orders;

public sealed record OrderDto(string OrderId, string CustomerId, IReadOnlyList<string> ItemSkus, string Status);
