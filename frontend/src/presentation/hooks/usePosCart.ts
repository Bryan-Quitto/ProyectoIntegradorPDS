import { useState, useMemo, useCallback } from 'react';
import { Cart } from '@/domain/Cart';
import { Product } from '@/domain/Product';
import { DiscountType } from '@/domain/CartItem';
import { ManageCartUseCase } from '@/application/useCases/UseCases';

export function usePosCart() {
  const [cartInstance] = useState(() => new Cart());
  const [version, setVersion] = useState(0);

  const cartUseCase = useMemo(() => new ManageCartUseCase(), []);

  const triggerUpdate = useCallback(() => {
    setVersion((v) => v + 1);
  }, []);

  const addItem = useCallback((product: Product, quantity = 1) => {
    cartUseCase.addItem(cartInstance, product, quantity);
    triggerUpdate();
  }, [cartInstance, cartUseCase, triggerUpdate]);

  const updateQuantity = useCallback((productId: number, quantity: number) => {
    cartUseCase.updateQuantity(cartInstance, productId, quantity);
    triggerUpdate();
  }, [cartInstance, cartUseCase, triggerUpdate]);

  const removeItem = useCallback((productId: number) => {
    cartUseCase.removeItem(cartInstance, productId);
    triggerUpdate();
  }, [cartInstance, cartUseCase, triggerUpdate]);

  const clearCart = useCallback(() => {
    cartUseCase.clearCart(cartInstance);
    triggerUpdate();
  }, [cartInstance, cartUseCase, triggerUpdate]);

  const applyDiscount = useCallback((type: DiscountType, value: number) => {
    cartUseCase.applyDiscount(cartInstance, type, value);
    triggerUpdate();
  }, [cartInstance, cartUseCase, triggerUpdate]);

  // Derived state (recalculated when version changes)
  const cartSummary = useMemo(() => {
    // eslint-disable-next-line @typescript-eslint/no-unused-expressions
    version; // dependency to trigger recalculation
    return {
      rawCart: cartInstance,
      items: cartInstance.items,
      subtotal: cartInstance.subtotal,
      discountAmount: cartInstance.discountAmount,
      netSubtotal: cartInstance.netSubtotal,
      taxAmount: cartInstance.taxAmount,
      total: cartInstance.total,
      discountPolicy: cartInstance.discountPolicy,
    };
  }, [cartInstance, version]);

  return {
    ...cartSummary,
    addItem,
    updateQuantity,
    removeItem,
    clearCart,
    applyDiscount,
  };
}
