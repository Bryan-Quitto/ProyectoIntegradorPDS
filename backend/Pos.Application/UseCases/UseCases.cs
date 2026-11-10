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

public class SearchCustomersUseCase
{
    private readonly IOdooPosGateway _odooGateway;

    public SearchCustomersUseCase(IOdooPosGateway odooGateway)
    {
        _odooGateway = odooGateway;
    }

    public async Task<IReadOnlyList<CustomerDto>> ExecuteAsync(string query, CancellationToken cancellationToken = default)
    {
        return await _odooGateway.SearchCustomersAsync(query, cancellationToken);
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
    private readonly IReadOnlyList<IPaymentProcessor> _paymentProcessors;

    public CreateSaleUseCase(
        IOdooPosGateway odooGateway,
        IIdempotencyStore idempotencyStore,
        ISaleHistoryRepository historyRepository)
        : this(odooGateway, idempotencyStore, historyRepository, new IPaymentProcessor[] { new CashPaymentProcessor(odooGateway) })
    {
    }

    /// <summary>
    /// [GUÍA ESTEBAN - TURNO 3 - RF-10 / OCP]:
    /// Inyección de dependencias de procesadores de pago polimórficos.
    /// Permite extender nuevos métodos de pago sin modificar este caso de uso.
    /// </summary>
    public CreateSaleUseCase(
        IOdooPosGateway odooGateway,
        IIdempotencyStore idempotencyStore,
        ISaleHistoryRepository historyRepository,
        IEnumerable<IPaymentProcessor> paymentProcessors)
    {
        _odooGateway = odooGateway;
        _idempotencyStore = idempotencyStore;
        _historyRepository = historyRepository;
        _paymentProcessors = paymentProcessors.ToList().AsReadOnly();
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

        // [GUÍA ESTEBAN - FIX AUDITORÍA ELI/VERO - RF-08 / RN-04]:
        // Resolución estricta de la estrategia de descuento (Strategy GoF).
        // Error detectado en el código previo de Eli y Vero:
        // Usaban un comodín silencioso '_ => new NoDiscountPolicy()' que ocultaba cualquier tipo
        // de descuento inválido, arbitrario o inyectado maliciosamente.
        // La especificación RF-08 exige explícitamente: "Rechazar descuentos no autorizados con mensajes claros".
        var normalizedDiscountType = (command.DiscountType ?? "NONE").Trim().ToUpperInvariant();
        IDiscountPolicy discountPolicy = normalizedDiscountType switch
        {
            "NONE" or "" => new NoDiscountPolicy(),
            "PERCENTAGE" => new PercentageDiscountPolicy(command.DiscountValue),
            "THRESHOLD_FIXED" => new ThresholdFixedDiscountPolicy(50m, command.DiscountValue > 0 ? command.DiscountValue : 5m),
            _ => throw new DomainValidationException(
                nameof(command.DiscountType),
                $"La política de descuento '{command.DiscountType}' no está autorizada por el sistema (RF-08 / RN-04).")
        };

        var order = new SaleOrder(
            command.PosSessionId,
            command.CustomerDocument,
            command.CustomerName,
            discountPolicy,
            id: DeriveOrderId(command.IdempotencyKey),
            partnerId: command.PartnerId);

        // 5. RN-03: Cargar precio y regla fiscal oficiales desde Odoo SOLO para los productos solicitados.
        //    Nunca se confía en precios enviados por el cliente.
        var requestedIds = command.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _odooGateway.GetProductsByIdsAsync(requestedIds, cancellationToken);
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
                throw new ApplicationValidationException(nameof(item.ProductId), $"El producto con ID {item.ProductId} no existe en el catálogo de Odoo o no está habilitado para el POS.");
            }

            var taxRule = new TaxRule(prodDto.TaxRate, prodDto.PriceIncludesTax, prodDto.TaxIds);
            var product = new Product(prodDto.Id, prodDto.Name, new Money(prodDto.Price), prodDto.Barcode, prodDto.AvailableQuantity, taxRule);
            order.AddLine(product, new Quantity(item.Quantity));
        }

