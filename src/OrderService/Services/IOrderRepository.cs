using OrderService.Models;

namespace OrderService.Services;

public interface IOrderRepository
{
    ValueTask<OrderRecord?> GetAsync(string orderId, CancellationToken cancellationToken = default);
    ValueTask AddAsync(OrderRecord order, CancellationToken cancellationToken = default);
}
