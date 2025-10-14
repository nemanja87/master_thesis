using System.Collections.Generic;

namespace InventoryService.Models;

public sealed record InventoryReservationResponse(bool Success, IReadOnlyDictionary<string, int> RemainingQuantities);
