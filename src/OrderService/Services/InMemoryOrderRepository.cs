using System.Collections.Concurrent;
using OrderService.Models;

namespace OrderService.Services;

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<string, OrderRecord> _orders = new(StringComparer.OrdinalIgnoreCase);

    public ValueTask<OrderRecord?> GetAsync(string orderId, CancellationToken cancellationToken = default)
    {
        _orders.TryGetValue(orderId, out var order);
        return ValueTask.FromResult(order);
    }

    public ValueTask AddAsync(OrderRecord order, CancellationToken cancellationToken = default)
    {
        _orders[order.OrderId] = order;
        return ValueTask.CompletedTask;
    }
}
