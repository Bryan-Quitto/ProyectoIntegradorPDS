import { useState, useEffect, useCallback, useMemo } from 'react';
import { Product } from '@/domain/Product';
import { PosSession } from '@/application/ports/IPosApiGateway';
import { SearchCatalogUseCase } from '@/application/useCases/UseCases';
import { PosApiHttpAdapter } from '@/infrastructure/http/PosApiHttpAdapter';

export function useProducts() {
  const [products, setProducts] = useState<Product[]>([]);
  const [activeSession, setActiveSession] = useState<PosSession | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const gateway = useMemo(() => new PosApiHttpAdapter(), []);
  const searchUseCase = useMemo(() => new SearchCatalogUseCase(), []);

  const loadData = useCallback(async (query = '') => {
    setLoading(true);
    setError(null);
    try {
      const [prods, session] = await Promise.all([
        searchUseCase.execute(gateway, query),
        gateway.getActiveSession(),
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
  }, [gateway, searchUseCase]);

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
