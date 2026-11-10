import { useState, useEffect, useCallback } from 'react';
import { Customer } from '@/application/ports/IPosApiGateway';
import { searchCustomersUseCase } from '@/di';

export function useCustomers(initialQuery = '') {
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const fetchCustomers = useCallback(async (query = '') => {
    setLoading(true);
    setError(null);
    try {
      const data = await searchCustomersUseCase.execute(query);
      setCustomers(data);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Error al cargar clientes desde Odoo.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchCustomers(initialQuery);
  }, [fetchCustomers, initialQuery]);

  return {
    customers,
    loading,
    error,
    refresh: fetchCustomers,
  };
}
