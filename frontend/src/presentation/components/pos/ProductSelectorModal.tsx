import React, { useState, useEffect, useRef } from 'react';
import { Search, Plus, Check, AlertCircle, Package, X, CheckCircle2, ArrowLeftRight } from 'lucide-react';
import { Product } from '@/domain/Product';
import { useProducts } from '@/presentation/hooks/useProducts';
import { Dialog } from '../ui/Dialog';
import { Input } from '../ui/Input';
import { Button } from '../ui/Button';
import { Badge } from '../ui/Badge';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '../ui/Table';
import { Pagination } from '../ui/Pagination';
import { useDebounce } from '@/shared/utils/useDebounce';

export type ProductSearchCriteria = 'ALL' | 'NAME' | 'CODE' | 'ID';

export interface ProductSelectorModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  products?: Product[];
  selectedProductIds: number[];
  onSelectProduct: (product: Product) => void;
  loading?: boolean;
  replacingProductId?: number | null;
  replacingProductName?: string;
}

export const ProductSelectorModal: React.FC<ProductSelectorModalProps> = ({
  open,
  onOpenChange,
  products: propsProducts,
  selectedProductIds,
  onSelectProduct,
  loading = false,
  replacingProductId,
  replacingProductName,
}) => {
  const [searchTerm, setSearchTerm] = useState('');
  const [searchCriteria, setSearchCriteria] = useState<ProductSearchCriteria>('ALL');
  const [onlyInStock, setOnlyInStock] = useState(false);
  const [pageNumber, setPageNumber] = useState<number>(1);
  const [stockError, setStockError] = useState<string | null>(null);
  const pageSize = 8;

  const debouncedSearchTerm = useDebounce(searchTerm, 350);

  // Consulta paginada del lado del servidor para soportar 1,000,000+ de productos en Odoo
  const {
    products: serverProducts,
    totalCount: serverTotalCount,
    loading: serverLoading,
    refresh,
  } = useProducts(debouncedSearchTerm, pageNumber, pageSize, searchCriteria, onlyInStock);

  const prevFilterRef = useRef({ search: debouncedSearchTerm, criteria: searchCriteria, inStock: onlyInStock });
  useEffect(() => {
    if (
      prevFilterRef.current.search !== debouncedSearchTerm ||
      prevFilterRef.current.criteria !== searchCriteria ||
      prevFilterRef.current.inStock !== onlyInStock
    ) {
      prevFilterRef.current = { search: debouncedSearchTerm, criteria: searchCriteria, inStock: onlyInStock };
      setPageNumber(1);
      setStockError(null);
    }
  }, [debouncedSearchTerm, searchCriteria, onlyInStock]);

  useEffect(() => {
    if (open) {
      void refresh(debouncedSearchTerm, pageNumber, pageSize, searchCriteria, onlyInStock);
    }
  }, [open, debouncedSearchTerm, pageNumber, pageSize, searchCriteria, onlyInStock, refresh]);

  const effectiveProducts = serverProducts;
  const totalCount = serverTotalCount;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const pagedProducts = effectiveProducts;
  const isEffectiveLoading = loading || serverLoading;

  const isReplaceMode = Boolean(replacingProductId);

  const handleSelect = (product: Product) => {
    // [RN-03]: Validación estricta de stock disponible desde Odoo
    if (product.availableQuantity <= 0) {
      setStockError(
        `No es posible agregar "${product.name}" a la orden porque no tiene existencias disponibles en el inventario de Odoo (Agotado: 0 uds.).`
      );
      return;
    }
    setStockError(null);

    if (!isReplaceMode && selectedProductIds.includes(product.id)) {
      return;
    }

    if (isReplaceMode && product.id === replacingProductId) {
      return;
    }

    onSelectProduct(product);
  };

  const getPlaceholder = () => {
    switch (searchCriteria) {
      case 'ID':
        return 'Buscar específicamente por ID Odoo (ej: 1, 2)...';
      case 'NAME':
        return 'Buscar específicamente por nombre del producto...';
      case 'CODE':
        return 'Buscar específicamente por código de referencia o código de barras...';
      case 'ALL':
      default:
        return 'Buscar por nombre, código de barras o ID Odoo...';
    }
  };

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title={
        <div className="flex items-center gap-2 text-slate-900">
          {isReplaceMode ? (
            <>
              <ArrowLeftRight className="h-5 w-5 text-amber-600" />
              <span>Reemplazar Producto en la Orden</span>
            </>
          ) : (
            <>
              <Package className="h-5 w-5 text-emerald-600" />
              <span>Catálogo de Productos Odoo (product.product)</span>
            </>
          )}
        </div>
      }
      description={
        isReplaceMode
          ? `Seleccione el nuevo producto que sustituirá a "${replacingProductName}". Se preservarán los ítems restantes de la orden.`
          : 'Seleccione los productos del catálogo de Odoo ERP para agregarlos a la orden en curso.'
      }
      className="max-w-4xl"
    >
      <div className="space-y-4 pt-1">
        {/* Banner de Stock Error (RN-03) */}
        {stockError && (
          <div className="p-3 rounded-lg bg-rose-50 border border-rose-200 text-rose-800 text-xs flex items-center justify-between gap-2 shadow-xs">
            <div className="flex items-center space-x-2">
              <AlertCircle className="h-4 w-4 text-rose-600 shrink-0" />
              <span className="font-medium">{stockError}</span>
            </div>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setStockError(null)}
              className="h-6 px-1.5 text-rose-600 hover:text-rose-800 hover:bg-rose-100"
            >
              <X className="h-3.5 w-3.5" />
            </Button>
          </div>
        )}

        {/* Banner Informativo de Modo Reemplazo */}
        {isReplaceMode && (
          <div className="p-3 rounded-lg bg-amber-50 border border-amber-200 text-amber-900 text-xs flex items-center space-x-2">
            <ArrowLeftRight className="h-4 w-4 text-amber-600 shrink-0" />
            <div>
              <span className="font-semibold">Modo Reemplazo:</span> El producto actual a reemplazar está deshabilitado. Puede elegir cualquier otro producto del catálogo o uno ya incluido en la orden (se incrementará su cantidad).
            </div>
          </div>
        )}

        {/* Controles de Búsqueda, Combobox y Filtro de Existencias */}
        <div className="flex flex-col sm:flex-row gap-3 items-stretch sm:items-center justify-between">
          <div className="flex items-center gap-2 flex-1 sm:max-w-md">
            <select
              value={searchCriteria}
              onChange={(e) => setSearchCriteria(e.target.value as ProductSearchCriteria)}
              className="text-xs h-9 rounded-lg border border-slate-300 bg-white px-2.5 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-emerald-500 cursor-pointer shadow-xs"
            >
              <option value="ALL">🔍 Todos</option>
              <option value="NAME">📦 Nombre</option>
              <option value="CODE">🏷️ Código</option>
              <option value="ID">#️⃣ ID Odoo</option>
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

          <div className="flex items-center gap-3 self-end sm:self-auto">
            <label className="flex items-center space-x-2 text-xs font-medium text-slate-700 cursor-pointer select-none bg-slate-50 px-3 py-2 rounded-lg border border-slate-200 hover:bg-slate-100 transition-colors">
              <input
                type="checkbox"
                checked={onlyInStock}
                onChange={(e) => setOnlyInStock(e.target.checked)}
                className="rounded text-emerald-600 focus:ring-emerald-500 h-4 w-4"
              />
              <span>Solo productos con existencias disponibles</span>
            </label>

            <span className="text-xs text-slate-500 font-mono">
              {totalCount.toLocaleString()} producto(s) en Odoo
            </span>
          </div>
        </div>

        {/* Product Catalog Table */}
        <div className="max-h-96 overflow-y-auto border border-slate-200 rounded-lg">
          {isEffectiveLoading ? (
            <div className="p-8 text-center text-slate-500">
              <Package className="h-8 w-8 mx-auto text-emerald-600 animate-spin mb-2" />
              <span>Cargando catálogo desde Odoo 20...</span>
            </div>
          ) : pagedProducts.length === 0 ? (
            <div className="p-8 text-center text-slate-400">
              No se encontraron productos coincidentes con los filtros aplicados.
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-20">ID Odoo</TableHead>
                  <TableHead>Producto</TableHead>
                  <TableHead className="text-right">Precio Unitario</TableHead>
                  <TableHead className="text-center">Stock Disp.</TableHead>
                  <TableHead className="text-right w-44">Acción</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {pagedProducts.map((product) => {
                  const isInCart = selectedProductIds.includes(product.id);
                  const isCurrentReplacing = isReplaceMode && product.id === replacingProductId;
                  const isOutOfStock = product.availableQuantity <= 0;

                  return (
                    <TableRow
                      key={product.id}
                      className={
                        isCurrentReplacing
                          ? 'bg-amber-50/60 opacity-60'
                          : isInCart
                          ? 'bg-emerald-50/40'
                          : isOutOfStock
                          ? 'bg-rose-50/30'
                          : ''
                      }
                    >
                      <TableCell className="font-mono text-xs text-slate-500 font-semibold">
                        #{product.id}
                      </TableCell>
                      <TableCell>
                        <div className="font-medium text-slate-900 text-xs sm:text-sm flex items-center gap-1.5">
                          <span>{product.name}</span>
                          {isInCart && !isReplaceMode && (
                            <Badge variant="success" className="bg-emerald-100 text-emerald-800 text-[10px] px-1.5 py-0">
                              En carrito
                            </Badge>
                          )}
                          {isCurrentReplacing && (
                            <Badge variant="warning" className="bg-amber-100 text-amber-800 text-[10px] px-1.5 py-0">
                              Producto actual
                            </Badge>
                          )}
                        </div>
                        <div className="text-xs text-slate-400 space-x-2 font-mono mt-0.5">
                          {product.defaultCode && <span>Cód: {product.defaultCode}</span>}
                          {product.barcode && <span>Barras: {product.barcode}</span>}
                        </div>
                      </TableCell>
                      <TableCell className="text-right font-medium text-slate-800 font-mono text-xs sm:text-sm">
                        ${product.price.toFixed(2)}
                      </TableCell>
                      <TableCell className="text-center">
                        {isOutOfStock ? (
                          <Badge variant="danger" className="font-mono text-xs">
                            0 uds. (Agotado)
                          </Badge>
                        ) : product.availableQuantity <= 5 ? (
                          <Badge variant="warning" className="font-mono text-xs">
                            {product.availableQuantity} uds.
                          </Badge>
                        ) : (
                          <Badge variant="success" className="font-mono text-xs bg-emerald-50 text-emerald-700 border border-emerald-200">
                            {product.availableQuantity} uds.
                          </Badge>
                        )}
                      </TableCell>
                      <TableCell className="text-right">
                        {isCurrentReplacing ? (
                          <span className="text-xs text-amber-600 italic">Actual</span>
                        ) : isReplaceMode ? (
                          <Button
                            type="button"
                            size="sm"
                            variant="default"
                            onClick={() => handleSelect(product)}
                            className="h-8 px-2.5 text-xs flex items-center space-x-1 ml-auto bg-amber-600 hover:bg-amber-700 text-white"
                          >
                            <ArrowLeftRight className="h-3.5 w-3.5" />
                            <span>Sustituir</span>
                          </Button>
                        ) : isInCart ? (
                          <span className="text-xs text-emerald-600 font-medium flex items-center justify-end space-x-1">
                            <CheckCircle2 className="h-3.5 w-3.5" />
                            <span>En orden</span>
                          </span>
                        ) : (
                          <Button
                            type="button"
                            size="sm"
                            onClick={() => handleSelect(product)}
                            className={`h-8 px-2.5 text-xs flex items-center space-x-1 ml-auto transition-colors ${
                              isOutOfStock
                                ? 'bg-slate-100 hover:bg-rose-50 text-slate-500 hover:text-rose-700 border border-slate-300'
                                : 'bg-emerald-600 hover:bg-emerald-700 text-white'
                            }`}
                            title={isOutOfStock ? 'Producto agotado en Odoo (clic para ver restricción)' : 'Agregar producto a la orden'}
                          >
                            <Plus className="h-3.5 w-3.5" />
                            <span>Agregar</span>
                          </Button>
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}

          {/* Pagination */}
          {!isEffectiveLoading && totalCount > 0 && (
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
            Total en Odoo: <strong className="font-semibold text-slate-800">{totalCount.toLocaleString()}</strong> productos (product.product)
          </span>
          <Button type="button" variant="outline" size="sm" onClick={() => onOpenChange(false)}>
            Cerrar Catálogo
          </Button>
        </div>
      </div>
    </Dialog>
  );
};
