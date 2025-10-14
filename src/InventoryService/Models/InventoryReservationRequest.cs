using System.Collections.Generic;

namespace InventoryService.Models;

public sealed record InventoryReservationRequest(string OrderId, IReadOnlyList<InventoryReservationItem> Items);

public sealed record InventoryReservationItem(string Sku, int Quantity);
