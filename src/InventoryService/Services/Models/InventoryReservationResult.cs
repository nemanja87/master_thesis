using System.Collections.Generic;

namespace InventoryService.Services.Models;

public sealed record InventoryReservationResult(bool Success, IReadOnlyDictionary<string, int> RemainingQuantities);
