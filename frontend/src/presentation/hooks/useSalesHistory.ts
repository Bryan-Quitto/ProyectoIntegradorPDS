import { useState, useEffect, useCallback, useMemo } from 'react';
import { SaleOrderResult } from '@/application/ports/IPosApiGateway';
import { GetSalesHistoryUseCase } from '@/application/useCases/UseCases';
import { PosApiHttpAdapter } from '@/infrastructure/http/PosApiHttpAdapter';

export function useSalesHistory() {
  const [sales, setSales] = useState<SaleOrderResult[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const gateway = useMemo(() => new PosApiHttpAdapter(), []);
  const useCase = useMemo(() => new GetSalesHistoryUseCase(gateway), [gateway]);

  const loadHistory = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await useCase.execute();
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
  }, [useCase]);

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
