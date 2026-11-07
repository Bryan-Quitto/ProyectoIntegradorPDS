import React, { useState } from 'react';
import { Dialog } from '../ui/Dialog';
import { Input } from '../ui/Input';
import { Button } from '../ui/Button';
import { Badge } from '../ui/Badge';
import { validateEcuadorianId } from '@/domain/ecuadorianIdValidator';
import { CheckoutResult } from '@/application/useCases/UseCases';
import {
  CheckCircle2,
  AlertTriangle,
  User,
  CreditCard,
  FileCheck2,
  Hash,
  Store,
  Receipt,
  Lock,
} from 'lucide-react';

interface PaymentModalProps {
  isOpen: boolean;
  onClose: () => void;
  total: number;
  onConfirmPayment: (customerDoc: string, customerName: string) => Promise<CheckoutResult>;
  processing: boolean;
  lastResult: CheckoutResult | null;
  onSuccessDone: () => void;
}

export const PaymentModal: React.FC<PaymentModalProps> = ({
  isOpen,
  onClose,
  total,
  onConfirmPayment,
  processing,
  lastResult,
  onSuccessDone,
}) => {
  const [customerDoc, setCustomerDoc] = useState('9999999999999');
  const [customerName, setCustomerName] = useState('Consumidor Final');
  const [idError, setIdError] = useState<string | null>(null);

  const handleDocChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value.trim();
    setCustomerDoc(val);

    if (val && val !== '9999999999999') {
      const res = validateEcuadorianId(val);
      setIdError(res.isValid ? null : res.error || 'Identificación inválida.');
    } else {
      setIdError(null);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (customerDoc !== '9999999999999') {
      const res = validateEcuadorianId(customerDoc);
      if (!res.isValid) {
        setIdError(res.error || 'Cédula o RUC incorrecto.');
        return;
      }
    }

    await onConfirmPayment(customerDoc, customerName);
  };

  return (
    <Dialog
      open={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={
        lastResult?.success ? (
          <div className="flex items-center gap-2 text-emerald-700">
            <CheckCircle2 className="h-5 w-5 text-emerald-600" />
            <span>¡Venta Sincronizada con Odoo!</span>
          </div>
        ) : (
          <div className="flex items-center gap-2 text-slate-900">
            <CreditCard className="h-5 w-5 text-emerald-600" />
            <span>Finalizar Cobro POS</span>
          </div>
        )
      }
      description={
        lastResult?.success
          ? 'La orden fue registrada y confirmada con éxito en el POS de Odoo 20 Community.'
          : `Total a liquidar y sincronizar en Odoo 20: $${total.toFixed(2)}`
      }
      className={lastResult?.success ? "max-w-xl" : "max-w-md"}
    >
      {lastResult?.success ? (
        <div className="space-y-4 py-1 text-xs">
          {/* 1. Cabecera de Trazabilidad Oficial de Odoo ERP */}
          <div className="bg-emerald-50/80 border border-emerald-200 rounded-xl p-3.5 flex items-center justify-between gap-3">
            <div className="flex items-center gap-2.5">
              <div className="h-9 w-9 rounded-full bg-emerald-600 text-white flex items-center justify-center font-bold text-sm shadow-sm shrink-0">
                <FileCheck2 className="h-5 w-5" />
              </div>
              <div>
                <p className="text-[10px] uppercase tracking-wider text-emerald-800 font-bold">
                  Referencia Oficial Odoo 20
                </p>
                <p className="text-sm font-bold text-slate-900 font-mono flex items-center gap-1.5">
                  {lastResult.order?.odooOrderReference || 'POS-ORDER'}
                  {lastResult.order?.odooOrderId && (
                    <span className="text-[11px] font-normal text-emerald-700 bg-emerald-100 px-2 py-0.5 rounded">
                      ERP #{lastResult.order.odooOrderId}
                    </span>
                  )}
                </p>
              </div>
            </div>
            <Badge variant="success" className="bg-emerald-100 text-emerald-800 border-emerald-300 font-semibold text-[10px]">
              <CheckCircle2 className="h-3 w-3 mr-1" />
              {lastResult.order?.state === 'Synced' || lastResult.order?.state === 'paid' ? 'Sincronizada (paid)' : lastResult.order?.state || 'Confirmada'}
            </Badge>
          </div>

          {/* 2. Metadatos de Cliente y Sesión */}
          <div className="grid grid-cols-2 gap-2.5 bg-slate-50 border border-slate-200/80 rounded-xl p-3 text-[11px]">
            <div>
              <span className="text-slate-400 font-medium flex items-center gap-1 mb-0.5">
                <User className="h-3 w-3 text-slate-500" />
                Cliente
              </span>
              <p className="font-semibold text-slate-800 truncate" title={lastResult.order?.customerName}>
                {lastResult.order?.customerName || 'Consumidor Final'}
              </p>
              <p className="font-mono text-slate-500 text-[10px]">
                {lastResult.order?.customerDocument || '9999999999999'}
              </p>
            </div>
            <div>
              <span className="text-slate-400 font-medium flex items-center gap-1 mb-0.5">
                <Store className="h-3 w-3 text-slate-500" />
                Sesión / Operador
              </span>
              <p className="font-semibold text-slate-800 truncate">
                {lastResult.order?.sessionName || `Sesión #${lastResult.order?.posSessionId}`}
              </p>
              <p className="text-slate-500 text-[10px]">
                {lastResult.order?.operatorName || 'Cajero POS'}
              </p>
            </div>
          </div>

          {/* 3. Desglose Inmutable de Ítems Vendidos (RN-06) */}
          {lastResult.order?.lines && lastResult.order.lines.length > 0 && (
            <div className="border border-slate-200 rounded-xl overflow-hidden">
              <div className="bg-slate-100/80 px-3 py-1.5 border-b border-slate-200 flex justify-between items-center text-[11px] font-bold text-slate-700">
                <span className="flex items-center gap-1">
                  <Receipt className="h-3.5 w-3.5 text-slate-500" />
                  Ítems Facturados ({lastResult.order.lines.length})
                </span>
                <span className="font-mono text-[10px] text-slate-500 flex items-center gap-1">
                  <Lock className="h-3 w-3 text-amber-600" /> Inmutable (RN-06)
                </span>
              </div>
              <div className="max-h-36 overflow-y-auto divide-y divide-slate-100 bg-white">
                {lastResult.order.lines.map((line, idx) => (
                  <div key={`${line.productId}-${idx}`} className="px-3 py-2 flex items-center justify-between hover:bg-slate-50">
                    <div className="min-w-0 pr-2">
                      <p className="font-semibold text-slate-800 text-[11px] truncate">{line.productName}</p>
                      <p className="text-[10px] text-slate-400 font-mono">
                        {line.quantity} × ${(line.unitPrice ?? 0).toFixed(2)}
                        {line.productCode ? ` • ${line.productCode}` : ''}
                      </p>
                    </div>
                    <div className="text-right shrink-0 font-mono font-bold text-slate-800 text-[11px]">
                      ${(line.totalWithTax ?? line.subTotal ?? 0).toFixed(2)}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* 4. Resumen Financiero y Fiscal Oficial */}
          <div className="bg-slate-50 border border-slate-200 rounded-xl p-3 space-y-1.5 text-[11px]">
            <div className="flex justify-between text-slate-600">
              <span>Base Imponible (Neto sin IVA):</span>
              <span className="font-mono">
                ${Math.max(0, (lastResult.order?.totalAmount ?? 0) - (lastResult.order?.taxAmount ?? 0)).toFixed(2)}
              </span>
            </div>
            <div className="flex justify-between text-slate-600">
              <span>IVA Ecuador (15%):</span>
              <span className="font-mono font-medium text-slate-800">
                ${(lastResult.order?.taxAmount ?? 0).toFixed(2)}
              </span>
            </div>
            {lastResult.order?.discountAmount ? (
              <div className="flex justify-between text-rose-600 font-medium">
                <span>Descuento Aplicado:</span>
                <span className="font-mono">-${lastResult.order.discountAmount.toFixed(2)}</span>
              </div>
            ) : null}
            <div className="flex justify-between items-center border-t border-slate-200 pt-2 text-sm font-black text-emerald-800">
              <span>Total Liquidado ({lastResult.order?.paymentMethod || 'Efectivo'}):</span>
              <span className="text-base text-emerald-700 font-mono">
                ${(lastResult.order?.totalAmount ?? 0).toFixed(2)}
              </span>
            </div>
          </div>

          {/* 5. Nota de Inmutabilidad y Botón de Nueva Venta */}
          <div className="flex items-center gap-1.5 text-[10px] text-slate-500 bg-amber-50/60 border border-amber-200/60 rounded-lg p-2">
            <Lock className="h-3.5 w-3.5 text-amber-600 shrink-0" />
            <span>
              <strong>Garantía RN-06:</strong> La orden se encuentra cerrada e inmutable en Odoo ERP y SQLite local.
            </span>
          </div>

          <Button
            className="w-full bg-emerald-600 hover:bg-emerald-700 text-white font-bold h-10 text-xs shadow-sm"
            onClick={onSuccessDone}
          >
            Completar y Nueva Venta
          </Button>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="space-y-4">
          {lastResult && !lastResult.success && (
            <div className="p-3 bg-rose-50 border border-rose-200 rounded-lg flex items-start gap-2 text-rose-700 text-xs">
              <AlertTriangle className="h-4 w-4 shrink-0 mt-0.5" />
              <div>
                <p className="font-bold">Error de Sincronización (RN-07):</p>
                <p>{lastResult.errorMessage}</p>
              </div>
            </div>
          )}

          <div className="space-y-3">
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1 flex items-center gap-1">
                <User className="h-3.5 w-3.5" /> Cédula / RUC del Cliente
              </label>
              <Input
                value={customerDoc}
                onChange={handleDocChange}
                placeholder="9999999999999 para Consumidor Final"
                className={idError ? 'border-rose-500 focus:ring-rose-500' : ''}
              />
              {idError && <p className="text-xs text-rose-600 mt-1">{idError}</p>}
              <div className="flex gap-2 mt-1">
                <button
                  type="button"
                  onClick={() => {
                    setCustomerDoc('9999999999999');
                    setCustomerName('Consumidor Final');
                    setIdError(null);
                  }}
                  className="text-[10px] text-slate-500 hover:text-emerald-600 underline"
                >
                  Usar Consumidor Final
                </button>
              </div>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">
                Nombre o Razón Social
              </label>
              <Input
                value={customerName}
                onChange={(e) => setCustomerName(e.target.value)}
                placeholder="Ej. Juan Pérez"
              />
            </div>

            <div className="p-3 bg-slate-50 rounded-xl border border-slate-200">
              <span className="text-xs text-slate-500 block mb-1">Monto Total a Cobrar:</span>
              <span className="text-2xl font-black text-emerald-600">${total.toFixed(2)}</span>
            </div>
          </div>

          <div className="flex gap-2 pt-2 border-t border-slate-100">
            <Button
              type="button"
              variant="outline"
              className="flex-1"
              onClick={onClose}
              disabled={processing}
            >
              Cancelar
            </Button>
            <Button
              type="submit"
              className="flex-1 bg-emerald-600 hover:bg-emerald-700 text-white font-bold"
              disabled={processing || !!idError}
            >
              {processing ? (
                <div className="flex items-center gap-1.5 justify-center">
                  <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white"></div>
                  <span>Sincronizando...</span>
                </div>
              ) : (
                'Confirmar Pago'
              )}
            </Button>
          </div>
        </form>
      )}
    </Dialog>
  );
};
