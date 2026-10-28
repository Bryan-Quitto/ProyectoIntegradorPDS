import React, { useState } from 'react';
import { Product } from '@/domain/Product';
import { Card, CardHeader, CardTitle, CardContent } from '../ui/Card';
import { Input } from '../ui/Input';
import { Button } from '../ui/Button';
import { Badge } from '../ui/Badge';
import { Search, Plus, Package } from 'lucide-react';

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
  const [searchTerm, setSearchTerm] = useState('');

  const handleSearchChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value;
    setSearchTerm(val);
    onSearch(val);
  };

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
        <div className="relative mt-3">
          <Search className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
          <Input
            placeholder="Buscar por nombre o código de barra..."
            value={searchTerm}
            onChange={handleSearchChange}
            className="pl-9 bg-slate-50 focus:bg-white"
          />
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
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            {products.map((product) => (
              <div
                key={product.id}
                onClick={() => onAddToCart(product)}
                className="group relative p-3 border rounded-xl hover:border-emerald-500 hover:shadow-md transition-all cursor-pointer bg-white flex flex-col justify-between"
              >
                <div>
                  <h4 className="font-semibold text-slate-800 text-sm line-clamp-2 group-hover:text-emerald-700 transition-colors">
                    {product.name}
                  </h4>
                  {product.barcode && (
                    <p className="text-xs font-mono text-slate-400 mt-1">EAN: {product.barcode}</p>
                  )}
                </div>
                <div className="mt-3 flex items-center justify-between pt-2 border-t border-slate-100">
                  <span className="text-base font-bold text-slate-900">${product.price.toFixed(2)}</span>
                  <Button
                    size="sm"
                    variant="ghost"
                    className="h-8 w-8 p-0 rounded-full group-hover:bg-emerald-500 group-hover:text-white"
                  >
                    <Plus className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
};
