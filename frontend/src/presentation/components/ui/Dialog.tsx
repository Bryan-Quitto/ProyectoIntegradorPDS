import React, { useEffect } from 'react';
import { createPortal } from 'react-dom';
import { X } from 'lucide-react';
import { cn } from '../utils/cn';
import { Button } from './Button';

export interface DialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title?: React.ReactNode;
  description?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
}

export const Dialog: React.FC<DialogProps> = ({
  open,
  onOpenChange,
  title,
  description,
  children,
  className,
}) => {
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onOpenChange(false);
      }
    };

    if (open) {
      document.body.style.overflow = 'hidden';
      window.addEventListener('keydown', handleKeyDown);
    }

    return () => {
      document.body.style.overflow = '';
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [open, onOpenChange]);

  if (!open) return null;

  return createPortal(
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6 overflow-hidden print:static print:p-0 print:overflow-visible print:z-auto print:block">
      {/* Backdrop: Estático, NO se cierra al hacer clic fuera y se oculta en impresión */}
      <div
        className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm transition-opacity no-print"
        aria-hidden="true"
      />

      {/* Content: Con altura máxima en pantalla y estático limpio en impresión */}
      <div
        role="dialog"
        aria-modal="true"
        className={cn(
          'relative z-50 w-full max-w-lg max-h-[90vh] flex flex-col rounded-xl bg-white p-6 shadow-xl border border-slate-200 transition-all',
          'animate-in fade-in zoom-in-95 duration-200',
          'print:static print:max-h-none print:shadow-none print:border-none print:p-0 print:w-full print:block',
          className
        )}
      >
        {/* Cabecera del modal: con separación limpia respecto al contenido */}
        <div className="flex items-start justify-between mb-3 shrink-0 border-b border-slate-200 pb-3.5 no-print">
          <div>
            {title && <h2 className="text-lg font-bold text-slate-900 leading-tight">{title}</h2>}
            {description && <p className="text-xs sm:text-sm text-slate-500 mt-1">{description}</p>}
          </div>
          <Button
            variant="ghost"
            size="icon"
            onClick={() => onOpenChange(false)}
            aria-label="Cerrar modal"
            className="text-slate-400 hover:text-slate-600 -mr-2 -mt-2 shrink-0"
          >
            <X className="h-4 w-4" />
          </Button>
        </div>

        {/* Scrollable body content con padding superior adecuado para evitar cortes de foco o solapamientos */}
        <div className="flex-1 overflow-y-auto pt-2.5 pb-1 px-0.5 space-y-4 print:overflow-visible print:p-0">
          {children}
        </div>
      </div>
    </div>,
    document.body
  );
};
