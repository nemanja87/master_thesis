using System.Collections.Generic;
using InventoryService.Services.Models;

namespace InventoryService.Services;

public interface IInventoryStore
{
    ValueTask<InventoryReservationResult> ReserveAsync(string orderId, IReadOnlyDictionary<string, int> requested, CancellationToken cancellationToken = default);
}
