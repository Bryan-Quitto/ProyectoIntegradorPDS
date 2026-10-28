import React from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from './Button';
import { cn } from '../utils/cn';

export interface PaginationProps {
  currentPage: number;
  totalPages: number;
  totalCount: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  className?: string;
}

export const Pagination: React.FC<PaginationProps> = ({
  currentPage,
  totalPages,
  totalCount,
  pageSize,
  onPageChange,
  className,
}) => {
  if (totalCount === 0) return null;

  const startItem = Math.min((currentPage - 1) * pageSize + 1, totalCount);
  const endItem = Math.min(currentPage * pageSize, totalCount);

  return (
    <div
      className={cn(
        'flex flex-col sm:flex-row items-center justify-between gap-3 px-4 py-3 bg-white border-t border-slate-200 text-xs text-slate-600',
        className
      )}
    >
      <div>
        Mostrando <span className="font-semibold text-slate-900">{startItem}</span> -{' '}
        <span className="font-semibold text-slate-900">{endItem}</span> de{' '}
        <span className="font-semibold text-slate-900">{totalCount}</span> registros
      </div>

      <div className="flex items-center space-x-1.5">
        <Button
          variant="outline"
          size="sm"
          onClick={() => onPageChange(currentPage - 1)}
          disabled={currentPage <= 1}
          className="h-8 px-2.5 text-xs"
          title="Página anterior"
        >
          <ChevronLeft className="h-3.5 w-3.5 mr-1" />
          <span>Anterior</span>
        </Button>

        <span className="px-3 py-1 text-xs font-medium text-slate-700 bg-slate-50 border border-slate-200 rounded-lg">
          Página {currentPage} de {Math.max(totalPages, 1)}
        </span>

        <Button
          variant="outline"
          size="sm"
          onClick={() => onPageChange(currentPage + 1)}
          disabled={currentPage >= totalPages}
          className="h-8 px-2.5 text-xs"
          title="Página siguiente"
        >
          <span>Siguiente</span>
          <ChevronRight className="h-3.5 w-3.5 ml-1" />
        </Button>
      </div>
    </div>
  );
};
