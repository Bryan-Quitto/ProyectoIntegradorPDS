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
