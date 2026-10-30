import { useState, useEffect, useCallback } from 'react';
import { SaleOrderResult } from '@/application/ports/IPosApiGateway';
import { getSalesHistoryUseCase } from '@/di';

export function useSalesHistory() {
  const [sales, setSales] = useState<SaleOrderResult[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadHistory = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getSalesHistoryUseCase.execute();
      setSales(data);
    } catch (err: unknown) {
      if (err instanceof Error) {
        setError(err.message);
      } else {
        setError('Error al cargar historial de ventas.');
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadHistory();
  }, [loadHistory]);

  return {
    sales,
    loading,
    error,
    refresh: loadHistory,
  };
}
