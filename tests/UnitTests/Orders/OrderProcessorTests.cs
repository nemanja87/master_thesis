using Microsoft.Extensions.Logging;
using Moq;
using OrderService.Clients;
using OrderService.Services;
using Shared.Contracts.Orders;

namespace UnitTests.Orders;

public sealed class OrderProcessorTests
{
    [Fact]
    public async Task CreateAsync_PersistsOrder_WhenInventoryReservationSucceeds()
    {
        // Arrange
        var inventoryClient = new Mock<IInventoryClient>();
        inventoryClient
            .Setup(client => client.ReserveAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var repository = new InMemoryOrderRepository();
        var processor = new OrderProcessor(repository, inventoryClient.Object, Mock.Of<ILogger<OrderProcessor>>());
        var request = new CreateOrderRequest("customer-1", new[] { "sku-1", "sku-2" }, 100m);

        // Act
        var response = await processor.CreateAsync(request);
        var stored = await repository.GetAsync(response.OrderId);

        // Assert
        Assert.True(response.Accepted);
        Assert.NotNull(stored);
        Assert.Equal(request.CustomerId, stored!.CustomerId);
        Assert.Equal(2, stored.ItemSkus.Length);
    }

    [Fact]
    public async Task CreateAsync_RejectsOrder_WhenInventoryReservationFails()
    {
        var inventoryClient = new Mock<IInventoryClient>();
        inventoryClient
            .Setup(client => client.ReserveAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var repository = new InMemoryOrderRepository();
        var processor = new OrderProcessor(repository, inventoryClient.Object, Mock.Of<ILogger<OrderProcessor>>());
        var request = new CreateOrderRequest("customer-1", new[] { "sku-1" }, 50m);

        var response = await processor.CreateAsync(request);
        var stored = await repository.GetAsync(response.OrderId);

        Assert.False(response.Accepted);
        Assert.Null(stored);
    }
}
