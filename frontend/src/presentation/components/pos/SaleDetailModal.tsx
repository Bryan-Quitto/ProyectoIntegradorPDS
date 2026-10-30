import React from 'react';
import { Dialog } from '../ui/Dialog';
import { Badge } from '../ui/Badge';
import { Button } from '../ui/Button';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '../ui/Table';
import { SaleOrderDetailDto } from '@/application/ports/IPosApiGateway';
import { formatEcDate, formatEcTime } from '@/presentation/utils/dateUtils';
import {
  CheckCircle2,
  Calendar,
  User,
  CreditCard,
  Receipt,
  Store,
  Tag,
  Hash,
  FileCheck2,
} from 'lucide-react';

interface SaleDetailModalProps {
  open: boolean;
  onClose: () => void;
  sale: SaleOrderDetailDto | null;
  loading?: boolean;
}

export const SaleDetailModal: React.FC<SaleDetailModalProps> = ({
  open,
  onClose,
  sale,
  loading = false,
}) => {
  if (!sale && !loading) return null;

  const formatCurrency = (val: number | undefined) => `$${(val ?? 0).toFixed(2)}`;

  const getStatusBadge = (state: string) => {
    switch (state?.toLowerCase()) {
      case 'synced':
      case 'paid':
        return {
          label: 'Sincronizada / Pagada',
          className: 'bg-emerald-50 text-emerald-700 border-emerald-300 font-semibold',
        };
      case 'confirmed':
        return {
          label: 'Confirmada',
          className: 'bg-blue-50 text-blue-700 border-blue-300 font-semibold',
        };
      case 'draft':
        return {
          label: 'Borrador',
          className: 'bg-amber-50 text-amber-700 border-amber-300 font-semibold',
        };
      default:
        return {
          label: state || 'Procesada',
          className: 'bg-slate-50 text-slate-700 border-slate-300 font-semibold',
        };
    }
  };

  const status = sale ? getStatusBadge(sale.state) : null;

  return (
    <Dialog
      open={open}
      onOpenChange={(isOpen) => !isOpen && onClose()}
      className="max-w-3xl"
      title={
        <div className="flex items-center gap-2 text-slate-900">
          <Receipt className="h-5 w-5 text-emerald-600" />
          <span>Detalle de Venta</span>
        </div>
      }
      description={
        sale ? (
          <span className="flex items-center gap-2 mt-0.5">
            <span className="font-mono text-xs text-slate-500">ID Local: {sale.orderId}</span>
          </span>
        ) : undefined
      }
    >
      {loading ? (
        <div className="p-12 text-center text-slate-400">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-emerald-600 mx-auto mb-2" />
          <span>Cargando detalle de la orden...</span>
        </div>
      ) : sale ? (
        <div className="space-y-5">
          {/* 1. Cabecera destacada de Odoo 20 y Estado */}
          <div className="bg-emerald-50/70 border border-emerald-200/80 rounded-lg p-3.5 flex flex-wrap items-center justify-between gap-3">
            <div className="flex items-center gap-2.5">
              <div className="h-9 w-9 rounded-full bg-emerald-600 text-white flex items-center justify-center font-bold text-sm shadow-sm">
                <FileCheck2 className="h-5 w-5" />
              </div>
              <div>
                <p className="text-xs uppercase tracking-wider text-emerald-800 font-semibold">
                  Referencia Oficial Odoo 20
                </p>
                <p className="text-base font-bold text-slate-900 font-mono flex items-center gap-1.5">
                  {sale.odooOrderReference || (sale.odooOrderId ? `Odoo #${sale.odooOrderId}` : 'Pendiente')}
                  {sale.odooOrderId && (
                    <span className="text-xs font-normal text-emerald-700 bg-emerald-100/80 px-2 py-0.5 rounded">
                      ID Odoo: {sale.odooOrderId}
                    </span>
                  )}
                </p>
              </div>
            </div>

            {status && (
              <Badge variant="success" className={status.className}>
                <CheckCircle2 className="h-3.5 w-3.5 mr-1" />
                {status.label}
              </Badge>
            )}
          </div>

          {/* 2. Grid de Metadatos: Sesión, Operador, Cliente, Fecha */}
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 bg-slate-50 border border-slate-200/80 rounded-lg p-3.5 text-xs">
            {/* Sesión POS */}
            <div className="space-y-1">
              <span className="text-slate-400 font-medium flex items-center gap-1">
                <Store className="h-3.5 w-3.5 text-slate-500" />
                Sesión POS
              </span>
              <p className="font-semibold text-slate-800">
                {sale.sessionName || `Sesión #${sale.posSessionId}`}
              </p>
              <p className="text-[11px] text-slate-500 font-mono">ID: {sale.posSessionId}</p>
            </div>

            {/* Operador / Cajero */}
            <div className="space-y-1">
              <span className="text-slate-400 font-medium flex items-center gap-1">
                <User className="h-3.5 w-3.5 text-slate-500" />
                Cajero / Operador
              </span>
              <p className="font-semibold text-slate-800">
                {sale.operatorName || 'Cajero POS'}
              </p>
              <p className="text-[11px] text-slate-500">Terminal Principal</p>
            </div>

            {/* Cliente */}
            <div className="space-y-1">
              <span className="text-slate-400 font-medium flex items-center gap-1">
                <User className="h-3.5 w-3.5 text-slate-500" />
                Cliente
              </span>
              <p className="font-semibold text-slate-800 truncate" title={sale.customerName}>
                {sale.customerName || 'Consumidor Final'}
              </p>
              <p className="text-[11px] text-slate-500 font-mono">
                {sale.customerDocument || '9999999999999'}
              </p>
            </div>

            {/* Fecha y Hora (Zona Horaria Ecuador UTC-5) */}
            <div className="space-y-1">
              <span className="text-slate-400 font-medium flex items-center gap-1">
                <Calendar className="h-3.5 w-3.5 text-slate-500" />
                Fecha y Hora (UTC-5)
              </span>
              <p className="font-semibold text-slate-800">
                {formatEcDate(sale.createdAtUtc)}
              </p>
              <p className="text-[11px] text-slate-500 font-mono">
                {formatEcTime(sale.createdAtUtc)}
              </p>
            </div>
          </div>

          {/* 3. Tabla Desglosada de Productos */}
          <div>
            <h3 className="text-xs font-bold uppercase tracking-wider text-slate-600 mb-2 flex items-center gap-1.5">
              <Hash className="h-3.5 w-3.5 text-slate-500" />
              Ítems de la Orden ({sale.lines.length})
            </h3>
            <div className="border border-slate-200 rounded-lg overflow-hidden">
              <Table>
                <TableHeader>
                  <TableRow className="bg-slate-100/70 text-[11px]">
                    <TableHead className="py-2">Producto</TableHead>
                    <TableHead className="py-2 text-center w-16">Cant.</TableHead>
                    <TableHead className="py-2 text-right w-24">P. Unitario</TableHead>
                    <TableHead className="py-2 text-right w-24">Impuesto (IVA)</TableHead>
                    <TableHead className="py-2 text-right w-24 font-bold">Total Línea</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {sale.lines.map((line, idx) => {
                    const lineTotal = line.totalWithTax ?? line.subTotal;
                    const taxAmt = line.taxAmount ?? (lineTotal * 0.15 / 1.15);
                    const netSub = line.netSubTotal ?? (lineTotal - taxAmt);

                    return (
                      <TableRow key={`${line.productId}-${idx}`} className="text-xs hover:bg-slate-50">
                        <TableCell className="py-2.5">
                          <div className="font-semibold text-slate-900">{line.productName}</div>
                          {line.productCode && (
                            <span className="text-[11px] font-mono text-emerald-700 bg-emerald-50 px-1.5 py-0.5 rounded">
                              {line.productCode}
                            </span>
                          )}
                        </TableCell>
                        <TableCell className="py-2.5 text-center font-mono font-medium">
                          {line.quantity}
                        </TableCell>
                        <TableCell className="py-2.5 text-right font-mono text-slate-700">
                          {formatCurrency(line.unitPrice)}
                        </TableCell>
                        <TableCell className="py-2.5 text-right text-slate-600">
                          <div className="font-mono">{formatCurrency(taxAmt)}</div>
                          <span className="text-[10px] text-slate-400">15% IVA</span>
                        </TableCell>
                        <TableCell className="py-2.5 text-right font-mono font-bold text-slate-900">
                          {formatCurrency(lineTotal)}
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          </div>

          {/* 4. Resumen Financiero y Detalle de Pago */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 pt-1">
            {/* Sección de Pago */}
            <div className="bg-slate-50 border border-slate-200 rounded-lg p-3.5 flex flex-col justify-between">
              <div>
                <h4 className="text-xs font-bold uppercase tracking-wider text-slate-600 mb-2.5 flex items-center gap-1.5">
                  <CreditCard className="h-4 w-4 text-emerald-600" />
                  Método de Pago Liquidado
                </h4>
                <div className="space-y-2 text-xs">
                  <div className="flex justify-between items-center py-1 border-b border-slate-200/60">
                    <span className="text-slate-500">Forma de Pago:</span>
                    <span className="font-semibold text-slate-800 bg-emerald-50 text-emerald-800 px-2 py-0.5 rounded border border-emerald-200">
                      {sale.paymentMethod || 'Efectivo'}
                    </span>
                  </div>
                  <div className="flex justify-between items-center py-1 border-b border-slate-200/60">
                    <span className="text-slate-500">Monto Cobrado:</span>
                    <span className="font-bold text-slate-900 font-mono">
                      {formatCurrency(sale.amountPaid || sale.totalAmount)}
                    </span>
                  </div>
                  <div className="flex justify-between items-center py-1">
                    <span className="text-slate-500">Cambio / Vuelto:</span>
                    <span className="font-mono text-slate-600">$0.00</span>
                  </div>
                </div>
              </div>

              <div className="mt-3 pt-2 text-[11px] text-slate-500 border-t border-slate-200/60 flex items-center gap-1">
                <CheckCircle2 className="h-3.5 w-3.5 text-emerald-600 shrink-0" />
                <span>Transacción conciliada automáticamente con caja en Odoo 20.</span>
              </div>
            </div>

            {/* Resumen Financiero */}
            <div className="bg-slate-50/70 border border-slate-200 rounded-lg p-3.5 text-xs space-y-2">
              <h4 className="text-xs font-bold uppercase tracking-wider text-slate-600 mb-2.5 flex items-center gap-1.5">
                <Tag className="h-4 w-4 text-slate-500" />
                Resumen Financiero
              </h4>

              {/* Subtotal base */}
              <div className="flex justify-between items-center text-slate-600">
                <span>Subtotal Base (Bruto):</span>
                <span className="font-mono">{formatCurrency(sale.subTotal)}</span>
              </div>

              {/* Descuentos */}
              <div className="flex justify-between items-center text-slate-600">
                <div className="flex items-center gap-1">
                  <span>Descuento:</span>
                  {sale.discountPolicyName && sale.discountPolicyName !== 'Sin Descuento' && (
                    <span className="text-[10px] text-purple-700 bg-purple-50 px-1.5 py-0.5 rounded border border-purple-200">
                      {sale.discountPolicyName}
                    </span>
                  )}
                </div>
                <span className={`font-mono ${sale.discountAmount > 0 ? 'text-rose-600 font-semibold' : ''}`}>
                  -{formatCurrency(sale.discountAmount)}
                </span>
              </div>

              {/* Subtotal Neto sin impuestos */}
              <div className="flex justify-between items-center text-slate-600 pt-1 border-t border-slate-200/60">
                <span>Base Imponible (Neto sin IVA):</span>
                <span className="font-mono font-medium">
                  {formatCurrency(Math.max(0, (sale.totalAmount ?? 0) - (sale.taxAmount ?? 0)))}
                </span>
              </div>

              {/* Impuesto IVA 15% */}
              <div className="flex justify-between items-center text-slate-600">
                <span>Impuesto IVA (15%):</span>
                <span className="font-mono font-medium text-slate-800">
                  {formatCurrency(sale.taxAmount)}
                </span>
              </div>

              {/* Total final */}
              <div className="flex justify-between items-center pt-2.5 border-t-2 border-slate-300 text-sm font-bold text-slate-900">
                <span className="text-emerald-800">Total Liquidado:</span>
                <span className="text-base text-emerald-700 font-mono">
                  {formatCurrency(sale.totalAmount)}
                </span>
              </div>
            </div>
          </div>

          {/* Botón inferior de cerrar */}
          <div className="pt-2 flex justify-end">
            <Button variant="outline" onClick={onClose} className="h-9 px-5">
              Cerrar
            </Button>
          </div>
        </div>
      ) : null}
    </Dialog>
  );
};
