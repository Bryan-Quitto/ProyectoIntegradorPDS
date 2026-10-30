import { useState, useMemo, useCallback } from 'react';
import { Cart } from '@/domain/Cart';
import { Product } from '@/domain/Product';
import { DiscountType } from '@/domain/CartItem';
import { manageCartUseCase } from '@/di';

export function usePosCart() {
  const [cartInstance] = useState(() => new Cart());
  const [version, setVersion] = useState(0);

  const triggerUpdate = useCallback(() => {
    setVersion((v) => v + 1);
  }, []);

  const addItem = useCallback((product: Product, quantity = 1) => {
    manageCartUseCase.addItem(cartInstance, product, quantity);
    triggerUpdate();
  }, [cartInstance, triggerUpdate]);

  const updateQuantity = useCallback((productId: number, quantity: number) => {
    manageCartUseCase.updateQuantity(cartInstance, productId, quantity);
    triggerUpdate();
  }, [cartInstance, triggerUpdate]);

  const removeItem = useCallback((productId: number) => {
    manageCartUseCase.removeItem(cartInstance, productId);
    triggerUpdate();
  }, [cartInstance, triggerUpdate]);

  const clearCart = useCallback(() => {
    manageCartUseCase.clearCart(cartInstance);
    triggerUpdate();
  }, [cartInstance, triggerUpdate]);

  const applyDiscount = useCallback((type: DiscountType, value: number) => {
    manageCartUseCase.applyDiscount(cartInstance, type, value);
    triggerUpdate();
  }, [cartInstance, triggerUpdate]);

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
