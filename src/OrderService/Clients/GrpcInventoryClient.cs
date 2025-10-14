using System.Linq;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Protos;

namespace OrderService.Clients;

public sealed class GrpcInventoryClient : IInventoryClient
{
    private readonly InventoryService.InventoryServiceClient _client;
    private readonly ILogger<GrpcInventoryClient> _logger;

    public GrpcInventoryClient(InventoryService.InventoryServiceClient client, ILogger<GrpcInventoryClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<bool> ReserveAsync(string orderId, IReadOnlyList<string> itemSkus, CancellationToken cancellationToken = default)
    {
        var grouped = itemSkus
            .GroupBy(sku => sku, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ReserveInventoryRequest
            {
                Sku = group.Key,
                Quantity = group.Count()
            })
            .ToList();

        foreach (var request in grouped)
        {
            try
            {
                var response = await _client.ReserveAsync(request, cancellationToken: cancellationToken);
                if (!response.Success)
                {
                    _logger.LogWarning("Inventory gRPC reservation failed for sku {Sku} (remaining {Remaining})", request.Sku, response.RemainingQuantity);
                    return false;
                }
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Inventory gRPC reservation failed for sku {Sku}", request.Sku);
                return false;
            }
        }

        return true;
    }
}
