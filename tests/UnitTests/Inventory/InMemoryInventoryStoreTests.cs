using InventoryService.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace UnitTests.Inventory;

public sealed class InMemoryInventoryStoreTests
{
    [Fact]
    public async Task ReserveAsync_Succeeds_WhenStockAvailable()
    {
        var store = new InMemoryInventoryStore(Mock.Of<ILogger<InMemoryInventoryStore>>());
        var result = await store.ReserveAsync("order-1", new Dictionary<string, int> { ["SKU-1000"] = 1 });

        Assert.True(result.Success);
        Assert.True(result.RemainingQuantities.ContainsKey("SKU-1000"));
    }

    [Fact]
    public async Task ReserveAsync_Fails_WhenStockInsufficient()
    {
        var store = new InMemoryInventoryStore(Mock.Of<ILogger<InMemoryInventoryStore>>());
        var result = await store.ReserveAsync("order-1", new Dictionary<string, int> { ["SKU-1000"] = 200_000 });

        Assert.False(result.Success);
        Assert.True(result.RemainingQuantities.ContainsKey("SKU-1000"));
    }
}
