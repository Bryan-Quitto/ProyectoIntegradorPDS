import { useState, useCallback, useRef } from 'react';
import { Cart } from '@/domain/Cart';
import { CheckoutResult } from '@/application/useCases/UseCases';
import { checkoutSaleUseCase } from '@/di';

export function useCheckout() {
  const [processing, setProcessing] = useState<boolean>(false);
  const [lastResult, setLastResult] = useState<CheckoutResult | null>(null);

  // [GUÍA ESTEBAN - FIX AUDITORÍA ELI - RN-08 / RF-16]:
  // Almacenamos la clave de idempotencia activa en una referencia mutable.
  // Error detectado en el código previo de Eli:
  // No se suministraba 'idempotencyKey' desde el hook, lo que provocaba que en cada
  // reintento tras un fallo o timeout de red se generara un nuevo UUID aleatorio.
  // Esto puenteaba (bypass) el Idempotent Receiver de SQLite en backend y causaba
  // que Odoo recibiera múltiples órdenes idénticas.
  // Con este fix, la clave se preserva intacta durante los reintentos y solo se
  // renueva/limpia tras una confirmación exitosa o cancelación manual.
  const activeIdempotencyKeyRef = useRef<string | null>(null);

  const checkout = useCallback(
    async (
      cart: Cart,
      posSessionId: number,
      customerDocument?: string,
      customerName?: string
    ): Promise<CheckoutResult> => {
      setProcessing(true);
      setLastResult(null);

      // Si no hay una clave activa para esta transacción en curso, generamos una
      if (!activeIdempotencyKeyRef.current) {
        activeIdempotencyKeyRef.current =
          typeof crypto !== 'undefined' && crypto.randomUUID
            ? crypto.randomUUID()
            : `pos-key-${Date.now()}`;
      }

      try {
        const result = await checkoutSaleUseCase.execute({
          cart,
          posSessionId,
          customerDocument,
          customerName,
          idempotencyKey: activeIdempotencyKeyRef.current,
        });

        setLastResult(result);

        // Si la venta se confirmó exitosamente en Odoo, reseteamos la clave para la siguiente venta
        if (result.success) {
          activeIdempotencyKeyRef.current = null;
        }

        return result;
      } finally {
        setProcessing(false);
      }
    },
    []
  );

  const resetCheckoutSession = useCallback(() => {
    activeIdempotencyKeyRef.current = null;
    setLastResult(null);
  }, []);

  return {
    checkout,
    processing,
    lastResult,
    resetCheckoutSession,
  };
}
