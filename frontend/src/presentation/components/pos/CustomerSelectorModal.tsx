import React, { useState, useEffect, useRef } from 'react';
import { Search, Check, AlertCircle, Users, User, X, Building2, Phone, Mail, MapPin } from 'lucide-react';
import { Customer } from '@/application/ports/IPosApiGateway';
import { useCustomers } from '@/presentation/hooks/useCustomers';
import { Dialog } from '../ui/Dialog';
import { Input } from '../ui/Input';
import { Button } from '../ui/Button';
import { Badge } from '../ui/Badge';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '../ui/Table';
import { Pagination } from '../ui/Pagination';
import { useDebounce } from '@/shared/utils/useDebounce';

export interface CustomerSelectorModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSelectCustomer: (customer: Customer | null) => void;
  selectedCustomerId?: number | null;
}

type SearchCriteriaType = 'all' | 'name' | 'vat' | 'id';

export const CustomerSelectorModal: React.FC<CustomerSelectorModalProps> = ({
  open,
  onOpenChange,
  onSelectCustomer,
  selectedCustomerId,
}) => {
  const [searchTerm, setSearchTerm] = useState('');
  const [searchCriteria, setSearchCriteria] = useState<SearchCriteriaType>('all');
  const [pageNumber, setPageNumber] = useState<number>(1);
  const pageSize = 8;

  const debouncedSearchTerm = useDebounce(searchTerm, 350);

  // Consulta con paginación server-side sobre Odoo 20 (res.partner)
  const { customers, totalCount, loading, error, refresh } = useCustomers(
    debouncedSearchTerm,
    pageNumber,
    pageSize,
    searchCriteria
  );

  // Reset de página al cambiar término de búsqueda o criterio
  const prevFilterRef = useRef({ search: debouncedSearchTerm, criteria: searchCriteria });
  useEffect(() => {
    if (
      prevFilterRef.current.search !== debouncedSearchTerm ||
      prevFilterRef.current.criteria !== searchCriteria
    ) {
      prevFilterRef.current = { search: debouncedSearchTerm, criteria: searchCriteria };
      setPageNumber(1);
    }
  }, [debouncedSearchTerm, searchCriteria]);

  useEffect(() => {
    if (open) {
      refresh(debouncedSearchTerm, pageNumber, pageSize, searchCriteria);
    }
  }, [open, debouncedSearchTerm, pageNumber, pageSize, searchCriteria, refresh]);

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const pagedCustomers = customers;

  const handleSelectCustomer = (customer: Customer) => {
    onSelectCustomer(customer);
    onOpenChange(false);
  };

  const handleSelectFinalConsumer = () => {
    onSelectCustomer(null);
    onOpenChange(false);
  };

  const getPlaceholder = () => {
    switch (searchCriteria) {
      case 'name':
        return 'Buscar por nombre o razón social...';
      case 'vat':
        return 'Buscar por cédula o RUC (ej. 1712...)...';
      case 'id':
        return 'Buscar por ID de Odoo (ej. 15, #42)...';
      case 'all':
      default:
        return 'Buscar cliente por nombre, identificación o ID de Odoo...';
    }
  };

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title={
        <div className="flex items-center gap-2 text-slate-900">
          <Users className="h-5 w-5 text-emerald-600" />
          <span>Seleccionar Cliente de Odoo (res.partner)</span>
        </div>
      }
      description="Seleccione un contacto registrado en Odoo ERP o establezca Consumidor Final para la venta actual."
      className="max-w-4xl"
    >
      <div className="space-y-4 pt-1">
        {/* Banner de Consumidor Final Rápido */}
        <div className="p-3 rounded-lg bg-emerald-50 border border-emerald-200 text-emerald-950 text-xs sm:text-sm flex flex-col sm:flex-row sm:items-center justify-between gap-2 shadow-xs">
          <div className="flex items-center space-x-2">
            <User className="h-5 w-5 text-emerald-600 shrink-0" />
            <div>
              <span className="font-bold">Consumidor Final Predeterminado:</span>
              <span className="text-slate-600 ml-1 text-xs">Cédula/RUC 9999999999999 (Sin partner enlazado).</span>
            </div>
          </div>
          <Button
            type="button"
            size="sm"
            variant="outline"
            onClick={handleSelectFinalConsumer}
            className="whitespace-nowrap text-xs h-8 border-emerald-300 hover:bg-emerald-100/70 text-emerald-800"
          >
            Usar Consumidor Final
          </Button>
        </div>

        {/* Controles de Búsqueda y Combobox de Filtros */}
        <div className="flex flex-col sm:flex-row gap-3 items-stretch sm:items-center justify-between">
          <div className="flex items-center gap-2 flex-1 sm:max-w-lg">
            <select
              value={searchCriteria}
              onChange={(e) => setSearchCriteria(e.target.value as SearchCriteriaType)}
              className="text-xs h-9 rounded-lg border border-slate-300 bg-white px-2.5 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-emerald-500 cursor-pointer shadow-xs"
            >
              <option value="all">🔍 Todos los campos</option>
              <option value="name">👤 Nombre / Razón</option>
              <option value="vat">💳 Cédula / RUC</option>
              <option value="id">🏷️ ID Odoo</option>
            </select>

            <div className="relative flex-1">
              <Search className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
              <Input
                placeholder={getPlaceholder()}
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="pl-9 pr-8 text-xs sm:text-sm h-9 bg-slate-50 focus:bg-white"
                autoFocus
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

          <span className="text-xs text-slate-500 font-mono self-end sm:self-auto">
            {totalCount.toLocaleString()} cliente(s) en Odoo
          </span>
        </div>

        {/* Mensaje de Error */}
        {error && (
          <div className="p-3 rounded-lg bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-center space-x-2">
            <AlertCircle className="h-4 w-4 shrink-0" />
            <span>{error}</span>
          </div>
        )}

        {/* Tabla de Clientes con Scroll */}
        <div className="border border-slate-200 rounded-lg overflow-hidden bg-white shadow-xs">
          {loading ? (
            <div className="p-8 text-center text-slate-400">
              <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-emerald-600 mx-auto mb-2" />
              <span className="text-xs">Consultando clientes en Odoo ERP...</span>
            </div>
          ) : customers.length === 0 ? (
            <div className="p-8 text-center text-slate-400 space-y-1">
              <Users className="h-8 w-8 mx-auto text-slate-300 stroke-1" />
              <p className="text-sm font-medium text-slate-600">No se encontraron clientes.</p>
              <p className="text-xs text-slate-400">
                {searchTerm
                  ? `Ningún contacto coincide con "${searchTerm}".`
                  : 'No hay contactos registrados en Odoo ERP.'}
              </p>
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow className="bg-slate-50 text-slate-600 text-xs">
                  <TableHead className="w-16">ID Odoo</TableHead>
                  <TableHead>Nombre / Razón Social</TableHead>
                  <TableHead>Identificación</TableHead>
                  <TableHead className="hidden md:table-cell">Contacto</TableHead>
                  <TableHead className="text-right w-24">Acción</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {pagedCustomers.map((customer) => {
                  const isSelected = selectedCustomerId === customer.id;

                  return (
                    <TableRow
                      key={customer.id}
                      className={`hover:bg-slate-50 transition-colors ${
                        isSelected ? 'bg-emerald-50/60' : ''
                      }`}
                    >
                      <TableCell className="font-mono text-xs text-slate-500 font-semibold">
                        #{customer.id}
                      </TableCell>
                      <TableCell>
                        <div className="font-medium text-xs sm:text-sm text-slate-900 flex items-center gap-1.5">
                          <span>{customer.name}</span>
                          {isSelected && (
                            <Badge variant="success" className="bg-emerald-100 text-emerald-800 text-[10px] px-1.5 py-0">
                              Seleccionado
                            </Badge>
                          )}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div className="font-mono text-xs text-slate-700 font-medium">
                          {customer.vat || <span className="text-slate-400 italic font-sans text-[11px]">Sin Cédula/RUC</span>}
                        </div>
                      </TableCell>
                      <TableCell className="hidden md:table-cell">
                        <div className="space-y-0.5 text-xs text-slate-500">
                          {customer.street && (
                            <div className="flex items-center gap-1 text-[11px] truncate max-w-xs text-slate-600" title={customer.street}>
                              <MapPin className="w-3 h-3 text-slate-400 shrink-0" />
                              <span className="truncate">{customer.street}</span>
                            </div>
                          )}
                          {customer.email && (
                            <div className="flex items-center gap-1 truncate max-w-xs">
                              <Mail className="h-3 w-3 text-slate-400 shrink-0" />
                              <span className="truncate">{customer.email}</span>
                            </div>
                          )}
                          {customer.phone && (
                            <div className="flex items-center gap-1">
                              <Phone className="h-3 w-3 text-slate-400 shrink-0" />
                              <span>{customer.phone}</span>
                            </div>
                          )}
                          {!customer.street && !customer.email && !customer.phone && (
                            <span className="text-slate-300 italic text-[11px]">Sin teléfono/email</span>
                          )}
                        </div>
                      </TableCell>
                      <TableCell className="text-right">
                        <Button
                          type="button"
                          size="sm"
                          variant={isSelected ? 'outline' : 'default'}
                          onClick={() => handleSelectCustomer(customer)}
                          className={`h-7 px-2 text-xs flex items-center space-x-1 ml-auto ${
                            isSelected
                              ? 'border-emerald-500 text-emerald-700 bg-emerald-50/50'
                              : 'bg-emerald-600 hover:bg-emerald-700 text-white'
                          }`}
                        >
                          {isSelected ? (
                            <>
                              <Check className="h-3.5 w-3.5" />
                              <span>Activo</span>
                            </>
                          ) : (
                            <span>Seleccionar</span>
                          )}
                        </Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}

          {/* Paginación */}
          {!loading && !error && totalCount > 0 && (
            <Pagination
              currentPage={pageNumber}
              totalPages={totalPages}
              totalCount={totalCount}
              pageSize={pageSize}
              onPageChange={setPageNumber}
            />
          )}
        </div>

        {/* Footer */}
        <div className="flex justify-between items-center pt-2 border-t border-slate-100">
          <span className="text-xs text-slate-500">
            Total en Odoo: <strong className="font-semibold text-slate-800">{totalCount.toLocaleString()}</strong> clientes (res.partner)
          </span>
          <Button type="button" variant="outline" size="sm" onClick={() => onOpenChange(false)}>
            Cerrar
          </Button>
        </div>
      </div>
    </Dialog>
  );
};
