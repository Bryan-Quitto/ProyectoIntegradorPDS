using System.Text.Json;
using Pos.Application.DTOs;
using Pos.Application.Exceptions;
using Pos.Application.Ports;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Strategies;
using Pos.Domain.ValueObjects;

namespace Pos.Application.UseCases;

public class SearchProductsUseCase
{
    private readonly IOdooPosGateway _odooGateway;

    public SearchProductsUseCase(IOdooPosGateway odooGateway)
    {
        _odooGateway = odooGateway;
    }

    public async Task<IReadOnlyList<ProductDto>> ExecuteAsync(string query, CancellationToken cancellationToken = default)
    {
        return await _odooGateway.SearchProductsAsync(query, cancellationToken);
    }
}

public class GetActiveSessionUseCase
{
    private readonly IOdooPosGateway _odooGateway;

    public GetActiveSessionUseCase(IOdooPosGateway odooGateway)
    {
        _odooGateway = odooGateway;
    }

    public async Task<PosSessionDto?> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _odooGateway.GetActiveSessionAsync(cancellationToken);
    }
}

public class CreateSaleUseCase
{
    private readonly IOdooPosGateway _odooGateway;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ISaleHistoryRepository _historyRepository;

    public CreateSaleUseCase(
        IOdooPosGateway odooGateway,
        IIdempotencyStore idempotencyStore,
        ISaleHistoryRepository historyRepository)
    {
        _odooGateway = odooGateway;
        _idempotencyStore = idempotencyStore;
        _historyRepository = historyRepository;
    }

    public async Task<SaleOrderResponseDto> ExecuteAsync(CreateSaleCommand command, CancellationToken cancellationToken = default)
    {
        // 1. RN-08 Control de Idempotencia
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            if (await _idempotencyStore.HasKeyAsync(command.IdempotencyKey, cancellationToken))
            {
                var cached = await _idempotencyStore.GetResponseAsync(command.IdempotencyKey, cancellationToken);
                if (!string.IsNullOrWhiteSpace(cached))
                {
                    var cachedDto = JsonSerializer.Deserialize<SaleOrderResponseDto>(cached);
                    if (cachedDto != null)
                        return cachedDto;
                }
            }
        }

        // 2. Validación de Sesión Activa de POS
        var activeSession = await _odooGateway.GetActiveSessionAsync(cancellationToken);
        if (activeSession == null || !activeSession.IsOpen || activeSession.Id != command.PosSessionId)
        {
            throw new ApplicationValidationException(nameof(command.PosSessionId),
                "La sesión de POS no se encuentra abierta o no coincide con la sesión activa de Odoo.");
        }

        // 3. RN-01 Rechazar venta sin productos
        if (command.Items == null || command.Items.Count == 0)
        {
            throw new DomainValidationException(nameof(command.Items), "No se puede registrar una venta sin productos (RN-01).");
        }

        // 4. Estrategia de descuento
        IDiscountPolicy discountPolicy = command.DiscountType?.ToUpperInvariant() switch
        {
            "PERCENTAGE" => new PercentageDiscountPolicy(command.DiscountValue),
            "THRESHOLD_FIXED" => new ThresholdFixedDiscountPolicy(50m, command.DiscountValue > 0 ? command.DiscountValue : 5m),
            _ => new NoDiscountPolicy()
        };

        var order = new SaleOrder(
            command.PosSessionId,
            command.CustomerDocument,
            command.CustomerName,
            discountPolicy);

        // 5. Cargar catálogo para validar productos y precios oficiales de Odoo
        var products = await _odooGateway.SearchProductsAsync(string.Empty, cancellationToken);
        var productDict = products.ToDictionary(p => p.Id);

        foreach (var item in command.Items)
        {
            // RN-02: Cantidad estrictamente mayor a 0
            if (item.Quantity <= 0)
            {
                throw new DomainValidationException(nameof(item.Quantity), "La cantidad de producto debe ser mayor a 0 (RN-02).");
            }

            if (!productDict.TryGetValue(item.ProductId, out var prodDto))
            {
                throw new ApplicationValidationException(nameof(item.ProductId), $"El producto con ID {item.ProductId} no existe en el catálogo de Odoo.");
            }

            var product = new Product(prodDto.Id, prodDto.Name, new Money(prodDto.Price), prodDto.Barcode, prodDto.AvailableQuantity);
            order.AddLine(product, new Quantity(item.Quantity));
        }

        // 6. Confirmación de la orden (transición de estado y congelación RN-06)
        order.Confirm();

        // 7. Sincronización hacia Odoo 20 JSON-2
        var syncResult = await _odooGateway.SyncSaleOrderAsync(order, cancellationToken);
        if (!syncResult.Success)
        {
            // RN-07: Prohibido declarar falsos éxitos si falla Odoo
            throw new OdooCommunicationException($"Error sincronizando la orden con Odoo: {syncResult.ErrorMessage}");
        }

        // 8. Marcar como sincronizada
        order.MarkAsSynced(syncResult.OdooReference, syncResult.OdooOrderId);

        // 9. Construir DTO de respuesta
        var responseDto = new SaleOrderResponseDto(
            order.Id,
            order.PosSessionId,
            order.CustomerDocument,
            order.CustomerName,
            order.SubTotal,
            order.DiscountAmount,
            order.TaxAmount,
            order.TotalAmount,
            order.State.StateName,
            order.OdooOrderReference,
            order.OdooOrderId,
            order.CreatedAtUtc,
            order.Lines.Select(l => new SaleOrderLineDto(
                l.ProductId,
                l.ProductName,
                l.Quantity.Value,
                l.UnitPrice.Amount,
                l.SubTotal.Amount)).ToList()
        );

        // 10. Persistencia local para trazabilidad e idempotencia
        await _historyRepository.SaveSaleAsync(responseDto, cancellationToken);

        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            var serialized = JsonSerializer.Serialize(responseDto);
            await _idempotencyStore.SaveKeyAsync(command.IdempotencyKey, order.Id, serialized, cancellationToken);
        }

        return responseDto;
    }
}

public class GetSalesHistoryUseCase
{
    private readonly ISaleHistoryRepository _historyRepository;

    public GetSalesHistoryUseCase(ISaleHistoryRepository historyRepository)
    {
        _historyRepository = historyRepository;
    }

    public async Task<IReadOnlyList<SaleOrderResponseDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _historyRepository.GetAllSalesAsync(cancellationToken);
    }
}

public class GetSaleByIdUseCase
{
    private readonly ISaleHistoryRepository _historyRepository;

    public GetSaleByIdUseCase(ISaleHistoryRepository historyRepository)
    {
        _historyRepository = historyRepository;
    }

    public async Task<SaleOrderResponseDto?> ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _historyRepository.GetSaleByIdAsync(id, cancellationToken);
    }
}
