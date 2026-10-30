import { useState, useEffect, useCallback } from 'react';
import { Product } from '@/domain/Product';
import { PosSession } from '@/application/ports/IPosApiGateway';
import { searchCatalogUseCase, getActiveSessionUseCase } from '@/di';

export function useProducts() {
  const [products, setProducts] = useState<Product[]>([]);
  const [activeSession, setActiveSession] = useState<PosSession | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const loadData = useCallback(async (query = '') => {
    setLoading(true);
    setError(null);
    try {
      const [prods, session] = await Promise.all([
        searchCatalogUseCase.execute(query),
        getActiveSessionUseCase.execute(),
      ]);
      setProducts(prods);
      setActiveSession(session);
    } catch (err: unknown) {
      if (err instanceof Error) {
        setError(err.message);
      } else {
        setError('Error desconocido cargando datos de Odoo.');
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  return {
    products,
    activeSession,
    loading,
    error,
    refresh: loadData,
  };
}
