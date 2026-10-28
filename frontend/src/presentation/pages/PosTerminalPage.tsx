import React, { useState } from 'react';
import { usePosCart } from '../hooks/usePosCart';
import { useProducts } from '../hooks/useProducts';
import { useCheckout } from '../hooks/useCheckout';
import { ProductCatalogPanel } from '../components/pos/ProductCatalogPanel';
import { CartSummaryPanel } from '../components/pos/CartSummaryPanel';
import { PaymentModal } from '../components/pos/PaymentModal';
import { Badge } from '../components/ui/Badge';
import { Button } from '../components/ui/Button';
import { Store, History, RefreshCw, AlertCircle } from 'lucide-react';

interface PosTerminalPageProps {
  onNavigateHistory: () => void;
}

export const PosTerminalPage: React.FC<PosTerminalPageProps> = ({ onNavigateHistory }) => {
  const {
    rawCart,
    items,
    subtotal,
    discountAmount,
    taxAmount,
    total,
    discountPolicy,
    addItem,
    updateQuantity,
    removeItem,
    clearCart,
    applyDiscount,
  } = usePosCart();

  const { products, activeSession, loading, error, refresh } = useProducts();
  const { checkout, processing, lastResult } = useCheckout();

  const [paymentModalOpen, setPaymentModalOpen] = useState(false);

  const handleConfirmPayment = async (customerDoc: string, customerName: string) => {
    if (!activeSession) {
      return {
        success: false,
        errorMessage: 'No existe sesión de POS activa en Odoo.',
      };
    }

    return await checkout(rawCart, activeSession.id, customerDoc, customerName);
  };

  const handleSuccessDone = () => {
    clearCart();
    setPaymentModalOpen(false);
    refresh();
  };

  return (
    <div className="flex flex-col h-screen bg-slate-100 overflow-hidden">
      {/* Header Superior del Terminal */}
      <header className="h-14 bg-white border-b border-slate-200 px-6 flex items-center justify-between shrink-0">
        <div className="flex items-center gap-3">
          <div className="h-9 w-9 rounded-xl bg-emerald-600 flex items-center justify-center text-white font-bold shadow-sm">
            <Store className="h-5 w-5" />
          </div>
          <div>
            <h1 className="text-sm font-bold text-slate-800 leading-tight">Terminal Punto de Venta (POS)</h1>
            <p className="text-xs text-slate-500">Integración Odoo 20 Community &bull; Clean Architecture</p>
          </div>
        </div>

        <div className="flex items-center gap-3">
          {activeSession ? (
            <Badge variant="success" className="bg-emerald-50 text-emerald-700 border-emerald-300 flex items-center gap-1.5 py-1">
              <span className="h-2 w-2 rounded-full bg-emerald-500 animate-pulse"></span>
              Sesión Odoo #{activeSession.id}: {activeSession.name} ({activeSession.state})
            </Badge>
          ) : (
            <Badge variant="warning" className="bg-amber-50 text-amber-700 border-amber-300 flex items-center gap-1.5 py-1">
              <AlertCircle className="h-3.5 w-3.5" />
              Sin sesión activa en Odoo
            </Badge>
          )}

          <Button variant="outline" size="sm" onClick={() => refresh()} className="h-8 gap-1.5">
            <RefreshCw className="h-3.5 w-3.5" />
            Recargar
          </Button>

          <Button variant="default" size="sm" onClick={onNavigateHistory} className="h-8 gap-1.5 bg-slate-800 hover:bg-slate-900 text-white">
            <History className="h-3.5 w-3.5" />
            Historial de Ventas
          </Button>
        </div>
      </header>

      {/* Alerta de Error de Conexión si aplica */}
      {error && (
        <div className="bg-rose-50 border-b border-rose-200 px-6 py-2 text-rose-700 text-xs flex items-center justify-between">
          <div className="flex items-center gap-2">
            <AlertCircle className="h-4 w-4 shrink-0" />
            <span>Fallo de sincronización con backend/Odoo: {error}</span>
          </div>
          <button onClick={() => refresh()} className="underline font-bold text-rose-800">
            Reintentar
          </button>
        </div>
      )}

      {/* Área Principal Dividida en 2 Paneles Modulares */}
      <div className="flex-1 p-4 grid grid-cols-1 md:grid-cols-12 gap-4 overflow-hidden">
        {/* Catálogo de Productos Odoo (Columna Izquierda 8/12) */}
        <div className="md:col-span-7 lg:col-span-8 h-full overflow-hidden">
          <ProductCatalogPanel
            products={products}
            onAddToCart={addItem}
            loading={loading}
            onSearch={(query) => refresh(query)}
          />
        </div>

        {/* Resumen del Carrito y Total (Columna Derecha 4/12) */}
        <div className="md:col-span-5 lg:col-span-4 h-full overflow-hidden">
          <CartSummaryPanel
            items={items}
            subtotal={subtotal}
            discountAmount={discountAmount}
            taxAmount={taxAmount}
            total={total}
            discountPolicy={discountPolicy}
            onUpdateQuantity={updateQuantity}
            onRemoveItem={removeItem}
            onClearCart={clearCart}
            onApplyDiscount={applyDiscount}
            onOpenPayment={() => setPaymentModalOpen(true)}
            disabled={!activeSession || !activeSession.isOpen}
          />
        </div>
      </div>

      {/* Modal de Finalización y Cobro */}
      <PaymentModal
        isOpen={paymentModalOpen}
        onClose={() => setPaymentModalOpen(false)}
        total={total}
        onConfirmPayment={handleConfirmPayment}
        processing={processing}
        lastResult={lastResult}
        onSuccessDone={handleSuccessDone}
      />
    </div>
  );
};
