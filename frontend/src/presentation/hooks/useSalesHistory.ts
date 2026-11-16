import { useState, useEffect, useCallback } from 'react';
import { SaleOrderResult } from '@/application/ports/IPosApiGateway';
import { getSalesHistoryUseCase } from '@/di';
import { formatUserErrorMessage } from '../utils/errorHandler';

export function useSalesHistory(
  page: number = 1,
  pageSize: number = 5,
  search: string = '',
  filter: string = 'ALL'
) {
  const [sales, setSales] = useState<SaleOrderResult[]>([]);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadHistory = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getSalesHistoryUseCase.execute(page, pageSize, search, filter);
      setSales(data);
      const count = (data as any)?.totalCount ?? data.length;
      setTotalCount(count);
    } catch (err: unknown) {
      setError(formatUserErrorMessage(err, 'cargar el historial de ventas'));
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, search, filter]);

  useEffect(() => {
    loadHistory();
  }, [loadHistory]);

  return {
    sales,
    totalCount,
    loading,
    error,
    refresh: loadHistory,
  };
}
