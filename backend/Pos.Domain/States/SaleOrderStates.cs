using Pos.Domain.Entities;
using Pos.Domain.Exceptions;

namespace Pos.Domain.States;

public interface ISaleOrderState
{
    string StateName { get; }
    bool CanModifyLines { get; }
    void Confirm(SaleOrder order);
    void MarkSynced(SaleOrder order, string odooReference, int odooOrderId);
}

public sealed class DraftState : ISaleOrderState
{
    public string StateName => "Draft";
    public bool CanModifyLines => true;

    public void Confirm(SaleOrder order)
    {
        if (order.Lines.Count == 0)
        {
            throw new DomainValidationException(nameof(order.Lines), "No se puede confirmar una orden de venta sin productos (RN-01).");
        }

        order.TransitionToState(new ConfirmedState());
    }

    public void MarkSynced(SaleOrder order, string odooReference, int odooOrderId)
    {
        throw new SaleOrderStateException("Una orden en borrador no puede sincronizarse directamente sin haber sido confirmada previamente.");
    }
}

public sealed class ConfirmedState : ISaleOrderState
{
    public string StateName => "Confirmed";
    public bool CanModifyLines => false;

    public void Confirm(SaleOrder order)
    {
        throw new SaleOrderStateException("La orden de venta ya se encuentra confirmada.");
    }

    public void MarkSynced(SaleOrder order, string odooReference, int odooOrderId)
    {
        if (string.IsNullOrWhiteSpace(odooReference))
            throw new DomainValidationException(nameof(odooReference), "La referencia de Odoo es obligatoria para marcar la sincronización.");

        order.SetOdooDetails(odooReference, odooOrderId);
        order.TransitionToState(new SyncedState());
    }
}

public sealed class SyncedState : ISaleOrderState
{
    public string StateName => "Synced";
    public bool CanModifyLines => false;

    public void Confirm(SaleOrder order)
    {
        throw new SaleOrderStateException("La orden ya fue sincronizada con Odoo y no puede volver a confirmarse.");
    }

    public void MarkSynced(SaleOrder order, string odooReference, int odooOrderId)
    {
        throw new SaleOrderStateException("La orden ya fue sincronizada con Odoo previamente.");
    }
}
