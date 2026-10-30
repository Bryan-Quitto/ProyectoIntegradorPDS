import { useState, useCallback } from 'react';
import { Cart } from '@/domain/Cart';
import { CheckoutResult } from '@/application/useCases/UseCases';
import { checkoutSaleUseCase } from '@/di';

export function useCheckout() {
  const [processing, setProcessing] = useState<boolean>(false);
  const [lastResult, setLastResult] = useState<CheckoutResult | null>(null);

  const checkout = useCallback(
    async (
      cart: Cart,
      posSessionId: number,
      customerDocument?: string,
      customerName?: string
    ): Promise<CheckoutResult> => {
      setProcessing(true);
      setLastResult(null);

      try {
        const result = await checkoutSaleUseCase.execute({
          cart,
          posSessionId,
          customerDocument,
          customerName,
        });
        setLastResult(result);
        return result;
      } finally {
        setProcessing(false);
      }
    },
    []
  );

  return {
    checkout,
    processing,
    lastResult,
  };
}
