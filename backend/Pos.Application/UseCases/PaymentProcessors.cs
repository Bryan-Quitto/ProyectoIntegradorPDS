using Pos.Application.Exceptions;
using Pos.Application.Ports;
using Pos.Domain.Entities;

namespace Pos.Application.UseCases;

/// <summary>
/// [GUÍA ESTEBAN - TURNO 3 - RF-10 / PATRÓN STRATEGY]:
/// Estrategia concreta de cobro en Efectivo (Cash).
/// Resuelve el ID del método tipo 'cash' en la sesión de Odoo y autoriza el pago.
/// </summary>
public class CashPaymentProcessor : IPaymentProcessor
{
    private readonly IOdooPosGateway _odooGateway;

    public string MethodName => "Efectivo";

    public CashPaymentProcessor(IOdooPosGateway odooGateway)
    {
        _odooGateway = odooGateway;
    }

    public bool CanHandle(string paymentMethod)
    {
        if (string.IsNullOrWhiteSpace(paymentMethod)) return false;
        var m = paymentMethod.Trim().ToUpperInvariant();
        return m is "EFECTIVO" or "CASH";
    }

    public async Task<PaymentProcessingResult> ProcessPaymentAsync(
        SaleOrder saleOrder,
        int posSessionId,
        CancellationToken cancellationToken = default)
    {
        var methodId = await _odooGateway.ResolvePaymentMethodIdAsync(posSessionId, "cash", cancellationToken);

        return new PaymentProcessingResult(
            Success: true,
            MethodName: "Efectivo",
            AmountPaid: saleOrder.TotalAmount,
            ResolvedOdooMethodId: methodId,
            TransactionReference: $"CASH-SESSION-{posSessionId}"
        );
    }
}

/// <summary>
/// [GUÍA ESTEBAN - TURNO 3 - RF-10 / PATRÓN STRATEGY]:
/// Estrategia concreta de cobro vía Transferencia Bancaria (Bank Transfer).
/// Resuelve el ID de pago tipo 'bank' en Odoo y asienta el cobro.
/// </summary>
public class BankTransferPaymentProcessor : IPaymentProcessor
{
    private readonly IOdooPosGateway _odooGateway;

    public string MethodName => "Transferencia";

    public BankTransferPaymentProcessor(IOdooPosGateway odooGateway)
    {
        _odooGateway = odooGateway;
    }

    public bool CanHandle(string paymentMethod)
    {
        if (string.IsNullOrWhiteSpace(paymentMethod)) return false;
        var m = paymentMethod.Trim().ToUpperInvariant();
        return m is "TRANSFERENCIA" or "TRANSFER" or "BANK";
    }

    public async Task<PaymentProcessingResult> ProcessPaymentAsync(
        SaleOrder saleOrder,
        int posSessionId,
        CancellationToken cancellationToken = default)
    {
        // En Odoo POS el tipo de método para transferencias o bancos es 'transfer'/'bank'
        var methodId = await _odooGateway.ResolvePaymentMethodIdAsync(posSessionId, "transfer", cancellationToken);
        if (methodId <= 0)
        {
            // Si el POS no tiene configurado diario bancario, fallback controlado al método disponible
            methodId = await _odooGateway.ResolvePaymentMethodIdAsync(posSessionId, "bank", cancellationToken);
        }
        if (methodId <= 0)
        {
            methodId = await _odooGateway.ResolvePaymentMethodIdAsync(posSessionId, "cash", cancellationToken);
        }

        return new PaymentProcessingResult(
            Success: true,
            MethodName: "Transferencia Bancaria",
            AmountPaid: saleOrder.TotalAmount,
            ResolvedOdooMethodId: methodId,
            TransactionReference: $"TRF-{DateTime.UtcNow.Ticks}"
        );
    }
}

/// <summary>
/// [RF-10 / PATRÓN STRATEGY / OCP]:
/// Estrategia concreta de cobro vía Tarjeta (Card / Datáfono).
/// Resuelve el ID de pago tipo 'card'/'tarjeta' en Odoo y asienta el cobro.
/// </summary>
public class CardPaymentProcessor : IPaymentProcessor
{
    private readonly IOdooPosGateway _odooGateway;

    public string MethodName => "Tarjeta";

    public CardPaymentProcessor(IOdooPosGateway odooGateway)
    {
        _odooGateway = odooGateway;
    }

    public bool CanHandle(string paymentMethod)
    {
        if (string.IsNullOrWhiteSpace(paymentMethod)) return false;
        var m = paymentMethod.Trim().ToUpperInvariant();
        return m is "TARJETA" or "CARD";
    }

    public async Task<PaymentProcessingResult> ProcessPaymentAsync(
        SaleOrder saleOrder,
        int posSessionId,
        CancellationToken cancellationToken = default)
    {
        var methodId = await _odooGateway.ResolvePaymentMethodIdAsync(posSessionId, "card", cancellationToken);
        if (methodId <= 0)
        {
            methodId = await _odooGateway.ResolvePaymentMethodIdAsync(posSessionId, "bank", cancellationToken);
        }
        if (methodId <= 0)
        {
            methodId = await _odooGateway.ResolvePaymentMethodIdAsync(posSessionId, "cash", cancellationToken);
        }

        return new PaymentProcessingResult(
            Success: true,
            MethodName: "Tarjeta",
            AmountPaid: saleOrder.TotalAmount,
            ResolvedOdooMethodId: methodId,
            TransactionReference: $"CARD-{DateTime.UtcNow.Ticks}"
        );
    }
}
