using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderService.Configuration;

namespace OrderService.Clients;

public sealed class RestInventoryClient : IInventoryClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RestInventoryClient> _logger;

    public RestInventoryClient(HttpClient httpClient, IOptions<InventoryOptions> options, ILogger<RestInventoryClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(options.Value.RestBaseUrl, UriKind.Absolute);
    }

    public async Task<bool> ReserveAsync(string orderId, IReadOnlyList<string> itemSkus, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            orderId,
            items = itemSkus
        };

        using var response = await _httpClient.PostAsJsonAsync("/inventory/reservations", payload, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning("Inventory REST reservation failed with status {Status} and body {Body}", response.StatusCode, content);
        return false;
    }
}
