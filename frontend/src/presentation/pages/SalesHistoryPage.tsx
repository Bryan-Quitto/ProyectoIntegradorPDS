import React, { useState, useMemo, useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useSalesHistory } from '../hooks/useSalesHistory';
import { useSaleDetail } from '../hooks/useSaleDetail';
import { Card, CardHeader, CardTitle, CardContent } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import { Badge } from '../components/ui/Badge';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '../components/ui/Table';
import { Pagination } from '../components/ui/Pagination';
import { SaleDetailModal } from '../components/pos/SaleDetailModal';
import { formatEcDateTime } from '../utils/dateUtils';
import {
  ArrowLeft,
  RefreshCw,
  FileText,
  CheckCircle2,
  Eye,
  AlertCircle,
  Search,
  Filter,
  X,
  Inbox,
} from 'lucide-react';

export type SaleHistoryFilterType = 'ALL' | 'REFERENCE' | 'CUSTOMER';

interface SalesHistoryPageProps {
  onBackToTerminal?: () => void;
}

export const SalesHistoryPage: React.FC<SalesHistoryPageProps> = ({ onBackToTerminal }) => {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  const handleBack = () => {
    if (onBackToTerminal) {
      onBackToTerminal();
    } else {
      navigate('/');
    }
  };

  const { sales, loading, error, refresh } = useSalesHistory();
  const {
    sale: selectedSale,
    loading: detailLoading,
    isOpen: isDetailOpen,
    openSaleDetail,
    closeSaleDetail,
  } = useSaleDetail();

  // Lectura inicial de parámetros URL (filtros persistentes y compartibles)
  const querySearch = searchParams.get('buscar') || searchParams.get('q') || '';
  const rawFilter = (searchParams.get('filtro') || searchParams.get('filter') || 'ALL').toUpperCase();
  const queryFilter: SaleHistoryFilterType =
    rawFilter === 'REFERENCE' || rawFilter === 'CUSTOMER' ? rawFilter : 'ALL';
  const queryPage = Math.max(1, parseInt(searchParams.get('pagina') || searchParams.get('page') || '1', 10) || 1);

  const [searchTerm, setSearchTerm] = useState(querySearch);
  const [filterType, setFilterType] = useState<SaleHistoryFilterType>(queryFilter);
  const [currentPage, setCurrentPage] = useState(queryPage);
  const PAGE_SIZE = 5; // 5 órdenes por página en el historial

  // Sincronizar estado local hacia los parámetros de la URL
  useEffect(() => {
    const params: Record<string, string> = {};
    if (searchTerm.trim()) {
      params.buscar = searchTerm.trim();
    }
    if (filterType !== 'ALL') {
      params.filtro = filterType;
    }
    if (currentPage > 1) {
      params.pagina = String(currentPage);
    }
    setSearchParams(params, { replace: true });
  }, [searchTerm, filterType, currentPage, setSearchParams]);

  // Reset de página al cambiar término de búsqueda o tipo de filtro
  useEffect(() => {
    setCurrentPage(1);
  }, [searchTerm, filterType]);

  // Filtrado reactivo en memoria:
  // - Referencia Odoo: 'contains' sobre odooOrderReference (ej: '260-1-000004', '0004', '4').
  //   Para evitar falsos positivos con dígitos aleatorios de GUIDs hexadecimales,
  //   el ID local solo se evalúa si el término tiene al menos 4 caracteres y con 'startsWith'.
  // - Cliente Nombre: 'contains' (permite buscar por apellido ej. 'Perez').
  // - Cédula / RUC: 'startsWith' (búsqueda natural por código de provincia '17...', '09...' o '999...').
  const filteredSales = useMemo(() => {
    const term = searchTerm.trim().toLowerCase();
    if (!term) return sales;

    return sales.filter((sale) => {
      const odooRef = (sale.odooOrderReference ?? '').toLowerCase();
      const odooId = sale.odooOrderId ? String(sale.odooOrderId) : '';
      const localId = (sale.orderId ?? '').toLowerCase();
      const clientName = (sale.customerName ?? '').toLowerCase();
      const clientDoc = (sale.customerDocument ?? '').trim();

      // Criterio de Referencia:
      // Busca en la Referencia oficial Odoo (ej. '260-1-000004' contiene '4' o '000004')
      // o el ID Odoo exacto. Para el ID local GUID solo si el término tiene >= 4 caracteres y empieza con él.
      const matchReference =
        odooRef.includes(term) ||
        odooId === term ||
        (term.length >= 4 && localId.startsWith(term));

      // Criterio de Cliente:
      // - Nombre / Razón Social: contains
      // - Cédula / RUC: startsWith
      const matchCustomer =
        clientName.includes(term) || clientDoc.startsWith(term);

      if (filterType === 'REFERENCE') return matchReference;
      if (filterType === 'CUSTOMER') return matchCustomer;
      return matchReference || matchCustomer;
    });
  }, [sales, searchTerm, filterType]);

  const totalPages = Math.max(1, Math.ceil(filteredSales.length / PAGE_SIZE));

  const pagedSales = useMemo(() => {
    const start = (currentPage - 1) * PAGE_SIZE;
    return filteredSales.slice(start, start + PAGE_SIZE);
  }, [filteredSales, currentPage, PAGE_SIZE]);

  // Localización estricta de estados de venta (Español Estricto)
  const getSaleStateBadge = (state: string) => {
    switch (state?.toLowerCase()) {
      case 'synced':
      case 'paid':
        return {
          label: 'Sincronizada / Pagada',
          className: 'bg-emerald-50 text-emerald-700 border-emerald-300 font-semibold text-[10px]',
        };
      case 'confirmed':
        return {
          label: 'Confirmada',
          className: 'bg-blue-50 text-blue-700 border-blue-300 font-semibold text-[10px]',
        };
      case 'draft':
        return {
          label: 'Borrador',
          className: 'bg-amber-50 text-amber-700 border-amber-300 font-semibold text-[10px]',
        };
      default:
        return {
          label: state || 'Procesada',
          className: 'bg-slate-50 text-slate-700 border-slate-300 font-semibold text-[10px]',
        };
    }
  };

  const getPlaceholder = () => {
    switch (filterType) {
      case 'REFERENCE':
        return 'Buscar por referencia Odoo o ID local (ej. 260-1, 000003, ID Odoo)...';
      case 'CUSTOMER':
        return 'Buscar por cliente o identificación (ej. Juan, Pérez, 1712...)...';
      case 'ALL':
      default:
        return 'Buscar por referencia Odoo, cliente, cédula o ID local...';
    }
  };

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col">
      {/* Barra superior de navegación */}
      <header className="h-14 bg-white border-b border-slate-200 px-6 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="sm" onClick={handleBack} className="h-8 gap-1.5" title="Volver al Terminal POS">
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

      {/* Contenido principal */}
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
            <Badge variant="default">
              {filteredSales.length === sales.length
                ? `${sales.length} órdenes`
                : `${filteredSales.length} de ${sales.length} órdenes`}
            </Badge>
          </CardHeader>

          {/* Barra de Búsqueda y Filtros de Órdenes */}
          <div className="p-4 bg-slate-50/70 border-b border-slate-200 space-y-2.5">
            <div className="flex flex-col sm:flex-row gap-3 items-stretch sm:items-center justify-between">
              {/* Input de Búsqueda */}
              <div className="relative flex-1 max-w-xl">
                <Search className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
                <Input
                  placeholder={getPlaceholder()}
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="pl-9 pr-8 bg-white h-9 text-xs shadow-xs"
                />
                {searchTerm && (
                  <button
                    type="button"
                    onClick={() => setSearchTerm('')}
                    className="absolute right-2.5 top-2.5 text-slate-400 hover:text-slate-600"
                    title="Limpiar búsqueda"
                  >
                    <X className="h-4 w-4" />
                  </button>
                )}
              </div>

              {/* Píldoras de Filtro: Todos | Referencia | Cliente */}
              <div className="flex items-center gap-1.5 shrink-0 text-xs">
                <span className="text-[11px] font-medium text-slate-400 flex items-center gap-1 mr-1">
                  <Filter className="h-3 w-3" />
                  Filtro:
                </span>
                <button
                  type="button"
                  onClick={() => setFilterType('ALL')}
                  className={`px-3 py-1.5 rounded-lg font-medium transition-colors ${
                    filterType === 'ALL'
                      ? 'bg-emerald-600 text-white shadow-sm font-semibold'
                      : 'bg-white border border-slate-200 text-slate-600 hover:bg-slate-100'
                  }`}
                >
                  Todos
                </button>
                <button
                  type="button"
                  onClick={() => setFilterType('REFERENCE')}
                  className={`px-3 py-1.5 rounded-lg font-medium transition-colors ${
                    filterType === 'REFERENCE'
                      ? 'bg-emerald-600 text-white shadow-sm font-semibold'
                      : 'bg-white border border-slate-200 text-slate-600 hover:bg-slate-100'
                  }`}
                >
                  Referencia
                </button>
                <button
                  type="button"
                  onClick={() => setFilterType('CUSTOMER')}
                  className={`px-3 py-1.5 rounded-lg font-medium transition-colors ${
                    filterType === 'CUSTOMER'
                      ? 'bg-emerald-600 text-white shadow-sm font-semibold'
                      : 'bg-white border border-slate-200 text-slate-600 hover:bg-slate-100'
                  }`}
                >
                  Cliente
                </button>
              </div>
            </div>

            {/* Mensaje descriptivo de coincidencia */}
            {searchTerm && (
              <div className="flex items-center justify-between text-[11px] text-slate-500 pt-0.5">
                <span>
                  Resultados encontrados:{' '}
                  <strong className="text-slate-800 font-mono font-semibold">
                    {filteredSales.length}
                  </strong>{' '}
                  orden(es)
                </span>
                <span className="text-emerald-700 font-medium">
                  {filterType === 'CUSTOMER'
                    ? 'Nombre (contiene) | Cédula/RUC (empieza con)'
                    : filterType === 'REFERENCE'
                    ? 'Referencia Odoo (contiene secuencia)'
                    : 'Referencia/Nombre (contiene) | Cédula (empieza con)'}
                </span>
              </div>
            )}
          </div>

          <CardContent className="p-0">
            {loading ? (
              <div className="p-12 text-center text-slate-400">
                <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-emerald-600 mx-auto mb-2" />
                <span>Cargando historial de ventas...</span>
              </div>
            ) : error ? (
              <div className="p-8 text-center text-rose-600 text-sm flex flex-col items-center gap-1.5">
                <AlertCircle className="h-5 w-5" />
                <p className="font-semibold">Error al cargar el historial:</p>
                <p className="text-xs text-slate-600">{error}</p>
              </div>
            ) : sales.length === 0 ? (
              <div className="p-12 text-center text-slate-400">
                <Inbox className="h-10 w-10 mx-auto mb-2 text-slate-300 stroke-1" />
                <p className="font-medium text-slate-600">No se han registrado ventas en esta sesión.</p>
                <p className="text-xs mt-1 text-slate-400">Regresa al terminal para procesar una nueva orden.</p>
              </div>
            ) : filteredSales.length === 0 ? (
              <div className="p-12 text-center text-slate-400">
                <Inbox className="h-10 w-10 mx-auto mb-2 text-slate-300 stroke-1" />
                <p className="font-medium text-slate-600">No se encontraron órdenes coincidentes.</p>
                <p className="text-xs mt-1 text-slate-400">
                  {filterType === 'REFERENCE'
                    ? `Ninguna orden coincide con la referencia "${searchTerm}".`
                    : filterType === 'CUSTOMER'
                    ? `Ningún cliente coincide con "${searchTerm}".`
                    : `Ningún registro coincide con "${searchTerm}".`}
                </p>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setSearchTerm('')}
                  className="mt-3 text-xs"
                >
                  Limpiar búsqueda
                </Button>
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
                    <TableHead className="text-right">Total Liquidado</TableHead>
                    <TableHead className="text-center w-28">Acciones</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {pagedSales.map((sale) => {
                    const statusBadge = getSaleStateBadge(sale.state);

                    return (
                      <TableRow key={sale.orderId} className="hover:bg-slate-50/80">
                        {/* ID Local (primeros 8 caracteres) */}
                        <TableCell className="font-mono text-xs text-slate-500">
                          {sale.orderId.substring(0, 8)}...
                        </TableCell>

                        {/* Referencia Oficial en Odoo 20 */}
                        <TableCell className="font-semibold text-slate-800">
                          <div className="flex items-center gap-1.5 font-mono">
                            <CheckCircle2 className="h-4 w-4 text-emerald-600 shrink-0" />
                            <span>{sale.odooOrderReference || (sale.odooOrderId ? `Odoo #${sale.odooOrderId}` : 'Pendiente')}</span>
                          </div>
                        </TableCell>

                        {/* Datos del Cliente */}
                        <TableCell>
                          <span className="font-medium text-xs text-slate-700 block">{sale.customerName}</span>
                          <span className="text-[11px] text-slate-400 font-mono">{sale.customerDocument}</span>
                        </TableCell>

                        {/* Fecha y Hora de Sincronización (UTC-5) */}
                        <TableCell className="text-xs text-slate-500 font-mono">
                          {formatEcDateTime(sale.createdAtUtc)}
                        </TableCell>

                        {/* Badge de Estado en Español */}
                        <TableCell>
                          <Badge variant="success" className={statusBadge.className}>
                            {statusBadge.label}
                          </Badge>
                        </TableCell>

                        {/* Total Liquidado */}
                        <TableCell className="text-right font-bold text-slate-900 font-mono">
                          ${sale.totalAmount.toFixed(2)}
                        </TableCell>

                        {/* Columna de Acciones: Ver Detalle (RF-14) */}
                        <TableCell className="text-center">
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => openSaleDetail(sale.orderId)}
                            className="h-7 px-2.5 text-xs gap-1 border-slate-300 hover:border-emerald-600 hover:text-emerald-700 hover:bg-emerald-50"
                            title="Ver desglose completo de la orden"
                          >
                            <Eye className="h-3.5 w-3.5" />
                            <span>Ver Detalle</span>
                          </Button>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            )}

            {/* Controles de Paginación del Historial */}
            {!loading && !error && filteredSales.length > 0 && (
              <Pagination
                currentPage={currentPage}
                totalPages={totalPages}
                totalCount={filteredSales.length}
                pageSize={PAGE_SIZE}
                onPageChange={setCurrentPage}
                className="border-t border-slate-200 bg-slate-50/50"
              />
            )}
          </CardContent>
        </Card>
      </main>

      {/* Modal de Detalle de Venta (RF-14) */}
      <SaleDetailModal
        open={isDetailOpen}
        onClose={closeSaleDetail}
        sale={selectedSale}
        loading={detailLoading}
      />
    </div>
  );
};
