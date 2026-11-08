using Pos.Application.DTOs;
using Pos.Domain.Entities;

namespace Pos.Application.Ports;

public interface IOdooPosGateway
{
    Task<IReadOnlyList<ProductDto>> SearchProductsAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los productos habilitados para el POS con los IDs indicados (precio y regla fiscal oficiales, RN-03).
    /// Los IDs inexistentes o no habilitados simplemente no se devuelven.
    /// </summary>
    Task<IReadOnlyList<ProductDto>> GetProductsByIdsAsync(IReadOnlyCollection<int> productIds, CancellationToken cancellationToken = default);

    Task<PosSessionDto?> GetActiveSessionAsync(CancellationToken cancellationToken = default);
    Task<OdooSyncResultDto> SyncSaleOrderAsync(SaleOrder saleOrder, CancellationToken cancellationToken = default);

    /// <summary>
    /// [GUÍA ESTEBAN - TURNO 3 - RF-10]:
    /// Resuelve dinámicamente en Odoo el ID del diario de pago para la sesión activa según el tipo solicitado.
    /// </summary>
    Task<int> ResolvePaymentMethodIdAsync(int sessionId, string paymentMethodType, CancellationToken cancellationToken = default);

    /// <summary>
    /// [GUÍA ESTEBAN - TURNO 3 - RF-10]:
    /// Sincroniza la orden en Odoo asociando el método de pago resuelto polimórficamente por el procesador (Strategy).
    /// </summary>
    Task<OdooSyncResultDto> SyncSaleOrderAsync(SaleOrder saleOrder, int paymentMethodId, CancellationToken cancellationToken = default);
}

public interface IIdempotencyStore
{
    Task<bool> HasKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task SaveKeyAsync(string idempotencyKey, Guid orderId, string responseJson, CancellationToken cancellationToken = default);
    Task<string?> GetResponseAsync(string idempotencyKey, CancellationToken cancellationToken = default);
}

public interface ISaleHistoryRepository
{
    Task SaveSaleAsync(SaleOrderResponseDto sale, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SaleOrderResponseDto>> GetAllSalesAsync(CancellationToken cancellationToken = default);
    Task<SaleOrderResponseDto?> GetSaleByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
