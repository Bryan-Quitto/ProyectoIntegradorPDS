import { useState, useEffect, useCallback } from 'react';
import { Product } from '@/domain/Product';
import { PosSession } from '@/application/ports/IPosApiGateway';
import { searchCatalogUseCase, getActiveSessionUseCase } from '@/di';
import { formatUserErrorMessage } from '../utils/errorHandler';

export function useProducts() {
  const [products, setProducts] = useState<Product[]>([]);
  const [activeSession, setActiveSession] = useState<PosSession | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const loadData = useCallback(async (query = ''): Promise<{ products: Product[]; session: PosSession | null } | null> => {
    setLoading(true);
    setError(null);
    try {
      const [prods, session] = await Promise.all([
        searchCatalogUseCase.execute(query),
        getActiveSessionUseCase.execute(),
      ]);
      setProducts(prods);
      setActiveSession(session);
      return { products: prods, session };
    } catch (err: unknown) {
      setError(formatUserErrorMessage(err, 'cargar el catálogo de productos'));
      return null;
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
