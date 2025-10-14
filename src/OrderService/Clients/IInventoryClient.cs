namespace OrderService.Clients;

public interface IInventoryClient
{
    Task<bool> ReserveAsync(string orderId, IReadOnlyList<string> itemSkus, CancellationToken cancellationToken = default);
}
