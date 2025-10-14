using System.Collections.Immutable;
using System.Linq;
using Microsoft.Extensions.Logging;
using OrderService.Clients;
using OrderService.Models;
using Shared.Contracts.Orders;

namespace OrderService.Services;

public sealed class OrderProcessor
{
    private readonly IOrderRepository _repository;
    private readonly IInventoryClient _inventoryClient;
    private readonly ILogger<OrderProcessor> _logger;

    public OrderProcessor(IOrderRepository repository, IInventoryClient inventoryClient, ILogger<OrderProcessor> logger)
    {
        _repository = repository;
        _inventoryClient = inventoryClient;
        _logger = logger;
    }

    public async Task<CreateOrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        var orderId = Guid.NewGuid().ToString("N");

        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            throw new ArgumentException("Customer identifier is required.", nameof(request));
        }

        if (request.ItemSkus is null || request.ItemSkus.Count == 0)
        {
            throw new ArgumentException("At least one item SKU is required.", nameof(request));
        }

        var itemSkus = request.ItemSkus
            .Where(sku => !string.IsNullOrWhiteSpace(sku))
            .Select(sku => sku.Trim())
            .ToImmutableArray();

        if (itemSkus.Length == 0)
        {
            throw new ArgumentException("At least one valid item SKU is required.", nameof(request));
        }

        var reserveSucceeded = await _inventoryClient.ReserveAsync(orderId, itemSkus, cancellationToken);
        if (!reserveSucceeded)
        {
            _logger.LogWarning("Inventory reservation failed for order {OrderId}", orderId);
            return new CreateOrderResponse(orderId, Accepted: false);
        }

        var order = new OrderRecord(
            orderId,
            request.CustomerId,
            itemSkus,
            request.TotalAmount,
            Status: "Reserved",
            CreatedAt: DateTimeOffset.UtcNow);

        await _repository.AddAsync(order, cancellationToken);

        _logger.LogInformation("Order {OrderId} created for customer {CustomerId}", orderId, request.CustomerId);

        return new CreateOrderResponse(orderId, Accepted: true);
    }

    public async Task<OrderDto?> GetAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var record = await _repository.GetAsync(orderId, cancellationToken);
        if (record is null)
        {
            return null;
        }

        return new OrderDto(
            record.OrderId,
            record.CustomerId,
            record.ItemSkus,
            record.Status);
    }
}
