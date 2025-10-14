using System;
using System.Collections.Generic;
using System.Linq;
using InventoryService.Services.Models;
using Microsoft.Extensions.Logging;

namespace InventoryService.Services;

public sealed class InMemoryInventoryStore : IInventoryStore
{
    private readonly Dictionary<string, int> _stock = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SKU-1000"] = 100000,
        ["SKU-2000"] = 100000,
        ["SKU-3000"] = 100000
    };

    private readonly ILogger<InMemoryInventoryStore> _logger;
    private readonly object _syncRoot = new();

    public InMemoryInventoryStore(ILogger<InMemoryInventoryStore> logger)
    {
        _logger = logger;
    }

    public ValueTask<InventoryReservationResult> ReserveAsync(string orderId, IReadOnlyDictionary<string, int> requested, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requested);

        lock (_syncRoot)
        {
            foreach (var kvp in requested)
            {
                if (!_stock.TryGetValue(kvp.Key, out var available) || available < kvp.Value)
                {
                    var remaining = BuildRemainingSnapshot(requested.Keys);
                    _logger.LogWarning("Insufficient stock for order {OrderId} sku {Sku}. Requested {Requested} available {Available}", orderId, kvp.Key, kvp.Value, available);
                    return ValueTask.FromResult(new InventoryReservationResult(false, remaining));
                }
            }

            foreach (var kvp in requested)
            {
                _stock[kvp.Key] = _stock[kvp.Key] - kvp.Value;
            }

            var remainingAfterReservation = BuildRemainingSnapshot(requested.Keys);
            _logger.LogInformation("Reserved inventory for order {OrderId}: {Items}", orderId, string.Join(", ", requested.Select(kvp => $"{kvp.Key}:{kvp.Value}")));
            return ValueTask.FromResult(new InventoryReservationResult(true, remainingAfterReservation));
        }
    }

    private Dictionary<string, int> BuildRemainingSnapshot(IEnumerable<string> skus)
    {
        return skus.ToDictionary(sku => sku, sku => _stock.TryGetValue(sku, out var remaining) ? remaining : 0, StringComparer.OrdinalIgnoreCase);
    }
}
