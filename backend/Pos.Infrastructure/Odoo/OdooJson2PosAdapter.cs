using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pos.Application.DTOs;
using Pos.Application.Exceptions;
using Pos.Application.Ports;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.Odoo;

public class OdooJson2PosAdapter : IOdooPosGateway
{
    private readonly HttpClient _httpClient;
    private readonly OdooOptions _options;
    private readonly ILogger<OdooJson2PosAdapter> _logger;
    private int? _uid;

    public OdooJson2PosAdapter(
        HttpClient httpClient,
        IOptions<OdooOptions> options,
        ILogger<OdooJson2PosAdapter> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        }
    }

    private async Task<int> AuthenticateAsync(CancellationToken cancellationToken)
    {
        if (_uid.HasValue)
            return _uid.Value;

        var payload = new
        {
            jsonrpc = "2.0",
            method = "call",
            @params = new
            {
                service = "common",
                method = "authenticate",
                args = new object[]
                {
                    _options.Database,
                    _options.Username,
                    _options.EffectiveSecret,
                    new { }
                }
            },
            id = Random.Shared.Next(1, 100000)
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/jsonrpc", payload, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonNode = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
            var result = jsonNode?["result"]?.GetValue<int?>();

            if (result == null || result.Value <= 0)
            {
                throw new OdooCommunicationException("Credenciales de Odoo inválidas o autenticación rechazada por el servidor.");
            }

            _uid = result.Value;
            return _uid.Value;
        }
        catch (Exception ex) when (ex is not OdooCommunicationException)
        {
            _logger.LogError(ex, "Error de comunicación al autenticar con Odoo 20.");
            throw new OdooCommunicationException($"No fue posible conectar con el servidor Odoo en {_options.BaseUrl}: {ex.Message}", ex);
        }
    }

    public async Task<IReadOnlyList<ProductDto>> SearchProductsAsync(string query, CancellationToken cancellationToken = default)
    {
        var uid = await AuthenticateAsync(cancellationToken);

        var domain = new List<object>();
        if (!string.IsNullOrWhiteSpace(query))
        {
            domain.Add(new object[] { "name", "ilike", query.Trim() });
        }

        var payload = new
        {
            jsonrpc = "2.0",
            method = "call",
            @params = new
            {
                service = "object",
                method = "execute_kw",
                args = new object[]
                {
                    _options.Database,
                    uid,
                    _options.EffectiveSecret,
                    "product.product",
                    "search_read",
                    new object[] { domain },
                    new
                    {
                        fields = new[] { "id", "name", "list_price", "barcode", "qty_available" },
                        limit = 100
                    }
                }
            },
            id = Random.Shared.Next(1, 100000)
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/jsonrpc", payload, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonNode = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
            var error = jsonNode?["error"];
            if (error != null)
            {
                throw new OdooCommunicationException($"Error devuelto por Odoo al consultar productos: {error["data"]?["message"] ?? error["message"]}");
            }

            var result = jsonNode?["result"]?.AsArray();
            if (result == null) return Array.Empty<ProductDto>();

            var products = new List<ProductDto>();
            foreach (var item in result)
            {
                int id = item?["id"]?.GetValue<int>() ?? 0;
                string name = item?["name"]?.GetValue<string>() ?? $"Producto {id}";
                decimal price = item?["list_price"]?.GetValue<decimal>() ?? 0m;
                string barcode = item?["barcode"] is JsonValue jv && jv.TryGetValue<string>(out var bc) ? bc : string.Empty;
                decimal qty = item?["qty_available"]?.GetValue<decimal>() ?? 0m;

                products.Add(new ProductDto(id, name, price, barcode, qty));
            }

            return products;
        }
        catch (Exception ex) when (ex is not OdooCommunicationException)
        {
            _logger.LogError(ex, "Fallo al consultar productos en Odoo.");
            throw new OdooCommunicationException("Error al comunicarse con Odoo para obtener el catálogo de productos.", ex);
        }
    }

    public async Task<PosSessionDto?> GetActiveSessionAsync(CancellationToken cancellationToken = default)
    {
        var uid = await AuthenticateAsync(cancellationToken);

        var payload = new
        {
            jsonrpc = "2.0",
            method = "call",
            @params = new
            {
                service = "object",
                method = "execute_kw",
                args = new object[]
                {
                    _options.Database,
                    uid,
                    _options.EffectiveSecret,
                    "pos.session",
                    "search_read",
                    new object[]
                    {
                        new object[] { new object[] { "state", "=", "opened" } }
                    },
                    new
                    {
                        fields = new[] { "id", "name", "state", "user_id" },
                        limit = 1
                    }
                }
            },
            id = Random.Shared.Next(1, 100000)
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/jsonrpc", payload, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonNode = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
            var result = jsonNode?["result"]?.AsArray();

            if (result == null || result.Count == 0)
                return null;

            var first = result[0];
            int id = first?["id"]?.GetValue<int>() ?? 0;
            string name = first?["name"]?.GetValue<string>() ?? string.Empty;
            string state = first?["state"]?.GetValue<string>() ?? string.Empty;
            
            int userId = 0;
            if (first?["user_id"] is JsonArray ua && ua.Count > 0)
            {
                userId = ua[0]?.GetValue<int>() ?? 0;
            }

            return new PosSessionDto(id, name, state, userId, state == "opened");
        }
        catch (Exception ex) when (ex is not OdooCommunicationException)
        {
            _logger.LogError(ex, "Error consultando la sesión activa en Odoo.");
            throw new OdooCommunicationException("Error de red consultando la sesión activa en Odoo.", ex);
        }
    }

    public async Task<OdooSyncResultDto> SyncSaleOrderAsync(SaleOrder saleOrder, CancellationToken cancellationToken = default)
    {
        var uid = await AuthenticateAsync(cancellationToken);

        var payloadBuilder = new OdooPosOrderPayloadBuilder();
        var orderPayload = payloadBuilder.FromSaleOrder(saleOrder).Build();

        var payload = new
        {
            jsonrpc = "2.0",
            method = "call",
            @params = new
            {
                service = "object",
                method = "execute_kw",
                args = new object[]
                {
                    _options.Database,
                    uid,
                    _options.EffectiveSecret,
                    "pos.order",
                    "create",
                    new object[] { orderPayload }
                }
            },
            id = Random.Shared.Next(1, 100000)
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/jsonrpc", payload, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonNode = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
            var error = jsonNode?["error"];
            if (error != null)
            {
                string msg = error["data"]?["message"]?.ToString() ?? error["message"]?.ToString() ?? "Error desconocido en Odoo";
                _logger.LogError("Odoo rejected order creation: {Error}", msg);
                return new OdooSyncResultDto(false, string.Empty, 0, msg);
            }

            int odooOrderId = jsonNode?["result"]?.GetValue<int>() ?? 0;
            string reference = orderPayload["pos_reference"]?.ToString() ?? $"POS-{odooOrderId}";

            return new OdooSyncResultDto(true, reference, odooOrderId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción durante la sincronización de la orden de venta hacia Odoo.");
            return new OdooSyncResultDto(false, string.Empty, 0, ex.Message);
        }
    }
}
