import React, { useState } from 'react';
import { Dialog } from '../ui/Dialog';
import { Input } from '../ui/Input';
import { Button } from '../ui/Button';
import { validateEcuadorianId } from '@/domain/ecuadorianIdValidator';
import { CheckoutResult } from '@/application/useCases/UseCases';
import { CheckCircle2, AlertTriangle, User, CreditCard } from 'lucide-react';

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
    <Dialog open={isOpen} onOpenChange={onClose}>
      <div className="p-6 max-w-md w-full bg-white rounded-2xl shadow-xl">
        {lastResult?.success ? (
          <div className="text-center py-4 space-y-4">
            <CheckCircle2 className="h-16 w-16 text-emerald-600 mx-auto animate-bounce" />
            <h3 className="text-xl font-bold text-slate-900">¡Venta Sincronizada con Odoo!</h3>
            <p className="text-sm text-slate-500">
              La orden fue registrada y confirmada con éxito en el POS de Odoo 20 Community.
            </p>

            <div className="bg-slate-50 p-4 rounded-xl text-left text-xs space-y-1.5 border border-slate-100">
              <div className="flex justify-between">
                <span className="text-slate-500">Referencia POS:</span>
                <span className="font-mono font-bold text-slate-800">
                  {lastResult.order?.odooOrderReference || 'POS-ORDER'}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">ID en Odoo ERP:</span>
                <span className="font-mono font-bold text-slate-800">
                  #{lastResult.order?.odooOrderId || 'N/A'}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Cliente:</span>
                <span className="font-semibold text-slate-800">{lastResult.order?.customerName}</span>
              </div>
              <div className="flex justify-between border-t border-slate-200 pt-1 text-sm font-extrabold text-emerald-700">
                <span>Monto Cobrado:</span>
                <span>${lastResult.order?.totalAmount.toFixed(2)}</span>
              </div>
            </div>

            <Button
              className="w-full bg-emerald-600 hover:bg-emerald-700 text-white font-bold"
              onClick={onSuccessDone}
            >
              Completar y Nueva Venta
            </Button>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="flex items-center gap-2 pb-2 border-b">
              <CreditCard className="h-6 w-6 text-emerald-600" />
              <div>
                <h3 className="text-lg font-bold text-slate-900">Finalizar Cobro POS</h3>
                <p className="text-xs text-slate-500">Total a sincronizar en Odoo: ${total.toFixed(2)}</p>
              </div>
            </div>

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

            <div className="flex gap-2 pt-2">
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
                  <div className="flex items-center gap-1.5">
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
      </div>
    </Dialog>
  );
};
