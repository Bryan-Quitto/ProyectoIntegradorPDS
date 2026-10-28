import React from 'react';
import { CartItem, DiscountType } from '@/domain/CartItem';
import { Card, CardHeader, CardTitle, CardContent, CardFooter } from '../ui/Card';
import { Button } from '../ui/Button';
import { Badge } from '../ui/Badge';
import { ShoppingCart, Plus, Minus, Trash2, Tag, Check } from 'lucide-react';

interface CartSummaryPanelProps {
  items: CartItem[];
  subtotal: number;
  discountAmount: number;
  taxAmount: number;
  total: number;
  discountPolicy: { type: DiscountType; value: number };
  onUpdateQuantity: (productId: number, quantity: number) => void;
  onRemoveItem: (productId: number) => void;
  onClearCart: () => void;
  onApplyDiscount: (type: DiscountType, value: number) => void;
  onOpenPayment: () => void;
  disabled: boolean;
}

export const CartSummaryPanel: React.FC<CartSummaryPanelProps> = ({
  items,
  subtotal,
  discountAmount,
  taxAmount,
  total,
  discountPolicy,
  onUpdateQuantity,
  onRemoveItem,
  onClearCart,
  onApplyDiscount,
  onOpenPayment,
  disabled,
}) => {
  return (
    <Card className="flex flex-col h-full shadow-sm border-slate-200">
      <CardHeader className="pb-3 border-b flex flex-row items-center justify-between">
        <div className="flex items-center gap-2">
          <ShoppingCart className="h-5 w-5 text-emerald-600" />
          <CardTitle className="text-lg font-bold">Carrito de Venta</CardTitle>
          <Badge variant="default">{items.length}</Badge>
        </div>
        {items.length > 0 && (
          <Button
            variant="ghost"
            size="sm"
            onClick={onClearCart}
            className="text-xs text-rose-500 hover:text-rose-700 hover:bg-rose-50 h-7 px-2"
          >
            Vaciar
          </Button>
        )}
      </CardHeader>

      <CardContent className="flex-1 overflow-y-auto p-4 space-y-3">
        {items.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-48 text-slate-400 text-center">
            <ShoppingCart className="h-10 w-10 mb-2 stroke-1" />
            <p className="font-medium">El carrito está vacío</p>
            <p className="text-xs">Selecciona productos del catálogo para iniciar la venta.</p>
          </div>
        ) : (
          items.map((item) => (
            <div
              key={item.productId}
              className="flex items-center justify-between p-2.5 bg-slate-50 border border-slate-100 rounded-lg hover:bg-slate-100 transition-colors"
            >
              <div className="flex-1 min-w-0 pr-2">
                <p className="font-semibold text-xs text-slate-800 truncate">{item.productName}</p>
                <p className="text-xs text-slate-500">
                  ${item.unitPrice.toFixed(2)} x {item.quantity} ={' '}
                  <span className="font-bold text-slate-700">${item.subtotal.toFixed(2)}</span>
                </p>
              </div>

              <div className="flex items-center gap-1.5">
                <Button
                  size="sm"
                  variant="outline"
                  className="h-7 w-7 p-0"
                  onClick={() => onUpdateQuantity(item.productId, item.quantity - 1)}
                  disabled={item.quantity <= 1}
                >
                  <Minus className="h-3 w-3" />
                </Button>
                <span className="text-xs font-bold w-6 text-center">{item.quantity}</span>
                <Button
                  size="sm"
                  variant="outline"
                  className="h-7 w-7 p-0"
                  onClick={() => onUpdateQuantity(item.productId, item.quantity + 1)}
                >
                  <Plus className="h-3 w-3" />
                </Button>
                <Button
                  size="sm"
                  variant="ghost"
                  className="h-7 w-7 p-0 text-slate-400 hover:text-rose-600"
                  onClick={() => onRemoveItem(item.productId)}
                >
                  <Trash2 className="h-3.5 w-3.5" />
                </Button>
              </div>
            </div>
          ))
        )}
      </CardContent>

      {/* Selector de Políticas de Descuento (Patrón Strategy en UI) */}
      <div className="px-4 py-2 border-t border-slate-100 bg-slate-50/50">
        <div className="flex items-center gap-1.5 mb-2">
          <Tag className="h-3.5 w-3.5 text-slate-500" />
          <span className="text-xs font-medium text-slate-700">Política de Descuento (Strategy):</span>
        </div>
        <div className="grid grid-cols-3 gap-1.5">
          <button
            type="button"
            onClick={() => onApplyDiscount('NONE', 0)}
            className={`text-xs py-1 px-2 rounded border transition-colors ${
              discountPolicy.type === 'NONE'
                ? 'bg-slate-800 text-white border-slate-800'
                : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-100'
            }`}
          >
            Sin desc.
          </button>
          <button
            type="button"
            onClick={() => onApplyDiscount('PERCENTAGE', 10)}
            className={`text-xs py-1 px-2 rounded border transition-colors ${
              discountPolicy.type === 'PERCENTAGE' && discountPolicy.value === 10
                ? 'bg-emerald-600 text-white border-emerald-600'
                : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-100'
            }`}
          >
            10% Promo
          </button>
          <button
            type="button"
            onClick={() => onApplyDiscount('THRESHOLD_FIXED', 5)}
            className={`text-xs py-1 px-2 rounded border transition-colors ${
              discountPolicy.type === 'THRESHOLD_FIXED'
                ? 'bg-emerald-600 text-white border-emerald-600'
                : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-100'
            }`}
          >
            -$5 (si &ge;$50)
          </button>
        </div>
      </div>

      <CardFooter className="flex flex-col p-4 border-t bg-slate-50/70 space-y-2">
        <div className="w-full text-xs space-y-1">
          <div className="flex justify-between text-slate-600">
            <span>Subtotal Bruto:</span>
            <span>${subtotal.toFixed(2)}</span>
          </div>
          {discountAmount > 0 && (
            <div className="flex justify-between text-emerald-600 font-medium">
              <span>Descuento aplicado:</span>
              <span>-${discountAmount.toFixed(2)}</span>
            </div>
          )}
          <div className="flex justify-between text-slate-600">
            <span>IVA Ecuador (15%):</span>
            <span>${taxAmount.toFixed(2)}</span>
          </div>
          <div className="flex justify-between text-base font-extrabold text-slate-900 pt-1.5 border-t border-slate-200">
            <span>Total a Pagar:</span>
            <span className="text-emerald-700">${total.toFixed(2)}</span>
          </div>
        </div>

        <Button
          className="w-full bg-emerald-600 hover:bg-emerald-700 text-white font-bold py-2.5 mt-2"
          onClick={onOpenPayment}
          disabled={disabled || items.length === 0}
        >
          <Check className="h-4 w-4 mr-2" />
          Cobrar e Imprimir
        </Button>
      </CardFooter>
    </Card>
  );
};
