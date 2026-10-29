import React from 'react';
import { useSalesHistory } from '../hooks/useSalesHistory';
import { Card, CardHeader, CardTitle, CardContent } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { Badge } from '../components/ui/Badge';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '../components/ui/Table';
import { ArrowLeft, RefreshCw, FileText, CheckCircle2 } from 'lucide-react';

interface SalesHistoryPageProps {
  onBackToTerminal: () => void;
}

export const SalesHistoryPage: React.FC<SalesHistoryPageProps> = ({ onBackToTerminal }) => {
  const { sales, loading, error, refresh } = useSalesHistory();

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col">
      <header className="h-14 bg-white border-b border-slate-200 px-6 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="sm" onClick={onBackToTerminal} className="h-8 gap-1.5">
            <ArrowLeft className="h-4 w-4" />
            Volver al Terminal
          </Button>
          <h1 className="text-sm font-bold text-slate-800">Historial de Ventas Sincronizadas</h1>
        </div>

        <Button variant="outline" size="sm" onClick={() => refresh()} className="h-8 gap-1.5">
          <RefreshCw className="h-3.5 w-3.5" />
          Actualizar
        </Button>
      </header>

      <main className="flex-1 p-6 max-w-6xl w-full mx-auto">
        <Card className="shadow-sm border-slate-200">
          <CardHeader className="pb-3 border-b flex flex-row items-center justify-between">
            <div>
              <CardTitle className="text-base font-bold flex items-center gap-2">
                <FileText className="h-5 w-5 text-emerald-600" />
                Órdenes Registradas (Persistencia Local de Auditoría)
              </CardTitle>
              <p className="text-xs text-slate-500 mt-1">
                Garantiza trazabilidad local e idempotencia (RN-08) sin clonar catálogo de Odoo.
              </p>
            </div>
            <Badge variant="default">{sales.length} órdenes</Badge>
          </CardHeader>

          <CardContent className="p-0">
            {loading ? (
              <div className="p-12 text-center text-slate-400">
                <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-emerald-600 mx-auto mb-2"></div>
                <span>Cargando historial...</span>
              </div>
            ) : error ? (
              <div className="p-8 text-center text-rose-600 text-sm">
                <p className="font-semibold">Error al cargar:</p>
                <p>{error}</p>
              </div>
            ) : sales.length === 0 ? (
              <div className="p-12 text-center text-slate-400">
                <p className="font-medium">No se han registrado ventas en esta sesión.</p>
                <p className="text-xs mt-1">Regresa al terminal para procesar una nueva orden.</p>
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow className="bg-slate-50/50">
                    <TableHead className="w-24">ID Local</TableHead>
                    <TableHead>Referencia Odoo</TableHead>
                    <TableHead>Cliente</TableHead>
                    <TableHead>Fecha Sincronización</TableHead>
                    <TableHead>Estado</TableHead>
                    <TableHead className="text-right">Total</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {sales.map((sale) => (
                    <TableRow key={sale.orderId} className="hover:bg-slate-50/80">
                      <TableCell className="font-mono text-xs text-slate-500">
                        {sale.orderId.substring(0, 8)}...
                      </TableCell>
                      <TableCell className="font-semibold text-slate-800 flex items-center gap-1.5">
                        <CheckCircle2 className="h-4 w-4 text-emerald-600" />
                        {sale.odooOrderReference || `Odoo #${sale.odooOrderId}`}
                      </TableCell>
                      <TableCell>
                        <span className="font-medium text-xs text-slate-700 block">{sale.customerName}</span>
                        <span className="text-[11px] text-slate-400 font-mono">{sale.customerDocument}</span>
                      </TableCell>
                      <TableCell className="text-xs text-slate-500">
                        {new Date(sale.createdAtUtc).toLocaleString('es-EC')}
                      </TableCell>
                      <TableCell>
                        <Badge
                          variant="success"
                          className="bg-emerald-50 text-emerald-700 border-emerald-300 text-[10px]"
                        >
                          {sale.state}
                        </Badge>
                      </TableCell>
                      <TableCell className="text-right font-bold text-slate-900">
                        ${sale.totalAmount.toFixed(2)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      </main>
    </div>
  );
};
