import { useState, useEffect, useCallback, useRef } from 'react';
import { Product } from '@/domain/Product';
import { PosSession } from '@/application/ports/IPosApiGateway';
import { searchCatalogUseCase, getActiveSessionUseCase } from '@/di';
import { formatUserErrorMessage, isAbortError } from '../utils/errorHandler';

export function useProducts(
  initialQuery = '',
  page?: number,
  pageSize?: number,
  criteria?: string,
  onlyInStock?: boolean,
  enabled = true,
  fetchSession = page === undefined
) {
  const [products, setProducts] = useState<Product[]>([]);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [activeSession, setActiveSession] = useState<PosSession | null>(null);
  const [loading, setLoading] = useState<boolean>(enabled);
  const [error, setError] = useState<string | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);
  const latestRequestRef = useRef<number>(0);
  const activeSessionRef = useRef<PosSession | null>(null);

  const refreshSession = useCallback(async (): Promise<PosSession | null> => {
    try {
      const session = await getActiveSessionUseCase.execute();
      activeSessionRef.current = session;
      setActiveSession(session);
      return session;
    } catch {
      return activeSessionRef.current;
    }
  }, []);

  useEffect(() => {
    if (!fetchSession) return;
    void refreshSession();
  }, [fetchSession, refreshSession]);

  const loadData = useCallback(
    async (
      query = initialQuery,
      p = page,
      ps = pageSize,
      crit = criteria,
      inStock = onlyInStock
    ): Promise<{ products: Product[]; session: PosSession | null; totalCount: number } | null> => {
      abortControllerRef.current?.abort();
      const controller = new AbortController();
      abortControllerRef.current = controller;
      const requestId = ++latestRequestRef.current;

      setLoading(true);
      setError(null);
      try {
        const prods = await searchCatalogUseCase.execute(query, p, ps, crit, inStock, controller.signal);
        if (controller.signal.aborted || requestId !== latestRequestRef.current) {
          return null;
        }
        setProducts(prods);
        const count = (prods as any)?.totalCount ?? prods.length;
        setTotalCount(count);
        return { products: prods, session: activeSessionRef.current, totalCount: count };
      } catch (err: unknown) {
        if (controller.signal.aborted || isAbortError(err) || requestId !== latestRequestRef.current) {
          return null;
        }
        setProducts([]);
        setTotalCount(0);
        setError(formatUserErrorMessage(err, 'cargar el catálogo de productos'));
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
    [initialQuery, page, pageSize, criteria, onlyInStock]
  );

  useEffect(() => {
    if (!enabled) {
      abortControllerRef.current?.abort();
      setLoading(false);
      return;
    }
    void loadData(initialQuery, page, pageSize, criteria, onlyInStock);
    return () => {
      abortControllerRef.current?.abort();
    };
  }, [enabled, loadData, initialQuery, page, pageSize, criteria, onlyInStock]);

  return {
    products,
    totalCount,
    activeSession,
    loading,
    error,
    refresh: loadData,
    refreshSession,
  };
}
