import React, { useState, useEffect } from 'react';
import { Dialog } from '../ui/Dialog';
import { Input } from '../ui/Input';
import { Button } from '../ui/Button';
import { Badge } from '../ui/Badge';
import { CheckoutResult } from '@/application/useCases/UseCases';
import { Customer } from '@/application/ports/IPosApiGateway';
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
  Printer,
} from 'lucide-react';

interface PaymentModalProps {
  isOpen: boolean;
  onClose: () => void;
  total: number;
  onConfirmPayment: (
    customerDoc: string,
    customerName: string,
    paymentMethod: string,
    partnerId?: number | null
  ) => Promise<CheckoutResult>;
  processing: boolean;
  lastResult: CheckoutResult | null;
  onSuccessDone: () => void;
  selectedCustomer?: Customer | null;
  onOpenCustomerSelector?: () => void;
}

export const PaymentModal: React.FC<PaymentModalProps> = ({
  isOpen,
  onClose,
  total,
  onConfirmPayment,
  processing,
  lastResult,
  onSuccessDone,
  selectedCustomer,
}) => {
  const [customerDoc, setCustomerDoc] = useState('9999999999999');
  const [customerName, setCustomerName] = useState('Consumidor Final');

  // [GUÍA ESTEBAN - TURNO 3 - RF-10 / SELECTOR DE MÉTODOS DE PAGO]:
  // Soporta Efectivo (por defecto), Transferencia Bancaria y Tarjeta
  const [paymentMethod, setPaymentMethod] = useState<'Efectivo' | 'Transferencia' | 'Tarjeta'>('Efectivo');

  useEffect(() => {
    if (selectedCustomer) {
      setCustomerDoc(selectedCustomer.vat || '9999999999999');
      setCustomerName(selectedCustomer.name);
    } else {
      setCustomerDoc('9999999999999');
      setCustomerName('Consumidor Final');
    }
  }, [selectedCustomer, isOpen]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    await onConfirmPayment(customerDoc, customerName, paymentMethod, selectedCustomer?.id ?? null);
  };

  return (
    <Dialog
      open={isOpen}
      onOpenChange={(open) => {
        if (!open) {
          if (lastResult?.success) {
            onSuccessDone();
          } else {
            onClose();
          }
        }
      }}
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
        <div className="space-y-4 py-1 text-xs" id="printable-invoice">
          {/* Cabecera para impresión física */}
          <div className="hidden print:block text-center border-b border-slate-300 pb-3 mb-2">
            <h2 className="text-base font-bold uppercase tracking-wider text-slate-900">Comprobante de Venta POS</h2>
            <p className="text-xs text-slate-600">Sistema Punto de Venta &bull; Integración Odoo 20 Community</p>
          </div>

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

          {/* 5. Nota de Inmutabilidad y Botones de Acción */}
          <div className="flex items-center gap-1.5 text-[10px] text-slate-500 bg-amber-50/60 border border-amber-200/60 rounded-lg p-2 no-print">
            <Lock className="h-3.5 w-3.5 text-amber-600 shrink-0" />
            <span>
              <strong>Garantía RN-06:</strong> La orden se encuentra cerrada e inmutable en Odoo ERP y SQLite local.
            </span>
          </div>

          <div className="flex flex-col sm:flex-row gap-2.5 pt-2 border-t border-slate-100 no-print">
            <Button
              type="button"
              variant="outline"
              onClick={() => window.print()}
              className="text-xs h-10 border-slate-300 gap-1.5 text-slate-700 hover:text-emerald-700 hover:bg-emerald-50"
              title="Imprimir comprobante físico de la venta"
            >
              <Printer className="h-4 w-4 text-emerald-600" />
              <span>Imprimir Recibo</span>
            </Button>
            <Button
              type="button"
              className="flex-1 bg-emerald-600 hover:bg-emerald-700 text-white font-bold h-10 text-xs shadow-sm"
              onClick={onSuccessDone}
            >
              <span>Finalizar y Nueva Venta</span>
            </Button>
          </div>
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
            {/* Sección de Cliente Asignado en Modo Confirmación (Solo Lectura) */}
            {selectedCustomer ? (
              <div className="p-3 rounded-xl bg-emerald-50/70 border border-emerald-200 text-xs">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <User className="h-4 w-4 text-emerald-700 shrink-0" />
                    <span className="font-bold text-slate-900 text-sm">{selectedCustomer.name}</span>
                  </div>
                  <Badge variant="success" className="font-mono text-[10px]">
                    Odoo Partner #{selectedCustomer.id}
                  </Badge>
                </div>
                <div className="text-slate-600 font-mono text-xs mt-1">
                  RUC / Cédula: <span className="font-bold text-slate-800">{customerDoc}</span>
                </div>
              </div>
            ) : (
              <div className="p-3 rounded-xl bg-slate-50 border border-slate-200 text-xs flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <User className="h-4 w-4 text-slate-500 shrink-0" />
                  <div>
                    <span className="font-bold text-slate-800 text-sm">Consumidor Final</span>
                    <div className="font-mono text-slate-500 text-xs">RUC: 9999999999999</div>
                  </div>
                </div>
                <Badge variant="default" className="text-[10px]">
                  Venta General
                </Badge>
              </div>
            )}

            {/* Cédula / RUC del Cliente (Solo Lectura) */}
            <div>
              <div className="flex items-center justify-between mb-1">
                <label className="text-xs font-semibold text-slate-700 flex items-center gap-1">
                  <User className="h-3.5 w-3.5 text-slate-500" /> Cédula / RUC del Cliente
                </label>
                <span className="text-[11px] text-slate-400 font-medium">Solo lectura (Confirmación)</span>
              </div>
              <Input
                value={customerDoc}
                readOnly
                tabIndex={-1}
                className="bg-slate-100 text-slate-800 font-mono font-bold select-none cursor-not-allowed border-slate-300"
              />
            </div>

            {/* Nombre o Razón Social (Solo Lectura) */}
            <div>
              <div className="flex items-center justify-between mb-1">
                <label className="text-xs font-semibold text-slate-700">
                  Nombre o Razón Social
                </label>
                <span className="text-[11px] text-slate-400 font-medium">Solo lectura (Confirmación)</span>
              </div>
              <Input
                value={customerName}
                readOnly
                tabIndex={-1}
                className="bg-slate-100 text-slate-800 font-semibold select-none cursor-not-allowed border-slate-300"
              />
            </div>

            {/* [GUÍA ESTEBAN - TURNO 3 - RF-10 / SELECTOR DE MÉTODOS DE PAGO]: */}
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1.5 flex items-center gap-1">
                <CreditCard className="h-3.5 w-3.5 text-emerald-600" /> Método de Pago (RF-10)
              </label>
              <div className="grid grid-cols-3 gap-2">
                <button
                  type="button"
                  onClick={() => setPaymentMethod('Efectivo')}
                  className={`p-2 rounded-xl border text-center transition-all ${
                    paymentMethod === 'Efectivo'
                      ? 'border-emerald-600 bg-emerald-50 text-emerald-800 font-bold shadow-sm ring-1 ring-emerald-500'
                      : 'border-slate-200 bg-white hover:bg-slate-50 text-slate-600'
                  }`}
                >
                  <div className="text-xs font-semibold">💵 Efectivo</div>
                  <div className="text-[10px] text-slate-500 mt-0.5">Caja POS</div>
                </button>
                <button
                  type="button"
                  onClick={() => setPaymentMethod('Transferencia')}
                  className={`p-2 rounded-xl border text-center transition-all ${
                    paymentMethod === 'Transferencia'
                      ? 'border-emerald-600 bg-emerald-50 text-emerald-800 font-bold shadow-sm ring-1 ring-emerald-500'
                      : 'border-slate-200 bg-white hover:bg-slate-50 text-slate-600'
                  }`}
                >
                  <div className="text-xs font-semibold">🏦 Transfer.</div>
                  <div className="text-[10px] text-slate-500 mt-0.5">Banco Odoo</div>
                </button>
                <button
                  type="button"
                  onClick={() => setPaymentMethod('Tarjeta')}
                  className={`p-2 rounded-xl border text-center transition-all ${
                    paymentMethod === 'Tarjeta'
                      ? 'border-emerald-600 bg-emerald-50 text-emerald-800 font-bold shadow-sm ring-1 ring-emerald-500'
                      : 'border-slate-200 bg-white hover:bg-slate-50 text-slate-600'
                  }`}
                >
                  <div className="text-xs font-semibold">💳 Tarjeta</div>
                  <div className="text-[10px] text-slate-500 mt-0.5">Datáfono POS</div>
                </button>
              </div>
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
              disabled={processing}
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
