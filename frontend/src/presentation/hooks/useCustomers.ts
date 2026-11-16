import { useState, useEffect, useCallback } from 'react';
import { Customer } from '@/application/ports/IPosApiGateway';
import { searchCustomersUseCase } from '@/di';

export function useCustomers(initialQuery = '', page = 1, pageSize = 8, criteria?: string) {
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const fetchCustomers = useCallback(async (query = initialQuery, p = page, ps = pageSize, crit = criteria) => {
    setLoading(true);
    setError(null);
    try {
      const data = await searchCustomersUseCase.execute(query, p, ps, crit);
      setCustomers(data);
      const count = (data as any)?.totalCount ?? data.length;
      setTotalCount(count);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Error al cargar clientes desde Odoo.');
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, initialQuery, criteria]);

  useEffect(() => {
    fetchCustomers(initialQuery, page, pageSize, criteria);
  }, [fetchCustomers, initialQuery, page, pageSize, criteria]);

  return {
    customers,
    totalCount,
    loading,
    error,
    refresh: fetchCustomers,
  };
}
