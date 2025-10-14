using Grpc.Core;
using OrderService.Services;
using Shared.Contracts.Orders;
using Shared.Contracts.Protos;

namespace OrderService.Grpc;

public sealed class OrderGrpcService : Shared.Contracts.Protos.OrderService.OrderServiceBase
{
    private readonly OrderProcessor _orderProcessor;

    public OrderGrpcService(OrderProcessor orderProcessor)
    {
        _orderProcessor = orderProcessor;
    }

    public override async Task<CreateOrderResponseMessage> Create(CreateOrderRequestMessage request, ServerCallContext context)
    {
        var domainRequest = new CreateOrderRequest(request.CustomerId, request.ItemSkus, (decimal)request.TotalAmount);
        var result = await _orderProcessor.CreateAsync(domainRequest, context.CancellationToken);

        return new CreateOrderResponseMessage
        {
            OrderId = result.OrderId,
            Accepted = result.Accepted
        };
    }

    public override async Task<OrderDtoMessage> Get(GetOrderRequest request, ServerCallContext context)
    {
        var order = await _orderProcessor.GetAsync(request.OrderId, context.CancellationToken);
        if (order is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Order {request.OrderId} not found."));
        }

        var dto = new OrderDtoMessage
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            Status = order.Status
        };

        dto.ItemSkus.AddRange(order.ItemSkus);
        return dto;
    }
}
