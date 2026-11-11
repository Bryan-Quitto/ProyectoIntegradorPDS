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
import { useAuthStore } from '../stores/useAuthStore';
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
  User,
  Store,
  LogOut,
} from 'lucide-react';

export type SaleHistoryFilterType = 'ALL' | 'REFERENCE' | 'CUSTOMER';

interface SalesHistoryPageProps {
  onBackToTerminal?: () => void;
}

export const SalesHistoryPage: React.FC<SalesHistoryPageProps> = ({ onBackToTerminal }) => {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const user = useAuthStore((state) => state.user);
  const logout = useAuthStore((state) => state.logout);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

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
    error: detailError,
    isOpen: isDetailOpen,
    openSaleDetail,
    closeSaleDetail,
  } = useSaleDetail();

  // Aislamiento de Historial y Least Privilege
  const isAdmin =
    user?.role === 'Administrator' ||
    user?.role?.toLowerCase() === 'administrator' ||
    Boolean(user?.role?.includes('Administrator'));
  const hasAssignedSession = Boolean(user?.posSessionId && user.posSessionId > 0);
  const isRestricted = !isAdmin && !hasAssignedSession;

  // Lectura inicial de parámetros URL (filtros persistentes y compartibles)
  const querySearch = searchParams.get('buscar') || searchParams.get('q') || '';
  const rawFilter = (searchParams.get('filtro') || searchParams.get('filter') || 'ALL').toUpperCase();
  const queryFilter: SaleHistoryFilterType =
    rawFilter === 'REFERENCE' || rawFilter === 'CUSTOMER' ? rawFilter : 'ALL';
  const queryPage = Math.max(1, parseInt(searchParams.get('pagina') || searchParams.get('page') || '1', 10) || 1);
  const queryDetail = searchParams.get('detalle') || searchParams.get('orden');

  const [searchTerm, setSearchTerm] = useState(querySearch);
  const [filterType, setFilterType] = useState<SaleHistoryFilterType>(queryFilter);
  const [currentPage, setCurrentPage] = useState(queryPage);
  const PAGE_SIZE = 5; // 5 órdenes por página en el historial

  // Apertura automática de detalle por parámetro de URL (Deep Linking para auditoría)
  useEffect(() => {
    if (queryDetail) {
      openSaleDetail(queryDetail);
    }
  }, [queryDetail, openSaleDetail]);

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
    if (queryDetail) {
      params.detalle = queryDetail;
    }
    setSearchParams(params, { replace: true });
  }, [searchTerm, filterType, currentPage, queryDetail, setSearchParams]);

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

        <div className="flex items-center gap-2">
          {/* Badge Vendedor y Caja Asignada (Fase 4) */}
          <div className="hidden md:flex items-center gap-1.5 px-3 py-1 bg-slate-50 border border-slate-200 rounded-lg text-xs">
            <span className="text-slate-700 font-medium flex items-center gap-1">
              <User className="h-3.5 w-3.5 text-slate-500" />
              {user?.name || 'Vendedor'}
            </span>
            <span className="text-slate-300">|</span>
            <span className={`font-semibold flex items-center gap-1 ${isAdmin ? 'text-purple-700' : 'text-emerald-700'}`}>
              <Store className={`h-3.5 w-3.5 ${isAdmin ? 'text-purple-600' : 'text-emerald-600'}`} />
              {isAdmin ? 'Todas las Cajas (Admin)' : (user?.posConfigName || 'Sin Caja Asignada')}
            </span>
          </div>

          <Button variant="outline" size="sm" onClick={() => refresh()} className="h-8 gap-1.5">
            <RefreshCw className="h-3.5 w-3.5" />
            Actualizar
          </Button>

          <Button
            variant="outline"
            size="sm"
            onClick={handleLogout}
            title="Cerrar Sesión del Vendedor"
            className="h-8 gap-1.5 text-xs border-rose-200 text-rose-700 hover:bg-rose-50 hover:text-rose-800"
          >
            <LogOut className="h-3.5 w-3.5" />
            <span className="hidden sm:inline">Cerrar Sesión</span>
          </Button>
        </div>
      </header>

      {/* Contenido principal */}
      <main className="flex-1 p-6 max-w-6xl w-full mx-auto">
        {/* Banner informativo de Acceso Restringido cuando no tiene caja asignada */}
        {isRestricted && (
          <div className="mb-4 p-4 rounded-xl border border-amber-300 bg-amber-50 text-amber-900 flex items-start gap-3 shadow-xs">
            <AlertCircle className="h-5 w-5 text-amber-600 shrink-0 mt-0.5" />
            <div className="text-xs leading-relaxed">
              <strong className="font-semibold block text-amber-800 mb-0.5">
                ⚠️ Acceso Restringido: Su usuario no tiene una caja asignada.
              </strong>
              Para proteger la confidencialidad de la información (Data Protection), el historial de ventas solo está disponible para vendedores con caja activa o administradores.
            </div>
          </div>
        )}

        <Card className="shadow-sm border-slate-200">
          <CardHeader className="pb-3 border-b flex flex-row items-center justify-between">
            <div>
              <CardTitle className="text-base font-bold flex items-center gap-2">
                <FileText className="h-5 w-5 text-emerald-600" />
                Órdenes Registradas (Persistencia Local de Auditoría)
              </CardTitle>
              <p className="text-xs text-slate-500 mt-1">
                {isAdmin
                  ? 'Vista de Administrador: Mostrando órdenes consolidadas de todas las cajas.'
                  : hasAssignedSession
                  ? `Mostrando órdenes exclusivas de su caja asignada: ${user?.posConfigName || `Caja #${user?.posSessionId}`} (Filtro de seguridad por caja activo).`
                  : 'No tiene una caja asignada. Su usuario no tiene acceso al historial de órdenes de venta.'}
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
              {/* Barra de Búsqueda con Combobox de Filtros (Punto 6) */}
              <div className="flex items-center gap-2 flex-1 max-w-2xl">
                {/* Combobox Select Estilizado (Punto 6) */}
                <select
                  value={filterType}
                  onChange={(e) => setFilterType(e.target.value as SaleHistoryFilterType)}
                  className="text-xs h-9 rounded-lg border border-slate-300 bg-white px-2.5 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-emerald-500 cursor-pointer shadow-xs shrink-0"
                  aria-label="Criterio de filtro para el historial de órdenes"
                >
                  <option value="ALL">🔍 Todos los criterios</option>
                  <option value="REFERENCE">🧾 Referencia / ID Odoo</option>
                  <option value="CUSTOMER">👤 Cliente / Identificación</option>
                </select>

                <div className="relative flex-1">
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
              </div>

              {/* Contador rápido / Estado a la derecha */}
              <div className="text-xs text-slate-500 hidden sm:block shrink-0">
                {searchTerm ? (
                  <span>
                    Coincidencias: <strong className="text-slate-800 font-mono font-semibold">{filteredSales.length}</strong> de{' '}
                    <strong className="text-slate-800 font-mono font-semibold">{sales.length}</strong>
                  </span>
                ) : (
                  <span>
                    Total: <strong className="text-slate-800 font-mono font-semibold">{sales.length}</strong> órdenes
                  </span>
                )}
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
              <div className="p-8 text-center text-rose-600 text-sm flex flex-col items-center gap-2 max-w-lg mx-auto">
                <AlertCircle className="h-6 w-6 text-rose-500" />
                <p className="font-semibold text-rose-700">Error al cargar el historial:</p>
                <p className="text-xs text-slate-600 bg-rose-50 border border-rose-100 rounded-lg p-3 leading-relaxed">{error}</p>
                <Button variant="outline" size="sm" onClick={() => refresh()} className="mt-1 text-xs">
                  <RefreshCw className="h-3.5 w-3.5 mr-1.5" /> Reintentar
                </Button>
              </div>
            ) : sales.length === 0 ? (
              <div className="p-12 text-center text-slate-400">
                <Inbox className="h-10 w-10 mx-auto mb-2 text-slate-300 stroke-1" />
                {isRestricted ? (
                  <>
                    <p className="font-semibold text-amber-800 text-sm">
                      ⚠️ Acceso Restringido: Su usuario no tiene una caja asignada.
                    </p>
                    <p className="text-xs mt-1 text-slate-500 max-w-md mx-auto">
                      Su usuario no tiene acceso al historial de órdenes de venta. Para proteger la confidencialidad de la información (Data Protection), el historial solo está disponible para vendedores con caja activa o administradores.
                    </p>
                  </>
                ) : (
                  <>
                    <p className="font-medium text-slate-600">No se han registrado ventas en esta sesión.</p>
                    <p className="text-xs mt-1 text-slate-400">Regresa al terminal para procesar una nueva orden.</p>
                  </>
                )}
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
                        {/* ID Local (primeros 8 caracteres con tooltip completo) */}
                        <TableCell className="font-mono text-xs text-slate-500">
                          <span
                            title={sale.orderId}
                            aria-label={`Identificador local completo: ${sale.orderId}`}
                            className="cursor-help hover:text-slate-800 transition-colors underline decoration-dotted decoration-slate-300"
                          >
                            {sale.orderId.substring(0, 8)}...
                          </span>
                        </TableCell>

                        {/* Referencia Oficial en Odoo 20 */}
                        <TableCell className="font-semibold text-slate-800">
                          <div className="flex items-center gap-1.5 font-mono">
                            <CheckCircle2 className="h-4 w-4 text-emerald-600 shrink-0" />
                            <span>{sale.odooOrderReference || (sale.odooOrderId ? `Odoo #${sale.odooOrderId}` : 'Pendiente')}</span>
                            {sale.odooOrderId && (
                              <span
                                className="text-[10px] font-mono text-slate-500 bg-slate-100 border border-slate-200 px-1 py-0.5 rounded font-normal"
                                title={`ID interno en base de datos de Odoo: ${sale.odooOrderId}`}
                              >
                                #{sale.odooOrderId}
                              </span>
                            )}
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
        error={detailError}
      />
    </div>
  );
};
