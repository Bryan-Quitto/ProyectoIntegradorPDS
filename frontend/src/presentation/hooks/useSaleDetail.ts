import { useState, useCallback } from 'react';
import { SaleOrderDetailDto } from '@/application/ports/IPosApiGateway';
import { getSaleDetailUseCase } from '@/di';

export function useSaleDetail() {
  const [sale, setSale] = useState<SaleOrderDetailDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isOpen, setIsOpen] = useState(false);

  const openSaleDetail = useCallback(async (id: string) => {
    setLoading(true);
    setError(null);
    setIsOpen(true);
    try {
      const data = await getSaleDetailUseCase.execute(id);
      if (!data) {
        setError(`No se encontró la venta con identificador ${id}.`);
      } else {
        setSale(data);
      }
    } catch (err: unknown) {
      if (err instanceof Error) {
        setError(err.message);
      } else {
        setError('Error al obtener el detalle de la venta.');
      }
    } finally {
      setLoading(false);
    }
  }, []);

  const closeSaleDetail = useCallback(() => {
    setIsOpen(false);
    setSale(null);
    setError(null);
  }, []);

  return {
    sale,
    loading,
    error,
    isOpen,
    openSaleDetail,
    closeSaleDetail,
  };
}
