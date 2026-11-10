import React, { useState, useMemo, useEffect } from 'react';
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

  const { customers, loading, error, refresh } = useCustomers(debouncedSearchTerm);

  useEffect(() => {
    setPageNumber(1);
  }, [debouncedSearchTerm, searchCriteria]);

  useEffect(() => {
    if (open) {
      refresh(debouncedSearchTerm);
    }
  }, [open, debouncedSearchTerm, refresh]);

  const filteredCustomers = useMemo(() => {
    // Excluir Consumidor Final de la lista tabular (ya cuenta con botón dedicado y no es un cliente particular)
    const baseList = customers.filter((c) => {
      const isFinalConsumer =
        c.vat === '9999999999999' ||
        (c.name || '').toLowerCase().includes('consumidor final');
      return !isFinalConsumer;
    });

    const term = searchTerm.trim().toLowerCase();
    if (!term) return baseList;

    const cleanIdTerm = term.replace(/^id:?|^#/i, '').trim();

    return baseList.filter((c) => {
      const matchId = cleanIdTerm !== '' && (String(c.id) === cleanIdTerm || String(c.id).includes(cleanIdTerm));
      const matchName = (c.name || '').toLowerCase().includes(term);
      const matchVat = (c.vat || '').toLowerCase().includes(term);

      if (searchCriteria === 'id') return matchId;
      if (searchCriteria === 'name') return matchName;
      if (searchCriteria === 'vat') return matchVat;
      return matchId || matchName || matchVat;
    });
  }, [customers, searchTerm, searchCriteria]);

  const totalCount = filteredCustomers.length;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  const pagedCustomers = useMemo(() => {
    const start = (pageNumber - 1) * pageSize;
    return filteredCustomers.slice(start, start + pageSize);
  }, [filteredCustomers, pageNumber, pageSize]);

  const handleSelect = (customer: Customer) => {
    onSelectCustomer(customer);
    onOpenChange(false);
  };

  const handleSelectFinalConsumer = () => {
    onSelectCustomer(null);
    onOpenChange(false);
  };

  const getPlaceholder = () => {
    switch (searchCriteria) {
      case 'id':
        return 'Buscar específicamente por ID Odoo (ej: 1, 2)...';
      case 'name':
        return 'Buscar por nombres o razón social...';
      case 'vat':
        return 'Buscar por Cédula o RUC (10 o 13 dígitos)...';
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
            {/* Combobox Select Estilizado (Punto 6) */}
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
            {totalCount} cliente(s) en Odoo
          </span>
        </div>

        {/* Tabla de Clientes Odoo */}
        <div className="max-h-96 overflow-y-auto border border-slate-200 rounded-lg">
          {loading ? (
            <div className="p-8 text-center text-slate-500">
              <Users className="h-8 w-8 mx-auto text-emerald-600 animate-spin mb-2" />
              <span>Consultando clientes en Odoo ERP (res.partner)...</span>
            </div>
          ) : error ? (
            <div className="p-6 text-center text-rose-600 flex items-center justify-center space-x-2">
              <AlertCircle className="h-5 w-5" />
              <span>{error}</span>
            </div>
          ) : filteredCustomers.length === 0 ? (
            <div className="p-8 text-center text-slate-400">
              No se encontraron clientes coincidentes con el criterio de búsqueda.
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-20">ID Odoo</TableHead>
                  <TableHead>Nombre / Razón Social</TableHead>
                  <TableHead>Identificación</TableHead>
                  <TableHead>Dirección / Contacto</TableHead>
                  <TableHead className="text-right w-36">Acción</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {pagedCustomers.map((customer) => {
                  const isCurrentlySelected = selectedCustomerId === customer.id;

                  return (
                    <TableRow
                      key={customer.id}
                      className={isCurrentlySelected ? 'bg-emerald-50/60' : ''}
                    >
                      <TableCell className="font-mono font-bold text-xs text-emerald-700">
                        #{customer.id}
                      </TableCell>
                      <TableCell>
                        <div className="font-semibold text-slate-900">{customer.name}</div>
                        {customer.email && (
                          <div className="text-[11px] text-slate-500 flex items-center gap-1 mt-0.5">
                            <Mail className="w-3 h-3 text-slate-400" />
                            <span>{customer.email}</span>
                          </div>
                        )}
                      </TableCell>
                      <TableCell className="font-mono text-xs text-slate-700">
                        {customer.vat ? (
                          <Badge variant="default" className="font-mono bg-slate-100 text-slate-800">
                            {customer.vat}
                          </Badge>
                        ) : (
                          <span className="text-slate-400 text-xs italic">Sin RUC</span>
                        )}
                      </TableCell>
                      <TableCell className="text-xs text-slate-600">
                        {customer.street ? (
                          <div className="flex items-center gap-1 text-[11px] truncate max-w-xs" title={customer.street}>
                            <MapPin className="w-3 h-3 text-slate-400 shrink-0" />
                            <span className="truncate">{customer.street}</span>
                          </div>
                        ) : null}
                        {customer.phone ? (
                          <div className="flex items-center gap-1 text-[11px] text-slate-500 mt-0.5">
                            <Phone className="w-3 h-3 text-slate-400 shrink-0" />
                            <span>{customer.phone}</span>
                          </div>
                        ) : null}
                        {!customer.street && !customer.phone ? (
                          <span className="text-slate-400 text-xs italic">—</span>
                        ) : null}
                      </TableCell>
                      <TableCell className="text-right">
                        <Button
                          type="button"
                          size="sm"
                          variant={isCurrentlySelected ? 'secondary' : 'default'}
                          onClick={() => handleSelect(customer)}
                          className={
                            isCurrentlySelected
                              ? 'h-8 px-3 text-xs bg-emerald-100 text-emerald-800 border-emerald-300'
                              : 'h-8 px-3 text-xs bg-emerald-600 hover:bg-emerald-700 text-white'
                          }
                        >
                          <Check className="h-3.5 w-3.5 mr-1" />
                          <span>{isCurrentlySelected ? 'Seleccionado' : 'Seleccionar'}</span>
                        </Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}

          {/* Paginación */}
          {filteredCustomers.length > 0 && (
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
            Mostrando {pagedCustomers.length} de {totalCount} clientes en Odoo
          </span>
          <Button type="button" variant="outline" size="sm" onClick={() => onOpenChange(false)}>
            Cerrar
          </Button>
        </div>
      </div>
    </Dialog>
  );
};
