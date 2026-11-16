import { useState, useEffect, useCallback } from 'react';
import { Product } from '@/domain/Product';
import { PosSession } from '@/application/ports/IPosApiGateway';
import { searchCatalogUseCase, getActiveSessionUseCase } from '@/di';
import { formatUserErrorMessage } from '../utils/errorHandler';

export function useProducts(initialQuery = '', page?: number, pageSize?: number, criteria?: string) {
  const [products, setProducts] = useState<Product[]>([]);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [activeSession, setActiveSession] = useState<PosSession | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const loadData = useCallback(async (query = initialQuery, p = page, ps = pageSize, crit = criteria): Promise<{ products: Product[]; session: PosSession | null; totalCount: number } | null> => {
    setLoading(true);
    setError(null);
    try {
      const [prods, session] = await Promise.all([
        searchCatalogUseCase.execute(query, p, ps, crit),
        getActiveSessionUseCase.execute(),
      ]);
      setProducts(prods);
      const count = (prods as any)?.totalCount ?? prods.length;
      setTotalCount(count);
      setActiveSession(session);
      return { products: prods, session, totalCount: count };
    } catch (err: unknown) {
      setError(formatUserErrorMessage(err, 'cargar el catálogo de productos'));
      return null;
    } finally {
      setLoading(false);
    }
  }, [initialQuery, page, pageSize, criteria]);

  useEffect(() => {
    loadData(initialQuery, page, pageSize, criteria);
  }, [loadData, initialQuery, page, pageSize, criteria]);

  return {
    products,
    totalCount,
    activeSession,
    loading,
    error,
    refresh: loadData,
  };
}
