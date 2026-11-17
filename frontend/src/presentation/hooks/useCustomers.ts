import { useState, useEffect, useCallback, useRef } from 'react';
import { Customer } from '@/application/ports/IPosApiGateway';
import { searchCustomersUseCase } from '@/di';
import { formatUserErrorMessage, isAbortError } from '../utils/errorHandler';

export function useCustomers(
  initialQuery = '',
  page = 1,
  pageSize = 8,
  criteria?: string,
  enabled = true
) {
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [loading, setLoading] = useState<boolean>(enabled);
  const [error, setError] = useState<string | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);
  const latestRequestRef = useRef<number>(0);

  const fetchCustomers = useCallback(
    async (
      query = initialQuery,
      p = page,
      ps = pageSize,
      crit = criteria
    ): Promise<Customer[] | null> => {
      abortControllerRef.current?.abort();
      const controller = new AbortController();
      abortControllerRef.current = controller;
      const requestId = ++latestRequestRef.current;

      setLoading(true);
      setError(null);
      try {
        const data = await searchCustomersUseCase.execute(query, p, ps, crit, controller.signal);
        if (controller.signal.aborted || requestId !== latestRequestRef.current) {
          return null;
        }
        setCustomers(data);
        const count = (data as any)?.totalCount ?? data.length;
        setTotalCount(count);
        return data;
      } catch (err: unknown) {
        if (controller.signal.aborted || isAbortError(err) || requestId !== latestRequestRef.current) {
          return null;
        }
        setCustomers([]);
        setTotalCount(0);
        setError(formatUserErrorMessage(err, 'cargar los clientes de Odoo'));
        return null;
      } finally {
        if (
          abortControllerRef.current === controller &&
          !controller.signal.aborted &&
          requestId === latestRequestRef.current
        ) {
          setLoading(false);
        }
      }
    },
    [page, pageSize, initialQuery, criteria]
  );

  useEffect(() => {
    if (!enabled) {
      abortControllerRef.current?.abort();
      setLoading(false);
      return;
    }
    void fetchCustomers(initialQuery, page, pageSize, criteria);
    return () => {
      abortControllerRef.current?.abort();
    };
  }, [enabled, fetchCustomers, initialQuery, page, pageSize, criteria]);

  return {
    customers,
    totalCount,
    loading,
    error,
    refresh: fetchCustomers,
  };
}
