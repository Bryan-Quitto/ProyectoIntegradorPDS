import React, { useState, useMemo, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { usePosCart } from '../hooks/usePosCart';
import { useProducts } from '../hooks/useProducts';
import { useCheckout } from '../hooks/useCheckout';
import { Customer } from '@/application/ports/IPosApiGateway';
import { Product } from '@/domain/Product';
import { CartItem } from '@/domain/CartItem';
import { validateEcuadorianId } from '@/domain/ecuadorianIdValidator';
import { useAuthStore } from '../stores/useAuthStore';
import { PaymentModal } from '../components/pos/PaymentModal';
import { CustomerSelectorModal } from '../components/pos/CustomerSelectorModal';
import { ProductSelectorModal } from '../components/pos/ProductSelectorModal';
import { Card, CardHeader, CardTitle, CardContent } from '../components/ui/Card';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '../components/ui/Table';
import { Badge } from '../components/ui/Badge';
import { Button } from '../components/ui/Button';
import { Dialog } from '../components/ui/Dialog';
import { Input } from '../components/ui/Input';
import {
  Store,
  History,
  RefreshCw,
  AlertCircle,
  User,
  UserCheck,
  UserPlus,
  Package,
  X,
  ShoppingCart,
  Plus,
  Minus,
  Trash2,
  ArrowLeftRight,
  CreditCard,
  Receipt,
  Tag,
  CheckCircle2,
  AlertTriangle,
  RotateCcw,
  Building2,
  Phone,
  Mail,
  MapPin,
  Loader2,
  LogOut,
} from 'lucide-react';

interface PosTerminalPageProps {
  onNavigateHistory?: () => void;
}

interface TableQuantityInputProps {
  productId: number;
  quantity: number;
  min?: number;
  max?: number;
  onUpdate: (productId: number, newQty: number) => void;
  disabled?: boolean;
}

const TableQuantityInput: React.FC<TableQuantityInputProps> = ({
  productId,
  quantity,
  min = 1,
  max = 1000,
  onUpdate,
  disabled = false,
}) => {
  const [localVal, setLocalVal] = React.useState<string>(String(quantity));

  React.useEffect(() => {
    setLocalVal(String(quantity));
  }, [quantity]);

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    const target = e.target as HTMLInputElement;
    const selStart = target.selectionStart ?? 0;
    const selEnd = target.selectionEnd ?? 0;
    const selectionLength = selEnd - selStart;
    const allSelected = selectionLength >= target.value.length;

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

      if (e.key === 'Backspace' || e.key === 'Delete') {
        if (target.value.length <= 1 || allSelected) {
          e.preventDefault();
          return;
        }
      }
      return;
    }

    if (!/^[0-9]$/.test(e.key)) {
      e.preventDefault();
      return;
    }

    if (e.key === '0' && (allSelected || target.value.length === 0 || (selStart === 0 && selectionLength === 0))) {
      e.preventDefault();
      return;
    }
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const digitsOnly = e.target.value.replace(/\D/g, '').replace(/^0+/, '');
    if (digitsOnly === '') {
      setLocalVal(String(quantity || min));
      return;
    }

    let parsed = parseInt(digitsOnly, 10);
    if (parsed > max) parsed = max;
    if (parsed < min) parsed = min;

    setLocalVal(String(parsed));
    onUpdate(productId, parsed);
  };

  const handleBlur = () => {
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

  return (
    <div className="inline-flex items-center border border-slate-300 rounded-lg bg-white overflow-hidden shadow-2xs">
      <button
        type="button"
        onClick={() => onUpdate(productId, Math.max(min, quantity - 1))}
        disabled={disabled || quantity <= min}
        className="px-2.5 py-1.5 bg-slate-50 hover:bg-slate-100 text-slate-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
        aria-label="Disminuir cantidad"
      >
        <Minus className="h-3.5 w-3.5" />
      </button>
      <input
        type="text"
        inputMode="numeric"
        pattern="[0-9]*"
        value={localVal}
        onChange={handleChange}
        onKeyDown={handleKeyDown}
        onBlur={handleBlur}
        onFocus={(e) => e.target.select()}
        disabled={disabled}
        className="w-14 text-center font-mono font-semibold text-sm py-1 border-x border-slate-200 focus:outline-none focus:bg-emerald-50/40 text-slate-800"
      />
      <button
        type="button"
        onClick={() => onUpdate(productId, Math.min(max, quantity + 1))}
        disabled={disabled || quantity >= max}
        className="px-2.5 py-1.5 bg-slate-50 hover:bg-slate-100 text-slate-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
        aria-label="Aumentar cantidad"
      >
        <Plus className="h-3.5 w-3.5" />
      </button>
    </div>
  );
};

