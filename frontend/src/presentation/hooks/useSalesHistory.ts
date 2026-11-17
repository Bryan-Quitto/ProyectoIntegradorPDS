import { useState, useEffect, useCallback, useRef } from 'react';
import { SaleOrderResult } from '@/application/ports/IPosApiGateway';
import { getSalesHistoryUseCase } from '@/di';
import { formatUserErrorMessage } from '../utils/errorHandler';

function isAbortError(err: unknown): boolean {
  return (
    (err instanceof Error && err.name === 'AbortError') ||
    (typeof DOMException !== 'undefined' && err instanceof DOMException && err.name === 'AbortError')
  );
}

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
  const abortControllerRef = useRef<AbortController | null>(null);

  const loadHistory = useCallback(async () => {
    abortControllerRef.current?.abort();
    const controller = new AbortController();
    abortControllerRef.current = controller;

    setLoading(true);
    setError(null);
    try {
      const data = await getSalesHistoryUseCase.execute(
        page,
        pageSize,
        search,
        filter,
        controller.signal
      );
      if (controller.signal.aborted) {
        return;
      }
      setSales(data);
      const count = (data as any)?.totalCount ?? data.length;
      setTotalCount(count);
    } catch (err: unknown) {
      if (controller.signal.aborted || isAbortError(err)) {
        return;
      }
      setError(formatUserErrorMessage(err, 'cargar el historial de ventas'));
    } finally {
      if (abortControllerRef.current === controller && !controller.signal.aborted) {
        setLoading(false);
      }
    }
  }, [page, pageSize, search, filter]);

  useEffect(() => {
    void loadHistory();
    return () => {
      abortControllerRef.current?.abort();
    };
  }, [loadHistory]);

  return {
    sales,
    totalCount,
    loading,
    error,
    refresh: loadHistory,
  };
}
