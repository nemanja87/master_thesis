using System;
using System.Collections.Generic;
using System.Linq;
using Grpc.Core;
using InventoryService.Services;
using Shared.Contracts.Protos;

namespace InventoryService.Grpc;

public sealed class InventoryGrpcService : Shared.Contracts.Protos.InventoryService.InventoryServiceBase
{
    private readonly IInventoryStore _inventoryStore;

    public InventoryGrpcService(IInventoryStore inventoryStore)
    {
        _inventoryStore = inventoryStore;
    }

    public override async Task<ReserveInventoryResponse> Reserve(ReserveInventoryRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Sku))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "SKU is required."));
        }

        if (request.Quantity <= 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Quantity must be greater than zero."));
        }

        var requested = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [request.Sku] = request.Quantity
        };

        var orderId = ResolveOrderId(context.RequestHeaders);
        var result = await _inventoryStore.ReserveAsync(orderId, requested, context.CancellationToken);

        result.RemainingQuantities.TryGetValue(request.Sku, out var remaining);

        return new ReserveInventoryResponse
        {
            Success = result.Success,
            RemainingQuantity = remaining
        };
    }

    private static string ResolveOrderId(Metadata headers)
    {
        var header = headers.FirstOrDefault(h => string.Equals(h.Key, "order-id", StringComparison.OrdinalIgnoreCase));
        return !string.IsNullOrWhiteSpace(header?.Value)
            ? header!.Value
            : $"grpc-order-{Guid.NewGuid():N}";
    }
}
