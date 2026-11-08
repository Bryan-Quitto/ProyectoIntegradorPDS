using Pos.Domain.Entities;

namespace Pos.Application.Ports;

/// <summary>
/// Resultado de la ejecución de una estrategia de pago (Strategy Pattern).
/// </summary>
public record PaymentProcessingResult(
    bool Success,
    string MethodName,
    decimal AmountPaid,
    int ResolvedOdooMethodId,
    string? TransactionReference = null,
    string? ErrorMessage = null
);

/// <summary>
/// [GUÍA ESTEBAN - TURNO 3 - RF-10 / OCP / PATRÓN STRATEGY]:
/// Abstracción que define la estrategia para procesar y validar un método de pago en el POS.
/// Cumple con el Principio Open/Closed (OCP): permite incorporar nuevos mecanismos
/// (efectivo, transferencia bancaria, tarjetas, billeteras digitales) registrando nuevas
/// implementaciones sin modificar el código interno de 'CreateSaleUseCase'.
/// </summary>
public interface IPaymentProcessor
{
    /// <summary>Nombre representativo del método de pago.</summary>
    string MethodName { get; }

    /// <summary>Indica si este procesador puede gestionar el método solicitado.</summary>
    bool CanHandle(string paymentMethod);

    /// <summary>
    /// Resuelve el identificador fiscal en Odoo y procesa la transacción de cobro.
    /// </summary>
    Task<PaymentProcessingResult> ProcessPaymentAsync(
        SaleOrder saleOrder,
        int posSessionId,
        CancellationToken cancellationToken = default);
}
