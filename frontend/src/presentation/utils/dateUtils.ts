/**
 * Utilidades de fecha y hora para el POS.
 * Convierte timestamps UTC recibidos del backend a la zona horaria oficial del proyecto:
 * Ecuador Continental (America/Guayaquil - UTC-5).
 */

export const TIMEZONE_EC = 'America/Guayaquil';

/**
 * Parsea una cadena de fecha asegurando que sea interpretada en tiempo UTC,
 * incluso si el backend omitió el sufijo 'Z'.
 */
export function parseUtcDate(dateStr?: string | null): Date {
  if (!dateStr) return new Date();
  const normalized = dateStr.endsWith('Z') || dateStr.includes('+') ? dateStr : `${dateStr}Z`;
  const parsed = new Date(normalized);
  return isNaN(parsed.getTime()) ? new Date() : parsed;
}

/**
 * Formatea solo la fecha en zona horaria UTC-5 (ej: "29 oct 2026").
 */
export function formatEcDate(dateStr?: string | null, options?: Intl.DateTimeFormatOptions): string {
  const date = parseUtcDate(dateStr);
  return date.toLocaleDateString('es-EC', {
    timeZone: TIMEZONE_EC,
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    ...options,
  });
}

/**
 * Formatea solo la hora en zona horaria UTC-5 con formato am/pm (ej: "03:40:07 p. m.").
 */
export function formatEcTime(dateStr?: string | null, options?: Intl.DateTimeFormatOptions): string {
  const date = parseUtcDate(dateStr);
  return date.toLocaleTimeString('es-EC', {
    timeZone: TIMEZONE_EC,
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: true,
    ...options,
  });
}

/**
 * Formatea fecha y hora combinadas en zona horaria UTC-5 (ej: "29/10/2026, 15:40").
 */
export function formatEcDateTime(dateStr?: string | null, options?: Intl.DateTimeFormatOptions): string {
  const date = parseUtcDate(dateStr);
  return date.toLocaleString('es-EC', {
    timeZone: TIMEZONE_EC,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: true,
    ...options,
  });
}
