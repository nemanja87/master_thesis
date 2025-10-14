using System.Collections.Generic;

namespace Shared.Contracts.Orders;

public sealed record CreateOrderRequest(string CustomerId, IReadOnlyList<string> ItemSkus, decimal TotalAmount);
