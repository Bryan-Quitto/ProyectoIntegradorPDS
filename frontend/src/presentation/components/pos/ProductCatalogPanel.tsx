import React, { useState, useMemo, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Product } from '@/domain/Product';
import { Card, CardHeader, CardTitle, CardContent } from '../ui/Card';
import { Input } from '../ui/Input';
import { Button } from '../ui/Button';
import { Badge } from '../ui/Badge';
import { Pagination } from '../ui/Pagination';
import { Search, Plus, Package, Filter, X } from 'lucide-react';

export type ProductFilterType = 'ALL' | 'NAME' | 'CODE';

interface ProductCatalogPanelProps {
  products: Product[];
  onAddToCart: (product: Product) => void;
  loading: boolean;
  onSearch: (query: string) => void;
}

export const ProductCatalogPanel: React.FC<ProductCatalogPanelProps> = ({
  products,
  onAddToCart,
  loading,
  onSearch,
}) => {
  const [searchParams, setSearchParams] = useSearchParams();

  // Lectura inicial de parámetros URL (filtros de catálogo compartibles y persistentes)
  const querySearch = searchParams.get('buscar') || searchParams.get('q') || '';
  const rawFilter = (searchParams.get('filtro') || searchParams.get('filter') || 'ALL').toUpperCase();
  const queryFilter: ProductFilterType =
    rawFilter === 'NAME' || rawFilter === 'CODE' ? rawFilter : 'ALL';
  const queryPage = Math.max(1, parseInt(searchParams.get('pagina') || searchParams.get('page') || '1', 10) || 1);

  const [searchTerm, setSearchTerm] = useState(querySearch);
  const [filterType, setFilterType] = useState<ProductFilterType>(queryFilter);
  const [currentPage, setCurrentPage] = useState(queryPage);
  const PAGE_SIZE = 6; // 6 productos por página para cuadrícula responsiva (2x3)

  // Sincronizar estado local hacia los SearchParams de la URL
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

  // Reset de página al cambiar término de búsqueda o categoría de filtro
  useEffect(() => {
    setCurrentPage(1);
  }, [searchTerm, filterType]);

  const handleSearchChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value;
    setSearchTerm(val);
    onSearch(val);
  };

  const handleClearSearch = () => {
    setSearchTerm('');
    onSearch('');
  };

  const getPlaceholder = () => {
    switch (filterType) {
      case 'NAME':
        return 'Buscar producto por nombre (ej. Producto A, Café)...';
      case 'CODE':
        return 'Buscar por código de referencia o código de barras (ej. PROD-001)...';
      case 'ALL':
      default:
        return 'Buscar por nombre, código de referencia o código de barras...';
    }
  };

  // Filtrado reactivo en cliente según el tipo de filtro activo
  const filteredProducts = useMemo(() => {
    const term = searchTerm.trim().toLowerCase();
    if (!term) return products;

    return products.filter((p) => {
      const matchName = p.name.toLowerCase().includes(term);
      const matchCode =
        (p.defaultCode && p.defaultCode.toLowerCase().includes(term)) ||
        (p.barcode && p.barcode.toLowerCase().includes(term));

      if (filterType === 'NAME') return matchName;
      if (filterType === 'CODE') return matchCode;
      return matchName || matchCode;
    });
  }, [products, searchTerm, filterType]);

  const totalPages = Math.max(1, Math.ceil(filteredProducts.length / PAGE_SIZE));

  const pagedProducts = useMemo(() => {
    const start = (currentPage - 1) * PAGE_SIZE;
    return filteredProducts.slice(start, start + PAGE_SIZE);
  }, [filteredProducts, currentPage, PAGE_SIZE]);

  return (
    <Card className="flex flex-col h-full shadow-sm border-slate-200">
      <CardHeader className="pb-3 border-b">
        <div className="flex items-center justify-between">
          <CardTitle className="text-lg font-bold flex items-center gap-2">
            <Package className="h-5 w-5 text-emerald-600" />
            Catálogo de Productos Odoo
          </CardTitle>
          <Badge variant="success" className="bg-emerald-50 text-emerald-700 border-emerald-200">
            {products.length} disponibles
          </Badge>
        </div>

        {/* Input de Búsqueda con botón de limpiar */}
        <div className="relative mt-3">
          <Search className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
          <Input
            placeholder={getPlaceholder()}
            value={searchTerm}
            onChange={handleSearchChange}
            className="pl-9 pr-8 bg-slate-50 focus:bg-white"
          />
          {searchTerm && (
            <button
              type="button"
              onClick={handleClearSearch}
              className="absolute right-2.5 top-2.5 text-slate-400 hover:text-slate-600"
              title="Limpiar búsqueda"
            >
              <X className="h-4 w-4" />
            </button>
          )}
        </div>

        {/* Filtros de Búsqueda: Todos | Nombre | Código */}
        <div className="flex items-center gap-1.5 mt-2.5 text-xs">
          <span className="text-[11px] font-medium text-slate-400 flex items-center gap-1 mr-1">
            <Filter className="h-3 w-3" />
            Filtro:
          </span>
          <button
            type="button"
            onClick={() => setFilterType('ALL')}
            className={`px-2.5 py-1 rounded-md font-medium transition-colors ${
              filterType === 'ALL'
                ? 'bg-emerald-600 text-white shadow-sm'
                : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
            }`}
          >
            Todos
          </button>
          <button
            type="button"
            onClick={() => setFilterType('NAME')}
            className={`px-2.5 py-1 rounded-md font-medium transition-colors ${
              filterType === 'NAME'
                ? 'bg-emerald-600 text-white shadow-sm'
                : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
            }`}
          >
            Nombre
          </button>
          <button
            type="button"
            onClick={() => setFilterType('CODE')}
            className={`px-2.5 py-1 rounded-md font-medium transition-colors ${
              filterType === 'CODE'
                ? 'bg-emerald-600 text-white shadow-sm'
                : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
            }`}
          >
            Código
          </button>

          {searchTerm && (
            <span className="ml-auto text-[11px] text-slate-400 font-mono">
              {filteredProducts.length} de {products.length}
            </span>
          )}
        </div>
      </CardHeader>

      <CardContent className="flex-1 overflow-y-auto p-4">
        {loading ? (
          <div className="flex items-center justify-center h-48 text-slate-400">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-emerald-600"></div>
            <span className="ml-2">Cargando catálogo desde Odoo 20...</span>
          </div>
        ) : products.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-48 text-slate-400 text-center">
            <Package className="h-10 w-10 mb-2 stroke-1" />
            <p className="font-medium">No se encontraron productos en Odoo.</p>
            <p className="text-xs">Verifica que existan productos con 'available_in_pos' en el ERP.</p>
          </div>
        ) : filteredProducts.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-48 text-slate-400 text-center">
            <Package className="h-10 w-10 mb-2 stroke-1" />
            <p className="font-medium">No se encontraron productos coincidentes.</p>
            <p className="text-xs text-slate-400 mt-0.5">
              {filterType === 'NAME'
                ? `Ningún producto contiene el nombre "${searchTerm}".`
                : filterType === 'CODE'
                ? `Ningún código de referencia o código de barras coincide con "${searchTerm}".`
                : `Intenta buscar con otro término o cambia el filtro activo.`}
            </p>
            {searchTerm && (
              <Button
                variant="outline"
                size="sm"
                onClick={handleClearSearch}
                className="mt-3 text-xs"
              >
                Limpiar búsqueda
              </Button>
            )}
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            {pagedProducts.map((product) => (
              <div
                key={product.id}
                onClick={() => onAddToCart(product)}
                className="group relative p-3 border rounded-xl hover:border-emerald-500 hover:shadow-md transition-all cursor-pointer bg-white flex flex-col justify-between"
              >
                <div>
                  {/* Badge de Código de Referencia Odoo */}
                  <div className="flex items-center gap-1.5 mb-1.5">
                    {product.defaultCode ? (
                      <span className="text-[11px] font-mono font-bold text-emerald-800 bg-emerald-50 border border-emerald-200/80 px-1.5 py-0.5 rounded shadow-xs">
                        {product.defaultCode}
                      </span>
                    ) : (
                      <span className="text-[11px] font-mono text-slate-400 bg-slate-50 border border-slate-200 px-1.5 py-0.5 rounded">
                        REF: #{product.id}
                      </span>
                    )}
                  </div>

                  <h4 className="font-semibold text-slate-800 text-sm line-clamp-2 group-hover:text-emerald-700 transition-colors">
                    {product.name}
                  </h4>

                  {product.barcode && (
                    <p className="text-[11px] font-mono text-slate-400 mt-1">EAN: {product.barcode}</p>
                  )}
                </div>

                <div className="mt-3 flex items-center justify-between pt-2 border-t border-slate-100">
                  <span className="text-base font-bold text-slate-900">${product.price.toFixed(2)}</span>
                  <Button
                    size="sm"
                    variant="ghost"
                    className="h-8 w-8 p-0 rounded-full group-hover:bg-emerald-500 group-hover:text-white"
                    title={`Agregar ${product.name} al carrito`}
                  >
                    <Plus className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            ))}
          </div>
        )}
      </CardContent>

      {/* Controles de Paginación de Productos */}
      {filteredProducts.length > 0 && (
        <Pagination
          currentPage={currentPage}
          totalPages={totalPages}
          totalCount={filteredProducts.length}
          pageSize={PAGE_SIZE}
          onPageChange={setCurrentPage}
          className="border-t border-slate-200 bg-slate-50/70 rounded-b-xl"
        />
      )}
    </Card>
  );
};
