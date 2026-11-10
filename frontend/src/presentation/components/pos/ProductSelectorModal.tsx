import React, { useState, useMemo, useEffect } from 'react';
import { Search, Plus, Check, AlertCircle, Package, X, CheckCircle2, ArrowLeftRight } from 'lucide-react';
import { Product } from '@/domain/Product';
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
  products: Product[];
  selectedProductIds: number[];
  onSelectProduct: (product: Product) => void;
  loading?: boolean;
  replacingProductId?: number | null;
  replacingProductName?: string;
}

export const ProductSelectorModal: React.FC<ProductSelectorModalProps> = ({
  open,
  onOpenChange,
  products,
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

  useEffect(() => {
    setPageNumber(1);
    setStockError(null);
  }, [debouncedSearchTerm, onlyInStock, searchCriteria, open]);

  const filteredProducts = useMemo(() => {
    const term = debouncedSearchTerm.trim().toLowerCase();
    const cleanIdTerm = term.replace(/^id:?|^#/i, '').trim();

    return products.filter((p) => {
      if (onlyInStock && p.availableQuantity <= 0) {
        return false;
      }

      if (!term) return true;

      const matchName = (p.name || '').toLowerCase().includes(term);
      const matchCode =
        (p.defaultCode && p.defaultCode.toLowerCase().includes(term)) ||
        (p.barcode && p.barcode.toLowerCase().includes(term));
      const matchId =
        cleanIdTerm !== '' && (String(p.id) === cleanIdTerm || String(p.id).includes(cleanIdTerm));

      if (searchCriteria === 'ID') return matchId;
      if (searchCriteria === 'NAME') return matchName;
      if (searchCriteria === 'CODE') return matchCode;
      return matchName || matchCode || matchId;
    });
  }, [products, onlyInStock, debouncedSearchTerm, searchCriteria]);

  const totalCount = filteredProducts.length;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  const pagedProducts = useMemo(() => {
    const start = (pageNumber - 1) * pageSize;
    return filteredProducts.slice(start, start + pageSize);
  }, [filteredProducts, pageNumber, pageSize]);

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
        isReplaceMode ? (
          <div className="flex items-center gap-2 text-slate-900">
            <ArrowLeftRight className="h-5 w-5 text-blue-600" />
            <span>Reemplazar Producto: {replacingProductName || `#${replacingProductId}`}</span>
          </div>
        ) : (
          <div className="flex items-center gap-2 text-slate-900">
            <Package className="h-5 w-5 text-emerald-600" />
            <span>Catálogo Completo de Productos Odoo</span>
          </div>
        )
      }
      description={
        isReplaceMode
          ? 'Seleccione el producto por el cual desea reemplazar este ítem en el carrito de compras.'
          : 'Seleccione productos disponibles en el POS de Odoo para agregarlos a la orden actual.'
      }
      className="max-w-4xl"
    >
      <div className="space-y-4 pt-1">
        {/* Banner de Error por Falta de Stock Disponible (Punto 1 y RN-03) */}
        {stockError && (
          <div className="p-3.5 rounded-lg bg-rose-50 border border-rose-300 text-rose-800 text-xs sm:text-sm flex items-start space-x-2.5 animate-in fade-in">
            <AlertCircle className="h-5 w-5 text-rose-600 shrink-0 mt-0.5" />
            <div className="flex-1 font-semibold leading-relaxed">{stockError}</div>
            <button
              type="button"
              onClick={() => setStockError(null)}
              className="text-rose-500 hover:text-rose-700 ml-2"
              title="Cerrar advertencia"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
        )}

        {/* Banner de Modo Reemplazo si aplica */}
        {isReplaceMode && (
          <div className="p-3 rounded-lg bg-blue-50 border border-blue-200 text-blue-900 text-xs sm:text-sm flex items-start space-x-2.5">
            <ArrowLeftRight className="h-5 w-5 text-blue-600 shrink-0 mt-0.5" />
            <div>
              <span className="font-semibold">Modo Reemplazo:</span> El producto actual a reemplazar está deshabilitado. Puede elegir cualquier otro producto del catálogo o uno ya incluido en la orden (se incrementará su cantidad).
            </div>
          </div>
        )}

        {/* Controles de Búsqueda, Combobox y Filtro de Existencias */}
        <div className="flex flex-col sm:flex-row gap-3 items-stretch sm:items-center justify-between">
          <div className="flex items-center gap-2 flex-1 sm:max-w-md">
            {/* Combobox Select Estilizado (Punto 6) */}
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

          <label className="flex items-center space-x-2 text-xs font-medium text-slate-700 cursor-pointer select-none bg-slate-50 px-3 py-2 rounded-lg border border-slate-200">
            <input
              type="checkbox"
              checked={onlyInStock}
              onChange={(e) => setOnlyInStock(e.target.checked)}
              className="rounded text-emerald-600 focus:ring-emerald-500 h-4 w-4"
            />
            <span>Solo productos con existencias disponibles</span>
          </label>
        </div>

        {/* Product Catalog Table */}
        <div className="max-h-96 overflow-y-auto border border-slate-200 rounded-lg">
          {loading ? (
            <div className="p-8 text-center text-slate-500">
              <Package className="h-8 w-8 mx-auto text-emerald-600 animate-spin mb-2" />
              <span>Cargando catálogo desde Odoo 20...</span>
            </div>
          ) : filteredProducts.length === 0 ? (
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
                  const isCurrentReplacingProduct = isReplaceMode && product.id === replacingProductId;
                  const isAlreadyInCart = selectedProductIds.includes(product.id);
                  const isOutOfStock = product.availableQuantity <= 0;

                  return (
                    <TableRow
                      key={product.id}
                      className={
                        isCurrentReplacingProduct
                          ? 'bg-slate-100/70 opacity-75'
                          : isAlreadyInCart
                          ? 'bg-amber-50/40'
                          : ''
                      }
                    >
                      <TableCell className="font-mono font-bold text-xs text-slate-500">
                        #{product.id}
                      </TableCell>
                      <TableCell>
                        <div className="font-medium text-slate-800">{product.name}</div>
                        {product.defaultCode ? (
                          <span className="text-[10px] font-mono font-bold text-emerald-800 bg-emerald-50 px-1.5 py-0.5 rounded border border-emerald-200/60 inline-block mt-0.5">
                            {product.defaultCode}
                          </span>
                        ) : null}
                        {isCurrentReplacingProduct ? (
                          <div className="text-[11px] text-slate-500 font-semibold flex items-center gap-1 mt-0.5">
                            <span className="w-1.5 h-1.5 rounded-full bg-slate-400 inline-block"></span>
                            Producto seleccionado para reemplazo
                          </div>
                        ) : isAlreadyInCart && isReplaceMode ? (
                          <div className="text-[11px] text-blue-700 font-semibold flex items-center gap-1 mt-0.5">
                            <CheckCircle2 className="w-3 h-3 text-blue-600" />
                            Ya en el carrito (se sumará +1)
                          </div>
                        ) : isAlreadyInCart ? (
                          <div className="text-[11px] text-amber-700 font-semibold flex items-center gap-1 mt-0.5">
                            <CheckCircle2 className="w-3 h-3 text-amber-600" />
                            Ya agregado al carrito
                          </div>
                        ) : null}
                      </TableCell>
                      <TableCell className="text-right font-mono font-semibold text-emerald-700">
                        ${Number(product.price).toFixed(2)}
                      </TableCell>
                      <TableCell className="text-center">
                        {isOutOfStock ? (
                          <Badge variant="danger">Agotado</Badge>
                        ) : product.availableQuantity <= 5 ? (
                          <Badge variant="warning">{product.availableQuantity} uds.</Badge>
                        ) : (
                          <Badge variant="success">{product.availableQuantity} uds.</Badge>
                        )}
                      </TableCell>
                      <TableCell className="text-right">
                        {isCurrentReplacingProduct ? (
                          <Button
                            type="button"
                            size="sm"
                            disabled={true}
                            className="h-8 px-3 text-xs bg-slate-200 text-slate-500 border border-slate-300 opacity-60 cursor-not-allowed ml-auto select-none"
                            title="No se puede reemplazar por el mismo producto"
                          >
                            <span>Producto actual</span>
                          </Button>
                        ) : isReplaceMode && isAlreadyInCart ? (
                          <Button
                            type="button"
                            size="sm"
                            onClick={() => handleSelect(product)}
                            className={`h-8 px-2.5 text-xs flex items-center space-x-1 ml-auto ${
                              isOutOfStock
                                ? 'bg-slate-100 hover:bg-rose-50 text-slate-500 hover:text-rose-700 border border-slate-300'
                                : 'bg-amber-600 hover:bg-amber-700 text-white'
                            }`}
                            title="Incrementa en 1 la cantidad de este ítem existente"
                          >
                            <Plus className="h-3.5 w-3.5" />
                            <span>Sumar +1</span>
                          </Button>
                        ) : isReplaceMode ? (
                          <Button
                            type="button"
                            size="sm"
                            onClick={() => handleSelect(product)}
                            className={`h-8 px-2.5 text-xs flex items-center space-x-1 ml-auto ${
                              isOutOfStock
                                ? 'bg-slate-100 hover:bg-rose-50 text-slate-500 hover:text-rose-700 border border-slate-300'
                                : 'bg-blue-600 hover:bg-blue-700 text-white'
                            }`}
                          >
                            <ArrowLeftRight className="h-3.5 w-3.5" />
                            <span>Reemplazar</span>
                          </Button>
                        ) : isAlreadyInCart ? (
                          <span className="inline-flex items-center text-xs font-semibold text-amber-700 bg-amber-100/80 px-2.5 py-1 rounded-md space-x-1">
                            <Check className="h-3.5 w-3.5 text-amber-600" />
                            <span>Agregado</span>
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
          {filteredProducts.length > 0 && (
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
            Mostrando {pagedProducts.length} de {totalCount} productos
          </span>
          <Button type="button" variant="outline" size="sm" onClick={() => onOpenChange(false)}>
            Cerrar Catálogo
          </Button>
        </div>
      </div>
    </Dialog>
  );
};