export const PosTerminalPage: React.FC<PosTerminalPageProps> = ({ onNavigateHistory }) => {
  const navigate = useNavigate();
  const user = useAuthStore((state) => state.user);
  const logout = useAuthStore((state) => state.logout);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const handleNavigateHistory = () => {
    if (onNavigateHistory) {
      onNavigateHistory();
    } else {
      navigate('/historial');
    }
  };

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
  const { checkout, processing, lastResult, resetCheckoutSession } = useCheckout();

  // Verificación de caja asignada al vendedor (Fase 4 / Control de Acceso)
  const hasAssignedSession = Boolean(user?.posSessionId && user.posSessionId > 0);

  // Estados de Modales y Selección (Puntos 1, 5 y 6)
  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(null);
  const [customerModalOpen, setCustomerModalOpen] = useState(false);
  const [productModalOpen, setProductModalOpen] = useState(false);
  const [replacingItem, setReplacingItem] = useState<CartItem | null>(null);
  const [paymentModalOpen, setPaymentModalOpen] = useState(false);

  // Estados de Diálogos de Confirmación
  const [itemToDelete, setItemToDelete] = useState<CartItem | null>(null);
  const [showResetConfirm, setShowResetConfirm] = useState(false);

  // Estado para la validación Pre-Flight en caliente al presionar 'Finalizar Venta / Cobrar POS'
  const [isValidatingPreflight, setIsValidatingPreflight] = useState(false);
  const [preflightErrorAlert, setPreflightErrorAlert] = useState<string | null>(null);

  // Limpiar alerta de pre-flight si el usuario modifica los ítems o el cliente
  useEffect(() => {
    setPreflightErrorAlert(null);
  }, [items, selectedCustomer]);

  // Mapa de productos de Odoo para consultar stock disponible en tiempo real
  const productMap = useMemo(() => new Map(products.map((p) => [p.id, p])), [products]);

  // Validación de cédula o RUC del cliente seleccionado
  const customerDocValidation = useMemo(() => {
    if (!selectedCustomer?.vat) return null;
    return validateEcuadorianId(selectedCustomer.vat);
  }, [selectedCustomer]);

  // [SHIFT-LEFT VALIDATION]: Validación temprana de datos del cliente
  const customerValidationError = useMemo(() => {
    if (!selectedCustomer) return null; // Consumidor Final predeterminado es siempre válido (9999999999999)
    if (!selectedCustomer.vat || selectedCustomer.vat.trim() === '') {
      return 'El cliente seleccionado no tiene Cédula o RUC registrada en Odoo.';
    }
    if (customerDocValidation && !customerDocValidation.isValid) {
      return `Identificación del cliente inválida: ${customerDocValidation.error}`;
    }
    return null;
  }, [selectedCustomer, customerDocValidation]);

  // [SHIFT-LEFT VALIDATION]: Detección anticipada de inconsistencias de stock en las líneas de la orden
  const stockValidationErrors = useMemo(() => {
    const errors: { productId: number; productName: string; availableStock: number; message: string; isOutOfStock: boolean }[] = [];
    for (const item of items) {
      const catalogProd = productMap.get(item.productId);
      const available = catalogProd?.availableQuantity ?? 0;
      if (available <= 0) {
        errors.push({
          productId: item.productId,
          productName: item.productName,
          availableStock: 0,
          message: `"${item.productName}" está agotado en Odoo (0 disponibles).`,
          isOutOfStock: true,
        });
      } else if (item.quantity > available) {
        errors.push({
          productId: item.productId,
          productName: item.productName,
          availableStock: available,
          message: `"${item.productName}": cantidad (${item.quantity} uds.) supera el stock (${available} disponibles).`,
          isOutOfStock: false,
        });
      }
    }
    return errors;
  }, [items, productMap]);

  // Condición de guardia para habilitar el cobro POS (Pre-flight antes de abrir el modal)
  const canProceedToPayment =
    hasAssignedSession &&
    Boolean(activeSession) &&
    items.length > 0 &&
    stockValidationErrors.length === 0 &&
    !customerValidationError;

  // Total de unidades acumuladas en el carrito
  const totalUnits = useMemo(() => items.reduce((acc, i) => acc + i.quantity, 0), [items]);

  // Manejador de selección o reemplazo de productos desde el Modal dedicado
  const handleSelectOrReplaceProduct = (product: Product) => {
    if (!hasAssignedSession) return;

    if (replacingItem) {
      const oldId = replacingItem.productId;
      const newId = product.id;

      if (oldId === newId) {
        setReplacingItem(null);
        setProductModalOpen(false);
        return;
      }

      // Si el nuevo producto ya existe en el carrito
      const alreadyInCart = items.find((i) => i.productId === newId);
      if (alreadyInCart) {
        removeItem(oldId);
        updateQuantity(newId, alreadyInCart.quantity + replacingItem.quantity);
      } else {
        const prevQty = replacingItem.quantity;
        removeItem(oldId);
        addItem(product, prevQty);
      }

      setReplacingItem(null);
      setProductModalOpen(false);
      return;
    }

    // Modo normal de adición
    addItem(product, 1);
  };

  // Enlace del partner_id de Odoo a la orden
  const handleConfirmPayment = async (
    customerDoc: string,
    customerName: string,
    paymentMethod: string,
    partnerId?: number | null
  ) => {
    if (!activeSession) {
      return {
        success: false,
        errorMessage: 'No existe sesión de POS activa en Odoo.',
      };
    }

    const effectivePartnerId = partnerId !== undefined ? partnerId : selectedCustomer?.id ?? null;
    const result = await checkout(
      rawCart,
      activeSession.id,
      customerDoc,
      customerName,
      paymentMethod,
      effectivePartnerId
    );

    // [SINCRONIZACIÓN INMEDIATA DE STOCK]: Al confirmarse la venta en Odoo, recargamos el catálogo
    if (result.success) {
      void refresh();
    }

    return result;
  };

  // Recargar catálogo de stock siempre que se abra el modal de búsqueda de productos
  useEffect(() => {
    if (productModalOpen) {
      void refresh();
    }
  }, [productModalOpen, refresh]);

  // [FIX UI/UX]: Si el usuario cierra el modal de venta sincronizada con la 'X' o botón secundario para modificar la orden,
  // NO limpiamos el carrito. Los productos se preservan en la orden para que pueda hacer ajustes.
  const handleClosePaymentModal = () => {
    resetCheckoutSession();
    setPaymentModalOpen(false);
  };

  // Solo al hacer clic explícito en 'Finalizar y Nueva Venta' se vacía el carrito y se restablece el cliente para el siguiente turno.
  const handleSuccessDone = () => {
    clearCart();
    applyDiscount('NONE', 0);
    setSelectedCustomer(null);
    resetCheckoutSession();
    setPaymentModalOpen(false);
    refresh();
  };

  const handleConfirmReset = () => {
    clearCart();
    applyDiscount('NONE', 0);
    setSelectedCustomer(null);
    setShowResetConfirm(false);
  };

  // [SHIFT-LEFT VALIDATION PRE-FLIGHT]: Validación en caliente contra Odoo antes de abrir el modal de cobro
  const handleProceedToPayment = async () => {
    if (items.length === 0 || !hasAssignedSession) return;

    setIsValidatingPreflight(true);
    setPreflightErrorAlert(null);

    try {
      // 1. Consulta en caliente a Odoo ERP para sincronizar inventario y sesión en tiempo real
      const freshData = await refresh();
      const freshProducts = freshData?.products ?? products;
      const freshSession = freshData?.session ?? activeSession;

      // 2. Mapeo de stock actualizado
      const freshMap = new Map(freshProducts.map((p) => [p.id, p]));

      // 3. Verificación de stock de cada producto en la orden
      const stockErrors: string[] = [];
      for (const item of items) {
        const prod = freshMap.get(item.productId);
        const available = prod?.availableQuantity ?? 0;
        if (available <= 0) {
          stockErrors.push(`"${item.productName}" se encuentra agotado en Odoo (0 disponibles).`);
        } else if (item.quantity > available) {
          stockErrors.push(
            `"${item.productName}": la cantidad solicitada (${item.quantity} uds.) supera las existencias en Odoo (${available} disponibles).`
          );
        }
      }

      // 4. Verificación de cliente (SRI)
      let customerError: string | null = null;
      if (selectedCustomer) {
        if (!selectedCustomer.vat || selectedCustomer.vat.trim() === '') {
          customerError = 'El cliente seleccionado no tiene Cédula o RUC registrada en Odoo.';
        } else {
          const docValidation = validateEcuadorianId(selectedCustomer.vat);
          if (!docValidation.isValid) {
            customerError = `Identificación del cliente inválida según algoritmo SRI: ${docValidation.error}`;
          }
        }
      }

      // 5. Verificación de sesión POS activa
      let sessionError: string | null = null;
      if (!freshSession) {
        sessionError = 'No se encontró una sesión de caja POS abierta en Odoo ERP.';
      }

      const totalErrorsCount = stockErrors.length + (customerError ? 1 : 0) + (sessionError ? 1 : 0);

      if (totalErrorsCount > 0) {
        const primaryMessage =
          stockErrors.length > 0
            ? `No es posible proceder al cobro: Se detectó stock insuficiente en ${stockErrors.length} producto(s) en Odoo. Por favor ajuste las cantidades o retire los productos señalados en rojo.`
            : customerError
            ? `No es posible proceder al cobro: ${customerError}`
            : sessionError || 'Existen errores de validación que impiden el cobro.';

        setPreflightErrorAlert(primaryMessage);

        // Scroll suave hacia la sección de líneas de productos para destacar los errores
        const tableElement = document.getElementById('pos-order-lines-section');
        if (tableElement) {
          tableElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
        return;
      }

      // Todas las validaciones superadas con éxito contra Odoo ERP: Abrir modal de cobro
      setPaymentModalOpen(true);
    } catch {
      setPreflightErrorAlert('Error de comunicación con Odoo al verificar existencias en vivo.');
    } finally {
      setIsValidatingPreflight(false);
    }
  };

  const confirmDeleteItem = () => {
    if (itemToDelete) {
      removeItem(itemToDelete.productId);
      setItemToDelete(null);
    }
  };

  return (
    <div className="min-h-screen bg-slate-100 flex flex-col text-slate-800">
      {/* Header Superior del Terminal */}
      <header className="sticky top-0 z-20 h-16 bg-white border-b border-slate-200 px-4 sm:px-6 flex items-center justify-between shadow-xs">
        <div className="flex items-center gap-3">
          <div className="h-10 w-10 rounded-xl bg-emerald-600 flex items-center justify-center text-white font-bold shadow-sm">
            <Store className="h-5 w-5" />
          </div>
          <div>
            <h1 className="text-base font-bold text-slate-900 leading-tight">
              Terminal Punto de Venta (POS)
            </h1>
            <p className="text-xs text-slate-500">
              Integración Odoo 20 Community &bull; Clean Architecture &bull; Esquema Horizontal
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2 sm:gap-3">
          {/* Badge Vendedor y Caja Asignada (Fase 4) */}
          <div
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs border shadow-2xs ${
              hasAssignedSession
                ? 'bg-slate-50 border-slate-200'
                : 'bg-amber-50 border-amber-300 text-amber-800'
            }`}
          >
            <span className="font-medium flex items-center gap-1">
              <User className="h-3.5 w-3.5 text-slate-500" />
              {user?.name || 'Vendedor'}
            </span>
            <span className={hasAssignedSession ? 'text-slate-300' : 'text-amber-400'}>|</span>
            <span
              className={`font-semibold flex items-center gap-1.5 ${
                hasAssignedSession ? 'text-emerald-700' : 'text-amber-800'
              }`}
            >
              {hasAssignedSession ? (
                <>
                  <span className="h-2 w-2 rounded-full bg-emerald-500 animate-pulse"></span>
                  <Store className="h-3.5 w-3.5 text-emerald-600" />
                  <span>{user?.posConfigName || activeSession?.name || 'Caja Asignada'}</span>
                </>
              ) : (
                <>
                  <AlertCircle className="h-3.5 w-3.5 text-amber-600" />
                  <span>Sin caja asignada</span>
                </>
              )}
            </span>
          </div>

          {(items.length > 0 || selectedCustomer) && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setShowResetConfirm(true)}
              className="h-8 gap-1.5 text-xs text-slate-600 border-slate-300 hover:bg-slate-100"
            >
              <RotateCcw className="h-3.5 w-3.5" />
              <span className="hidden sm:inline">Limpiar Formulario</span>
            </Button>
          )}

          <Button
            variant="outline"
            size="sm"
            onClick={() => refresh()}
            className="h-8 gap-1.5 text-xs border-slate-300 hover:bg-slate-50"
          >
            <RefreshCw className="h-3.5 w-3.5" />
            <span className="hidden sm:inline">Actualizar</span>
          </Button>

          <Button
            variant="default"
            size="sm"
            onClick={handleNavigateHistory}
            className="h-8 gap-1.5 text-xs bg-slate-800 hover:bg-slate-900 text-white"
          >
            <History className="h-3.5 w-3.5" />
            <span>Historial</span>
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

      {/* Alerta de Error de Conexión Odoo si aplica */}
      {error && (
        <div className="bg-rose-50 border-b border-rose-200 px-6 py-2.5 text-rose-700 text-xs flex items-center justify-between">
          <div className="flex items-center gap-2">
            <AlertCircle className="h-4 w-4 shrink-0 text-rose-500" />
            <span className="font-medium">{error}</span>
          </div>
          <button
            onClick={() => refresh()}
            className="underline font-bold text-rose-800 hover:text-rose-950 ml-4 shrink-0 transition-colors"
          >
            Reintentar
          </button>
        </div>
      )}

      {/* Banner de Modo Consulta: Sin Caja Asignada (Fase 4) */}
      {!hasAssignedSession && (
        <div className="bg-amber-50 border-b border-amber-300 px-4 sm:px-6 py-3 text-amber-900 text-xs sm:text-sm flex items-start sm:items-center gap-3 shadow-2xs">
          <AlertCircle className="h-5 w-5 shrink-0 text-amber-600 mt-0.5 sm:mt-0" />
          <div className="flex-1 leading-relaxed">
            <span className="font-bold">⚠️ Atención: </span>
            <span>
              Su usuario no tiene una caja o sesión de POS activa asignada en Odoo ERP. El terminal se encuentra en modo sólo lectura (no puede agregar productos ni registrar ventas). Contacte al administrador en Odoo para que le asigne y abra una caja.
            </span>
          </div>
        </div>
      )}

      {/* Contenedor Principal: Layout Centrado en Marcos Horizontales (Estilo NORTHWIND-React) */}
      <main className="max-w-6xl mx-auto w-full p-4 sm:p-6 space-y-6 flex-1 pb-16">
        {/* MARCO 1: INFORMACIÓN DEL CLIENTE E IDENTIFICACIÓN FISCAL */}
        <Card className="border-slate-200 shadow-xs">
          <CardHeader className="pb-3 border-b border-slate-100 flex flex-col sm:flex-row justify-between sm:items-center gap-2">
            <CardTitle className="text-base font-semibold text-slate-900 flex items-center space-x-2">
              <UserCheck className="h-5 w-5 text-emerald-600" />
              <span>1. Información del Cliente e Identificación Fiscal (Odoo)</span>
            </CardTitle>

            <div className="flex items-center gap-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={!hasAssignedSession}
                onClick={() => setCustomerModalOpen(true)}
                className="h-8 text-xs gap-1.5 border-slate-300 hover:border-emerald-400 hover:bg-emerald-50 hover:text-emerald-800 disabled:opacity-50 disabled:cursor-not-allowed"
                title={!hasAssignedSession ? 'Modo sólo lectura: Sin caja asignada' : undefined}
              >
                <UserPlus className="h-3.5 w-3.5 text-emerald-600" />
                <span>{selectedCustomer ? 'Cambiar Cliente' : 'Seleccionar Cliente (Odoo)'}</span>
              </Button>

              {selectedCustomer && (
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={() => setSelectedCustomer(null)}
                  className="h-8 text-xs text-slate-500 hover:text-rose-600 hover:bg-rose-50"
                  title="Restablecer a Consumidor Final"
                >
                  <X className="h-3.5 w-3.5 mr-1" />
                  <span>Consumidor Final</span>
                </Button>
              )}
            </div>
          </CardHeader>

          <CardContent className="pt-4 space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {/* Cliente Activo / Información Principal */}
              <div>
                <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700 mb-1">
                  Cliente Comercial <span className="text-rose-500">*</span>
                </label>
                {selectedCustomer ? (
                  <div className="p-3.5 rounded-lg bg-emerald-50/70 border border-emerald-300 flex items-start justify-between gap-3">
                    <div className="space-y-1">
                      <div className="flex items-center gap-2">
                        <span className="font-bold text-slate-900 text-sm">{selectedCustomer.name}</span>
                        <span className="font-mono text-[10px] text-emerald-800 bg-white/90 px-1.5 py-0.5 rounded border border-emerald-300 font-semibold">
                          Odoo #{selectedCustomer.id}
                        </span>
                      </div>
                      <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-slate-600">
                        {selectedCustomer.email && (
                          <span className="flex items-center gap-1">
                            <Mail className="h-3 w-3 text-slate-400" />
                            {selectedCustomer.email}
                          </span>
                        )}
                        {selectedCustomer.phone && (
                          <span className="flex items-center gap-1">
                            <Phone className="h-3 w-3 text-slate-400" />
                            {selectedCustomer.phone}
                          </span>
                        )}
                        {selectedCustomer.street && (
                          <span className="flex items-center gap-1">
                            <MapPin className="h-3 w-3 text-slate-400" />
                            {selectedCustomer.street}
                          </span>
                        )}
                      </div>
                    </div>
                  </div>
                ) : (
                  <div className="p-3.5 rounded-lg bg-slate-50 border border-slate-200 flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <div className="h-8 w-8 rounded-lg bg-slate-200 flex items-center justify-center text-slate-500">
                        <User className="h-4 w-4" />
                      </div>
                      <div>
                        <div className="font-semibold text-slate-800 text-sm">Consumidor Final</div>
                        <div className="text-xs text-slate-500">
                          Venta genérica al público &bull; Sin partner asociado en Odoo
                        </div>
                      </div>
                    </div>
                    <Badge variant="default" className="text-slate-500 bg-white">
                      Por Defecto
                    </Badge>
                  </div>
                )}
              </div>

              {/* Cédula / RUC (Ecuador) - Lectura desde Odoo o Consumidor Final */}
              <div>
                <div className="flex items-center justify-between mb-1">
                  <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700">
                    Cédula / RUC (Ecuador) <span className="text-rose-500">*</span>
                  </label>
                  <span className="text-[11px] text-slate-400 font-medium">Solo lectura (Ficha Odoo)</span>
                </div>
                <Input
                  type="text"
                  value={selectedCustomer?.vat ? selectedCustomer.vat : '9999999999999'}
                  readOnly
                  tabIndex={-1}
                  className={`font-mono text-sm select-none cursor-not-allowed ${
                    customerValidationError
                      ? 'bg-rose-50 text-rose-950 font-bold border-rose-300'
                      : selectedCustomer?.vat
                      ? 'bg-emerald-50/40 text-emerald-950 font-bold border-emerald-300'
                      : 'bg-slate-100 text-slate-700 border-slate-300'
                  }`}
                />
                <div className="mt-1 flex items-center space-x-1.5 text-xs">
                  {selectedCustomer ? (
                    selectedCustomer.vat ? (
                      customerDocValidation?.isValid ? (
                        <span className="text-emerald-700 font-medium flex items-center space-x-1">
                          <CheckCircle2 className="h-3.5 w-3.5" />
                          <span>Identificación válida conforme al algoritmo SRI ({customerDocValidation.type})</span>
                        </span>
                      ) : (
                        <div className="p-2.5 rounded-lg bg-rose-50 border border-rose-200 text-rose-800 flex items-start justify-between gap-2 w-full mt-0.5">
                          <div className="flex items-start gap-1.5">
                            <AlertCircle className="h-4 w-4 text-rose-600 shrink-0 mt-0.5" />
                            <div>
                              <span className="font-bold">Identificación inválida según algoritmo SRI:</span>{' '}
                              <span>{customerDocValidation?.error || 'Dígito verificador incorrecto'}</span>
                            </div>
                          </div>
                          <button
                            type="button"
                            onClick={() => setSelectedCustomer(null)}
                            className="underline text-xs font-bold text-rose-800 hover:text-rose-950 shrink-0 ml-2"
                            title="Descartar cliente y utilizar Consumidor Final genérico autorizado por el SRI"
                          >
                            Usar Consumidor Final
                          </button>
                        </div>
                      )
                    ) : (
                      <div className="p-2.5 rounded-lg bg-rose-50 border border-rose-200 text-rose-800 flex items-center justify-between w-full mt-0.5">
                        <span className="flex items-center gap-1.5">
                          <AlertCircle className="h-4 w-4 text-rose-600 shrink-0" />
                          <span>Este cliente no tiene Cédula o RUC registrada en Odoo.</span>
                        </span>
                        <button
                          type="button"
                          onClick={() => setSelectedCustomer(null)}
                          className="underline text-xs font-bold text-rose-800 hover:text-rose-950 shrink-0 ml-2"
                        >
                          Usar Consumidor Final
                        </button>
                      </div>
                    )
                  ) : (
                    <span className="text-slate-500 flex items-center space-x-1">
                      <CheckCircle2 className="h-3.5 w-3.5 text-slate-400" />
                      <span>RUC genérico autorizado por el SRI para Consumidor Final (13 dígitos 9s).</span>
                    </span>
                  )}
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* MARCO 2: LÍNEAS DE PRODUCTOS / CARRITO */}
        <Card id="pos-order-lines-section" className="border-slate-200 shadow-xs">
          <CardHeader className="pb-3 border-b border-slate-100 flex flex-col sm:flex-row justify-between sm:items-center gap-3">
            <div className="flex items-center space-x-2">
              <ShoppingCart className="h-5 w-5 text-emerald-600" />
              <CardTitle className="text-base font-semibold text-slate-900">
                2. Líneas de Productos
              </CardTitle>
              <Badge variant="default" className="font-mono text-xs ml-1 bg-slate-50">
                {items.length} {items.length === 1 ? 'producto' : 'productos'} &bull; {totalUnits} unid.
              </Badge>
            </div>

            <Button
              type="button"
              variant="default"
              size="sm"
              disabled={!hasAssignedSession}
              onClick={() => {
                if (!hasAssignedSession) return;
                setReplacingItem(null);
                setProductModalOpen(true);
              }}
              title={
                !hasAssignedSession
                  ? 'Modo sólo lectura: Sin caja asignada en Odoo ERP'
                  : 'Agregar Producto'
              }
              className="bg-emerald-600 hover:bg-emerald-700 text-white flex items-center space-x-1.5 shadow-xs disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <Plus className="h-4 w-4" />
              <span>Agregar Producto</span>
            </Button>
          </CardHeader>

          <CardContent className="pt-4 space-y-4">
            {items.length === 0 ? (
              <div className="py-12 text-center border-2 border-dashed border-slate-200 rounded-xl bg-slate-50/50">
                <ShoppingCart className="h-12 w-12 mx-auto text-slate-300 mb-2" />
                <p className="font-semibold text-slate-700 text-sm">El carrito de productos está vacío</p>
                <p className="text-xs text-slate-400 mt-1 max-w-sm mx-auto">
                  Haga clic en &quot;Agregar Producto&quot; para abrir el catálogo modal de Odoo e incorporar ítems a la venta.
                </p>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={!hasAssignedSession}
                  onClick={() => {
                    if (!hasAssignedSession) return;
                    setReplacingItem(null);
                    setProductModalOpen(true);
                  }}
                  title={
                    !hasAssignedSession
                      ? 'Modo sólo lectura: Sin caja asignada en Odoo ERP'
                      : 'Abrir Catálogo de Productos'
                  }
                  className="mt-4 border-emerald-400 text-emerald-800 bg-white hover:bg-emerald-50 gap-1.5 disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  <Plus className="h-3.5 w-3.5 text-emerald-600" />
                  <span>Abrir Catálogo de Productos</span>
                </Button>
              </div>
            ) : (
              <div className="overflow-x-auto border border-slate-200 rounded-lg">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-12 text-center">#</TableHead>
                      <TableHead>Producto</TableHead>
                      <TableHead className="text-right">Precio Unitario ($)</TableHead>
                      <TableHead className="text-center">Stock Disp.</TableHead>
                      <TableHead className="text-center w-40">Cantidad</TableHead>
                      <TableHead className="text-right">Subtotal ($)</TableHead>
                      <TableHead className="text-center w-28">Acción</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {items.map((item, index) => {
                      const catalogProduct = productMap.get(item.productId);
                      const availableStock = catalogProduct?.availableQuantity ?? 0;
                      const isOutOfStock = availableStock <= 0;
                      const isExceedingStock = !isOutOfStock && item.quantity > availableStock;
                      const hasStockError = isOutOfStock || isExceedingStock;

                      return (
                        <TableRow
                          key={item.productId}
                          className={
                            hasStockError
                              ? 'bg-rose-50/70 border-l-4 border-l-rose-500 hover:bg-rose-50/90'
                              : 'hover:bg-slate-50/80'
                          }
                        >
                          <TableCell className="text-center font-mono text-xs text-slate-400">
                            {index + 1}
                          </TableCell>
                          <TableCell>
                            <div className="font-semibold text-slate-900 text-sm">{item.productName}</div>
                            <div className="flex items-center gap-2 mt-0.5">
                              <span className="text-[11px] font-mono text-slate-400">
                                Odoo ID #{item.productId}
                              </span>
                              {item.productCode && (
                                <Badge variant="default" className="text-[10px] py-0 px-1 font-mono">
                                  Ref: {item.productCode}
                                </Badge>
                              )}
                            </div>

                            {/* Alertas inline de existencias por producto */}
                            {isOutOfStock && (
                              <div className="mt-1.5 text-xs font-semibold text-rose-800 flex items-center gap-1.5 bg-rose-100/80 py-1 px-2 rounded border border-rose-200">
                                <AlertCircle className="h-3.5 w-3.5 text-rose-600 shrink-0" />
                                <span>Producto agotado o retirado en Odoo (0 disponibles). Debe retirarlo o reemplazarlo.</span>
                              </div>
                            )}
                            {isExceedingStock && (
                              <div className="mt-1.5 text-xs font-semibold text-rose-800 flex items-center justify-between gap-1.5 bg-rose-100/80 py-1 px-2 rounded border border-rose-200">
                                <div className="flex items-center gap-1.5">
                                  <AlertCircle className="h-3.5 w-3.5 text-rose-600 shrink-0" />
                                  <span>
                                    Cantidad solicitada ({item.quantity} uds.) supera existencias ({availableStock} disponibles).
                                  </span>
                                </div>
                                <button
                                  type="button"
                                  onClick={() => updateQuantity(item.productId, availableStock)}
                                  className="underline text-[11px] font-bold text-rose-900 hover:text-rose-950 shrink-0 ml-2"
                                  title="Ajustar automáticamente a la cantidad máxima disponible"
                                >
                                  Ajustar a {availableStock}
                                </button>
                              </div>
                            )}
                          </TableCell>
                          <TableCell className="text-right font-mono text-slate-700">
                            ${Number(item.unitPrice).toFixed(2)}
                          </TableCell>
                          <TableCell className="text-center">
                            {availableStock <= 0 ? (
                              <Badge variant="danger" className="font-mono text-xs">
                                Agotado
                              </Badge>
                            ) : (
                              <Badge
                                variant={availableStock <= 5 ? 'warning' : 'success'}
                                className="font-mono text-xs"
                              >
                                {availableStock} unid.
                              </Badge>
                            )}
                          </TableCell>
                          <TableCell className="text-center">
                            <TableQuantityInput
                              productId={item.productId}
                              quantity={item.quantity}
                              min={1}
                              max={availableStock > 0 ? availableStock : 1}
                              disabled={!hasAssignedSession || isOutOfStock}
                              onUpdate={updateQuantity}
                            />
                            {isExceedingStock ? (
                              <p className="text-[10px] text-rose-600 font-bold mt-1">
                                Stock insuficiente
                              </p>
                            ) : availableStock > 0 && item.quantity >= availableStock ? (
                              <p className="text-[10px] text-amber-700 font-medium mt-1">
                                Stock máx. alcanzado ({availableStock})
                              </p>
                            ) : null}
                          </TableCell>
                          <TableCell className="text-right font-mono font-bold text-slate-900">
                            ${Number(item.subtotal).toFixed(2)}
                          </TableCell>
                          <TableCell className="text-center">
                            <div className="flex items-center justify-center space-x-1">
                              <Button
                                type="button"
                                variant="ghost"
                                size="icon"
                                disabled={!hasAssignedSession}
                                onClick={() => {
                                  if (!hasAssignedSession) return;
                                  setReplacingItem(item);
                                  setProductModalOpen(true);
                                }}
                                className="h-8 w-8 text-emerald-600 hover:text-emerald-800 hover:bg-emerald-50 disabled:opacity-40 disabled:cursor-not-allowed"
                                title={
                                  !hasAssignedSession
                                    ? 'Modo sólo lectura: Sin caja asignada'
                                    : 'Reemplazar producto por otro del catálogo'
                                }
                              >
                                <ArrowLeftRight className="h-4 w-4" />
                              </Button>
                              <Button
                                type="button"
                                variant="ghost"
                                size="icon"
                                disabled={!hasAssignedSession}
                                onClick={() => {
                                  if (!hasAssignedSession) return;
                                  setItemToDelete(item);
                                }}
                                className="h-8 w-8 text-rose-500 hover:text-rose-700 hover:bg-rose-50 disabled:opacity-40 disabled:cursor-not-allowed"
                                title={
                                  !hasAssignedSession
                                    ? 'Modo sólo lectura: Sin caja asignada'
                                    : 'Eliminar producto de la orden'
                                }
                              >
                                <Trash2 className="h-4 w-4" />
                              </Button>
                            </div>
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </div>
            )}
          </CardContent>
        </Card>

        {/* MARCO 3: LIQUIDACIÓN FINANCIERA, ESTRATEGIA DE DESCUENTO Y COBRO POS */}
        <Card className="border-slate-200 shadow-xs">
          <CardHeader className="pb-3 border-b border-slate-100">
            <CardTitle className="text-base font-semibold text-slate-900 flex items-center space-x-2">
              <Receipt className="h-5 w-5 text-emerald-600" />
              <span>3. Liquidación Financiera y Cobro POS</span>
            </CardTitle>
          </CardHeader>

          <CardContent className="pt-4">
            <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
              {/* Columna Izquierda: Estrategia de Descuentos & Detalles Normativos */}
              <div className="lg:col-span-7 space-y-4">
                <div>
                  <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700 mb-2 flex items-center gap-1.5">
                    <Tag className="h-3.5 w-3.5 text-emerald-600" />
                    <span>Estrategia de Descuento (Patrón Strategy)</span>
                  </label>
                  <div className="grid grid-cols-3 gap-2">
                    <button
                      type="button"
                      disabled={!hasAssignedSession}
                      onClick={() => applyDiscount('NONE', 0)}
                      className={`text-xs py-2 px-3 rounded-lg border font-medium transition-all disabled:opacity-50 disabled:cursor-not-allowed ${
                        discountPolicy.type === 'NONE'
                          ? 'bg-slate-800 text-white border-slate-800 shadow-xs'
                          : 'bg-white text-slate-700 border-slate-200 hover:bg-slate-50'
                      }`}
                    >
                      Sin Descuento
                    </button>
                    <button
                      type="button"
                      disabled={!hasAssignedSession}
                      onClick={() => applyDiscount('PERCENTAGE', 10)}
                      className={`text-xs py-2 px-3 rounded-lg border font-medium transition-all disabled:opacity-50 disabled:cursor-not-allowed ${
                        discountPolicy.type === 'PERCENTAGE' && discountPolicy.value === 10
                          ? 'bg-emerald-600 text-white border-emerald-600 shadow-xs'
                          : 'bg-white text-slate-700 border-slate-200 hover:bg-slate-50'
                      }`}
                    >
                      10% Promoción
                    </button>
                    <button
                      type="button"
                      disabled={!hasAssignedSession}
                      onClick={() => applyDiscount('THRESHOLD_FIXED', 5)}
                      className={`text-xs py-2 px-3 rounded-lg border font-medium transition-all disabled:opacity-50 disabled:cursor-not-allowed ${
                        discountPolicy.type === 'THRESHOLD_FIXED'
                          ? 'bg-emerald-600 text-white border-emerald-600 shadow-xs'
                          : 'bg-white text-slate-700 border-slate-200 hover:bg-slate-50'
                      }`}
                    >
                      -$5 (si &ge; $50)
                    </button>
                  </div>
                </div>

                <div className="p-3 rounded-lg bg-slate-50 border border-slate-200 text-xs text-slate-600 space-y-1">
                  <p className="font-semibold text-slate-700">Normativa Fiscal y Sincronización:</p>
                  <p>&bull; IVA Tarifa 15% Ecuador calculado sobre base imponible neta.</p>
                  <p>&bull; Sincronización transaccional directa con Odoo 20 vía JSON-2 RPC (pos.order.sync_from_ui).</p>
                  <p>&bull; Al confirmar el cobro se generará el recibo térmico con opción de impresión directa.</p>
                </div>
              </div>

              {/* Columna Derecha: Resumen de Totales y Botón de Pago */}
              <div className="lg:col-span-5 bg-slate-50/80 p-4 rounded-xl border border-slate-200 space-y-3">
                <div className="space-y-1.5 text-xs text-slate-600">
                  <div className="flex justify-between">
                    <span>Subtotal Base Imponible:</span>
                    <span className="font-mono font-medium text-slate-800">${subtotal.toFixed(2)}</span>
                  </div>
                  {discountAmount > 0 && (
                    <div className="flex justify-between text-emerald-700 font-semibold">
                      <span>Descuento Aplicado:</span>
                      <span className="font-mono">-${discountAmount.toFixed(2)}</span>
                    </div>
                  )}
                  <div className="flex justify-between">
                    <span>IVA (Tarifa 15% Ecuador):</span>
                    <span className="font-mono font-medium text-slate-800">${taxAmount.toFixed(2)}</span>
                  </div>
                  <div className="flex justify-between text-base font-extrabold text-slate-900 pt-2 border-t border-slate-200">
                    <span>Total a Liquidar:</span>
                    <span className="font-mono text-emerald-700 text-lg">${total.toFixed(2)}</span>
                  </div>
                </div>

                {/* Banner de Errores Pre-flight (Shift-Left Validation) */}
                {(stockValidationErrors.length > 0 || customerValidationError || preflightErrorAlert) && (
                  <div className="p-3.5 rounded-lg bg-rose-50 border border-rose-300 text-rose-800 text-xs space-y-2 animate-in fade-in">
                    <div className="font-bold flex items-center gap-1.5 text-rose-900">
                      <AlertCircle className="h-4 w-4 shrink-0 text-rose-600" />
                      <span>
                        Acción requerida antes de cobrar ({stockValidationErrors.length + (customerValidationError ? 1 : 0) + (preflightErrorAlert && stockValidationErrors.length === 0 && !customerValidationError ? 1 : 0)}):
                      </span>
                    </div>
                    {preflightErrorAlert && (
                      <div className="p-2 rounded bg-rose-100/90 border border-rose-300/80 text-rose-950 font-semibold text-[11px] leading-snug">
                        {preflightErrorAlert}
                      </div>
                    )}
                    {(stockValidationErrors.length > 0 || customerValidationError) && (
                      <ul className="list-disc list-inside space-y-1 text-rose-800 pl-1 font-medium text-[11px]">
                        {stockValidationErrors.map((err) => (
                          <li key={err.productId} className="leading-tight">
                            {err.message}
                          </li>
                        ))}
                        {customerValidationError && (
                          <li className="leading-tight">{customerValidationError}</li>
                        )}
                      </ul>
                    )}
                    <p className="text-[10px] text-rose-600 italic pt-0.5">
                      Corrija los productos o la ficha del cliente señalados para habilitar el cobro.
                    </p>
                  </div>
                )}

                {/* Banner de Modo Consulta si no tiene caja */}
                {!hasAssignedSession && (
                  <div className="p-3.5 rounded-lg bg-amber-50 border border-amber-300 text-amber-900 text-xs flex items-start gap-2">
                    <AlertCircle className="h-4 w-4 shrink-0 text-amber-600 mt-0.5" />
                    <div>
                      <span className="font-bold">Modo Sólo Lectura: </span>
                      <span>
                        Su usuario no tiene una caja o sesión activa asignada en Odoo ERP. No está autorizado para registrar ventas ni procesar cobros.
                      </span>
                    </div>
                  </div>
                )}

                <Button
                  type="button"
                  size="lg"
                  onClick={handleProceedToPayment}
                  disabled={items.length === 0 || isValidatingPreflight || !hasAssignedSession}
                  className="w-full mt-2 bg-emerald-600 hover:bg-emerald-700 text-white font-bold flex items-center justify-center space-x-2 shadow-sm disabled:opacity-50 disabled:cursor-not-allowed transition-all"
                  title={
                    !hasAssignedSession
                      ? 'No puede cobrar: Su usuario no tiene una caja asignada en Odoo ERP'
                      : undefined
                  }
                >
                  {isValidatingPreflight ? (
                    <>
                      <Loader2 className="h-4 w-4 animate-spin mr-1.5" />
                      <span>Verificando existencias en Odoo...</span>
                    </>
                  ) : (
                    <>
                      <CreditCard className="h-4 w-4 mr-1.5" />
                      <span>Finalizar Venta / Cobrar POS</span>
                    </>
                  )}
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      </main>

      {/* Diálogo de Confirmación: Eliminar Ítem del Carrito */}
      <Dialog
        open={!!itemToDelete}
        onOpenChange={(isOpen: boolean) => !isOpen && setItemToDelete(null)}
        title={
          <div className="flex items-center gap-2 text-rose-600">
            <AlertTriangle className="h-5 w-5 text-rose-600" />
            <span>¿Quitar producto de la orden?</span>
          </div>
        }
        description="Confirma si deseas remover este producto de la venta actual."
        className="max-w-md"
      >
        {itemToDelete && (
          <div className="space-y-4">
            <div className="bg-slate-50 border border-slate-200 rounded-lg p-3 text-xs space-y-2">
              <p className="font-semibold text-slate-800 text-sm">{itemToDelete.productName}</p>
              <div className="flex justify-between text-slate-600 pt-1 border-t border-slate-200/60">
                <span>Cantidad a remover:</span>
                <span className="font-bold text-slate-800 font-mono">
                  {itemToDelete.quantity} unidad(es)
                </span>
              </div>
              <div className="flex justify-between text-slate-600">
                <span>Precio unitario:</span>
                <span className="font-mono text-slate-700">${itemToDelete.unitPrice.toFixed(2)}</span>
              </div>
              <div className="flex justify-between text-slate-600 font-medium">
                <span>Subtotal de línea:</span>
                <span className="font-bold text-slate-900 font-mono">
                  ${itemToDelete.subtotal.toFixed(2)}
                </span>
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
                onClick={() => setItemToDelete(null)}
              >
                Cancelar
              </Button>
              <Button
                type="button"
                variant="default"
                size="sm"
                onClick={confirmDeleteItem}
                className="bg-rose-600 hover:bg-rose-700 text-white"
              >
                Remover Producto
              </Button>
            </div>
          </div>
        )}
      </Dialog>

      {/* Diálogo de Confirmación: Limpiar / Restablecer Formulario */}
      <Dialog
        open={showResetConfirm}
        onOpenChange={setShowResetConfirm}
        title={
          <div className="flex items-center gap-2 text-slate-800">
            <RotateCcw className="h-5 w-5 text-amber-600" />
            <span>¿Limpiar toda la orden actual?</span>
          </div>
        }
        description="Esta acción vaciará el carrito y restablecerá el cliente a Consumidor Final."
        className="max-w-md"
      >
        <div className="space-y-4">
          <p className="text-xs text-slate-600">
            Se perderán los productos agregados actualmente ({items.length} productos, {totalUnits} unidades) y la asignación del cliente Odoo seleccionado.
          </p>

          <div className="flex justify-end gap-2 pt-2 border-t border-slate-100">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setShowResetConfirm(false)}
            >
              Cancelar
            </Button>
            <Button
              type="button"
              variant="default"
              size="sm"
              onClick={handleConfirmReset}
              className="bg-slate-900 hover:bg-slate-800 text-white"
            >
              Confirmar y Limpiar
            </Button>
          </div>
        </div>
      </Dialog>

      {/* Modal Dedicado de Selección de Clientes Odoo (Punto 5 y 6) */}
      <CustomerSelectorModal
        open={customerModalOpen}
        onOpenChange={setCustomerModalOpen}
        onSelectCustomer={setSelectedCustomer}
        selectedCustomerId={selectedCustomer?.id}
      />

      {/* Modal Dedicado de Búsqueda de Productos (Puntos 1 y 6) */}
      <ProductSelectorModal
        open={productModalOpen}
        onOpenChange={(open) => {
          setProductModalOpen(open);
          if (!open) setReplacingItem(null);
        }}
        products={products}
        selectedProductIds={items.map((i) => i.productId)}
        onSelectProduct={handleSelectOrReplaceProduct}
        loading={loading}
        replacingProductId={replacingItem?.productId}
        replacingProductName={replacingItem?.productName}
      />

      {/* Modal de Finalización y Cobro POS con Impresión Térmica */}
      <PaymentModal
        isOpen={paymentModalOpen}
        onClose={handleClosePaymentModal}
        total={total}
        onConfirmPayment={handleConfirmPayment}
        processing={processing}
        lastResult={lastResult}
        onSuccessDone={handleSuccessDone}
        selectedCustomer={selectedCustomer}
        onOpenCustomerSelector={() => setCustomerModalOpen(true)}
      />
    </div>
  );
};