        // 6. Confirmación de la orden (transición de estado y congelación RN-06)
        order.Confirm();

        // [GUÍA ESTEBAN - TURNO 3 - RF-10 / PATRÓN STRATEGY / OCP]:
        // 7. Resolución de la estrategia de pago mediante el procesador polimórfico
        var requestedMethod = string.IsNullOrWhiteSpace(command.PaymentMethod) ? "Efectivo" : command.PaymentMethod.Trim();
        var paymentProcessor = _paymentProcessors.FirstOrDefault(p => p.CanHandle(requestedMethod))
            ?? _paymentProcessors.FirstOrDefault(p => p.CanHandle("Efectivo"))
            ?? throw new ApplicationValidationException(nameof(command.PaymentMethod), $"Método de pago '{requestedMethod}' no soportado (RF-10).");

        var paymentResult = await paymentProcessor.ProcessPaymentAsync(order, command.PosSessionId, cancellationToken);
        if (!paymentResult.Success)
        {
            throw new ApplicationValidationException(nameof(command.PaymentMethod), paymentResult.ErrorMessage ?? "Error procesando el método de pago.");
        }

        // 8. Sincronización hacia Odoo 20 JSON-2 asociando el método resuelto
        var syncResult = paymentResult.ResolvedOdooMethodId > 0
            ? await _odooGateway.SyncSaleOrderAsync(order, paymentResult.ResolvedOdooMethodId, cancellationToken)
            : await _odooGateway.SyncSaleOrderAsync(order, cancellationToken);
        if (!syncResult.Success)
        {
            // RN-07: Prohibido declarar falsos éxitos si falla Odoo
            throw new OdooCommunicationException($"Error sincronizando la orden con Odoo: {syncResult.ErrorMessage}");
        }

        // 9. Marcar como sincronizada
        order.MarkAsSynced(syncResult.OdooReference, syncResult.OdooOrderId);

        // 10. Construir DTO de respuesta con el método y monto real procesado
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
            order.Lines.Select(l =>
            {
                var fiscal = order.GetLineFiscalAmounts(l);
                var prodInfo = productDict.TryGetValue(l.ProductId, out var p) ? p : null;
                return new SaleOrderLineDto(
                    l.ProductId,
                    l.ProductName,
                    l.Quantity.Value,
                    l.UnitPrice.Amount,
                    l.SubTotal.Amount,
                    ProductCode: prodInfo?.DefaultCode ?? "",
                    TaxRate: l.Tax.Rate,
                    NetSubTotal: fiscal.TaxBase,
                    TaxAmount: fiscal.TaxAmount,
                    TotalWithTax: fiscal.TotalWithTax);
            }).ToList(),
            DiscountPolicyName: order.DiscountPolicy.PolicyName,
            PaymentMethod: paymentResult.MethodName,
            AmountPaid: paymentResult.AmountPaid,
            SessionName: activeSession?.Name ?? $"Sesión #{order.PosSessionId}",
            OperatorName: !string.IsNullOrWhiteSpace(activeSession?.UserName) ? activeSession.UserName : "Cajero POS",
            PartnerId: order.PartnerId
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

    /// <summary>
    /// Deriva un identificador de orden determinista a partir de la Idempotency-Key.
    /// Ese ID viaja a Odoo como <c>uuid</c> del <c>pos.order</c> (restricción única en Odoo), de modo que
    /// un reintento tras una caída entre "Odoo confirmó" y "SQLite guardó" no duplica la venta en el ERP.
    /// </summary>
    private static Guid DeriveOrderId(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Guid.NewGuid();

        if (Guid.TryParse(idempotencyKey, out var parsed))
            return parsed;

        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(idempotencyKey.Trim()));
        return new Guid(hash);
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

    public async Task<SaleOrderResponseDto?> ExecuteAsync(string idString, CancellationToken cancellationToken = default)
    {
        if (Guid.TryParse(idString, out var guidId))
        {
            return await _historyRepository.GetSaleByIdAsync(guidId, cancellationToken);
        }
        return null;
    }
}
