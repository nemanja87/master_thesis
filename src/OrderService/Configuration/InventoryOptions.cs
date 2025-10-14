namespace OrderService.Configuration;

public sealed class InventoryOptions
{
    public bool UseGrpc { get; set; }
    public string RestBaseUrl { get; set; } = "https://localhost:6002";
    public string GrpcAddress { get; set; } = "https://localhost:7002";
}
