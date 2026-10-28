import { useState, useCallback, useMemo } from 'react';
import { Cart } from '@/domain/Cart';
import { CheckoutSaleUseCase, CheckoutResult } from '@/application/useCases/UseCases';
import { PosApiHttpAdapter } from '@/infrastructure/http/PosApiHttpAdapter';

export function useCheckout() {
  const [processing, setProcessing] = useState<boolean>(false);
  const [lastResult, setLastResult] = useState<CheckoutResult | null>(null);

  const gateway = useMemo(() => new PosApiHttpAdapter(), []);
  const checkoutUseCase = useMemo(() => new CheckoutSaleUseCase(gateway), [gateway]);

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
        const result = await checkoutUseCase.execute({
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
    [checkoutUseCase]
  );

  return {
    checkout,
    processing,
    lastResult,
  };
}
