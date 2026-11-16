import React, { useState } from 'react';
import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from 'lucide-react';
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

  const [jumpPage, setJumpPage] = useState('');

  const startItem = Math.min((currentPage - 1) * pageSize + 1, totalCount);
  const endItem = Math.min(currentPage * pageSize, totalCount);

  const handleJump = (e: React.FormEvent) => {
    e.preventDefault();
    const p = parseInt(jumpPage, 10);
    if (!isNaN(p) && p >= 1 && p <= totalPages) {
      onPageChange(p);
      setJumpPage('');
    }
  };

  return (
    <div
      className={cn(
        'flex flex-col sm:flex-row items-center justify-between gap-3 px-4 py-3 bg-white border-t border-slate-200 text-xs text-slate-600',
        className
      )}
    >
      <div>
        Mostrando <span className="font-semibold text-slate-900">{startItem.toLocaleString()}</span> -{' '}
        <span className="font-semibold text-slate-900">{endItem.toLocaleString()}</span> de{' '}
        <span className="font-semibold text-slate-900">{totalCount.toLocaleString()}</span> registros
      </div>

      <div className="flex flex-wrap items-center gap-1.5">
        <Button
          variant="outline"
          size="sm"
          onClick={() => onPageChange(1)}
          disabled={currentPage <= 1}
          className="h-8 px-2 text-xs"
          title="Primera página"
        >
          <ChevronsLeft className="h-3.5 w-3.5" />
          <span className="hidden sm:inline ml-1">Primera</span>
        </Button>

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

        <span className="px-3 py-1 text-xs font-medium text-slate-700 bg-slate-50 border border-slate-200 rounded-lg whitespace-nowrap">
          Página {currentPage.toLocaleString()} de {Math.max(totalPages, 1).toLocaleString()}
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

        <Button
          variant="outline"
          size="sm"
          onClick={() => onPageChange(totalPages)}
          disabled={currentPage >= totalPages}
          className="h-8 px-2 text-xs"
          title="Última página"
        >
          <span className="hidden sm:inline mr-1">Última</span>
          <ChevronsRight className="h-3.5 w-3.5" />
        </Button>

        {totalPages > 5 && (
          <form onSubmit={handleJump} className="flex items-center gap-1 ml-1 sm:ml-2">
            <span className="text-[11px] text-slate-400">Ir:</span>
            <input
              type="number"
              min={1}
              max={totalPages}
              value={jumpPage}
              onChange={(e) => setJumpPage(e.target.value)}
              placeholder="N°"
              className="w-16 h-8 text-xs border border-slate-200 rounded px-1.5 text-center focus:outline-none focus:ring-1 focus:ring-emerald-500"
              aria-label="Número de página a saltar"
            />
            <Button type="submit" variant="ghost" size="sm" className="h-8 px-2 text-xs">
              Ir
            </Button>
          </form>
        )}
      </div>
    </div>
  );
};
