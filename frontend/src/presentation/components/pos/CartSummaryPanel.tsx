import React from 'react';
import { CartItem, DiscountType } from '@/domain/CartItem';
import { Card, CardHeader, CardTitle, CardContent, CardFooter } from '../ui/Card';
import { Button } from '../ui/Button';
import { Badge } from '../ui/Badge';
import { Dialog } from '../ui/Dialog';
import { ShoppingCart, Plus, Minus, Trash2, Tag, Check, AlertTriangle } from 'lucide-react';

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

interface CartQuantityInputProps {
  productId: number;
  quantity: number;
  min?: number;
  max?: number;
  onUpdate: (productId: number, newQty: number) => void;
  disabled?: boolean;
}

const CartQuantityInput: React.FC<CartQuantityInputProps> = ({
  productId,
  quantity,
  min = 1,
  max = 1000,
  onUpdate,
  disabled = false,
}) => {
  const [localVal, setLocalVal] = React.useState<string>(String(quantity));

  // Sincronizar estado local si la cantidad cambia externamente (+/- o reset)
  React.useEffect(() => {
    setLocalVal(String(quantity));
  }, [quantity]);

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    const target = e.target as HTMLInputElement;
    const selStart = target.selectionStart ?? 0;
    const selEnd = target.selectionEnd ?? 0;
    const selectionLength = selEnd - selStart;
    const allSelected = selectionLength >= target.value.length;

    // Teclas de navegación y control permitidas
    const allowedKeys = [
      'Backspace',
      'Delete',
      'Tab',
      'Escape',
      'Enter',
      'ArrowLeft',
      'ArrowRight',
      'ArrowUp',
      'ArrowDown',
      'Home',
      'End',
    ];

    if (allowedKeys.includes(e.key) || e.ctrlKey || e.metaKey) {
      if (e.key === 'Enter') {
        target.blur();
        return;
      }
      if (e.key === 'ArrowUp') {
        e.preventDefault();
        const next = Math.min(max, quantity + 1);
        if (next !== quantity) onUpdate(productId, next);
        return;
      }
      if (e.key === 'ArrowDown') {
        e.preventDefault();
        const prev = Math.max(min, quantity - 1);
        if (prev !== quantity) onUpdate(productId, prev);
        return;
      }

      // Regla estricta: NO permitir dejar el campo en blanco.
      // Si queda un solo dígito o todo el texto está seleccionado, bloquear Backspace y Delete.
      if (e.key === 'Backspace' || e.key === 'Delete') {
        if (target.value.length <= 1 || allSelected) {
          e.preventDefault();
          return;
        }
      }
      return;
    }

    // Bloquear cualquier caracter que no sea dígito 0-9
    if (!/^[0-9]$/.test(e.key)) {
      e.preventDefault();
      return;
    }

    // Bloquear el dígito '0' si se intenta ingresar como primer dígito o reemplazando todo el valor
    if (e.key === '0' && (allSelected || target.value.length === 0 || (selStart === 0 && selectionLength === 0))) {
      e.preventDefault();
      return;
    }
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    // Sanitizar dejando solo dígitos y descartando ceros iniciales
    const digitsOnly = e.target.value.replace(/\D/g, '').replace(/^0+/, '');

    // Si por alguna razón queda vacío, mantener el valor actual o mínimo (nunca en blanco)
    if (digitsOnly === '') {
      setLocalVal(String(quantity || min));
      return;
    }

    let parsed = parseInt(digitsOnly, 10);

    // Límite de seguridad superior: bloquea números > 1000
    if (parsed > max) {
      parsed = max;
    }

    if (parsed < min) {
      parsed = min;
    }

    setLocalVal(String(parsed));
    onUpdate(productId, parsed);
  };

  const handleBlur = () => {
    // Garantizar al desenfocar que el valor sea válido (entre 1 y 1000)
    const parsed = parseInt(localVal, 10);
    if (isNaN(parsed) || parsed < min) {
      setLocalVal(String(min));
      if (quantity !== min) onUpdate(productId, min);
    } else if (parsed > max) {
      setLocalVal(String(max));
      if (quantity !== max) onUpdate(productId, max);
    } else {
      setLocalVal(String(parsed));
      if (quantity !== parsed) onUpdate(productId, parsed);
    }
  };

  const handlePaste = (e: React.ClipboardEvent<HTMLInputElement>) => {
    e.preventDefault();
    const pasted = e.clipboardData.getData('text').replace(/\D/g, '').replace(/^0+/, '');
    if (!pasted) return;

    let parsed = parseInt(pasted, 10);
    if (isNaN(parsed) || parsed < min) parsed = min;
    if (parsed > max) parsed = max;

    setLocalVal(String(parsed));
    onUpdate(productId, parsed);
  };

  return (
    <input
      type="text"
      inputMode="numeric"
      pattern="[0-9]*"
      value={localVal}
      onChange={handleChange}
      onKeyDown={handleKeyDown}
      onBlur={handleBlur}
      onPaste={handlePaste}
      onFocus={(e) => e.target.select()}
      disabled={disabled}
      className="h-7 w-12 text-center text-xs font-bold text-slate-800 bg-white border border-slate-200 rounded focus:outline-none focus:ring-1 focus:ring-emerald-500 focus:border-emerald-500 transition-colors shadow-inner"
      title={`Cantidad entre ${min} y ${max} unidades (editable)`}
    />
  );
};

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
  const [itemToRemove, setItemToRemove] = React.useState<CartItem | null>(null);
  const [isClearModalOpen, setIsClearModalOpen] = React.useState(false);

  return (
    <>
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
              onClick={() => setIsClearModalOpen(true)}
              disabled={disabled}
              className="text-xs text-rose-500 hover:text-rose-700 hover:bg-rose-50 h-7 px-2"
              title="Vaciar / Limpiar todo el carrito"
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
                <p className="font-semibold text-xs text-slate-800 truncate">
                  {item.productCode && (
                    <span className="font-mono text-emerald-800 bg-emerald-50 px-1 py-0.5 mr-1 rounded border border-emerald-200/80 text-[10px]">
                      {item.productCode}
                    </span>
                  )}
                  {item.productName}
                </p>
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
                  disabled={disabled || item.quantity <= 1}
                  title="Disminuir en 1"
                >
                  <Minus className="h-3 w-3" />
                </Button>
                <CartQuantityInput
                  productId={item.productId}
                  quantity={item.quantity}
                  min={1}
                  max={1000}
                  onUpdate={onUpdateQuantity}
                  disabled={disabled}
                />
                <Button
                  size="sm"
                  variant="outline"
                  className="h-7 w-7 p-0"
                  onClick={() => onUpdateQuantity(item.productId, item.quantity + 1)}
                  disabled={disabled || item.quantity >= 1000}
                  title="Aumentar en 1"
                >
                  <Plus className="h-3 w-3" />
                </Button>
                <Button
                  size="sm"
                  variant="ghost"
                  className="h-7 w-7 p-0 text-slate-400 hover:text-rose-600"
                  onClick={() => setItemToRemove(item)}
                  disabled={disabled}
                  title="Quitar ítem del carrito"
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

    {/* 1. Modal de Confirmación: Quitar Ítem */}
    <Dialog
      open={!!itemToRemove}
      onOpenChange={(isOpen) => !isOpen && setItemToRemove(null)}
      title={
        <div className="flex items-center gap-2 text-rose-600">
          <AlertTriangle className="h-5 w-5 text-rose-600" />
          <span>¿Quitar producto del carrito?</span>
        </div>
      }
      description="Confirma si deseas remover este producto de la venta actual."
      className="max-w-md"
    >
      {itemToRemove && (
        <div className="space-y-4">
          <div className="bg-slate-50 border border-slate-200 rounded-lg p-3 text-xs space-y-2">
            <p className="font-semibold text-slate-800 text-sm">{itemToRemove.productName}</p>
            <div className="flex justify-between text-slate-600 pt-1 border-t border-slate-200/60">
              <span>Cantidad a remover:</span>
              <span className="font-bold text-slate-800 font-mono">{itemToRemove.quantity} unidad(es)</span>
            </div>
            <div className="flex justify-between text-slate-600">
              <span>Precio unitario:</span>
              <span className="font-mono text-slate-700">${itemToRemove.unitPrice.toFixed(2)}</span>
            </div>
            <div className="flex justify-between text-slate-600 font-medium">
              <span>Subtotal de línea:</span>
              <span className="font-bold text-slate-900 font-mono">${itemToRemove.subtotal.toFixed(2)}</span>
            </div>
          </div>

          <p className="text-xs text-slate-500">
            El producto será eliminado de la lista y los cálculos de impuestos y totales se actualizarán de inmediato.
          </p>

          <div className="flex justify-end gap-2 pt-2 border-t border-slate-100">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setItemToRemove(null)}
            >
              Cancelar
            </Button>
            <Button
              type="button"
              variant="destructive"
              size="sm"
              onClick={() => {
                onRemoveItem(itemToRemove.productId);
                setItemToRemove(null);
              }}
              className="gap-1.5"
            >
              <Trash2 className="h-3.5 w-3.5" />
              <span>Quitar Producto</span>
            </Button>
          </div>
        </div>
      )}
    </Dialog>

    {/* 2. Modal de Confirmación: Vaciar / Limpiar Carrito */}
    <Dialog
      open={isClearModalOpen}
      onOpenChange={setIsClearModalOpen}
      title={
        <div className="flex items-center gap-2 text-rose-600">
          <Trash2 className="h-5 w-5 text-rose-600" />
          <span>¿Vaciar todo el carrito?</span>
        </div>
      }
      description="Esta acción cancelará la orden en preparación y limpiará todos los productos agregados."
      className="max-w-md"
    >
      <div className="space-y-4">
        <div className="bg-rose-50 border border-rose-200 rounded-lg p-3 text-xs space-y-1.5 text-rose-900">
          <p className="font-medium">
            Se eliminarán todos los <strong className="font-bold font-mono">{items.length} productos</strong> agregados actualmente.
          </p>
          <div className="flex justify-between text-rose-800 pt-1 border-t border-rose-200/60">
            <span>Total acumulado a descartar:</span>
            <span className="font-bold font-mono text-sm">${total.toFixed(2)}</span>
          </div>
        </div>

        <p className="text-xs text-slate-500">
          ¿Estás seguro de que deseas vaciar y reiniciar la venta actual? Esta acción no se puede deshacer.
        </p>

        <div className="flex justify-end gap-2 pt-2 border-t border-slate-100">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => setIsClearModalOpen(false)}
          >
            Cancelar
          </Button>
          <Button
            type="button"
            variant="destructive"
            size="sm"
            onClick={() => {
              onClearCart();
              setIsClearModalOpen(false);
            }}
            className="gap-1.5"
          >
            <Trash2 className="h-3.5 w-3.5" />
            <span>Sí, Vaciar Carrito</span>
          </Button>
        </div>
      </div>
    </Dialog>
    </>
  );
};
