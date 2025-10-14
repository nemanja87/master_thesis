namespace Shared.Contracts.Orders;

public sealed record CreateOrderResponse(string OrderId, bool Accepted);
