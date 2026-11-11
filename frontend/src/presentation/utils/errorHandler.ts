import { PosApiError } from '@/application/ports/IPosApiGateway';

/**
 * Mensaje estándar y comprensible para el usuario final ante fallos de conexión o caídas de servicio.
 * Cumple con CA-01, RNF-08, RF-17 y P08.
 */
export const USER_FRIENDLY_NETWORK_ERROR =
  'Ha ocurrido un error inesperado al conectar con los servicios. Vuelva a intentarlo. En caso de persistir, contáctese con administración.';

export const USER_FRIENDLY_AUTH_ERROR =
  'Ocurrió un error de autenticación con el servidor Odoo. Por favor intente nuevamente o contacte al administrador.';

/**
 * Determina si un error corresponde a un fallo de red, servidor apagado o excepción técnica de fetch.
 */
export function isNetworkOrFetchError(err: unknown): boolean {
  if (err instanceof TypeError) {
    return true; // En navegadores modernos, fetch() arroja TypeError ante servidores caídos o errores de red
  }
  if (err instanceof Error) {
    const msg = (err.message || '').toLowerCase();
    return (
      msg.includes('fetch') ||
      msg.includes('networkerror') ||
      msg.includes('network error') ||
      msg.includes('load failed') ||
      msg.includes('connection refused') ||
      msg.includes('failed to fetch')
    );
  }
  return false;
}

/**
 * Traduce cualquier excepción a un mensaje de error comprensible y útil para el usuario final (CA-01 / P08).
 * - Si es PosApiError (ProblemDetails): preserva el detalle de negocio específico (ej. sesión cerrada),
 *   salvo que contenga errores técnicos o de API Key / sesión no coincidentes.
 * - Si es un fallo de red o 'Failed to fetch': devuelve el mensaje amigable estándar.
 * - Si es un error genérico no controlado: devuelve el fallback amigable sin tecnicismos en inglés.
 */
export function formatUserErrorMessage(err: unknown, fallbackAction = 'cargar la información'): string {
  if (err instanceof PosApiError) {
    const detail = err.detail || '';
    const title = err.title || '';
    const combined = `${title} ${detail}`.toLowerCase();

    if (err.status === 401) {
      return 'Usuario/correo o contraseña incorrectos. Verifique sus credenciales e intente nuevamente.';
    }

    if (
      combined.includes('session user does not match') ||
      combined.includes('apikey') ||
      combined.includes('api key') ||
      combined.includes('pos.session.search_read') ||
      (err.status === 403 && combined.includes('denegó'))
    ) {
      return USER_FRIENDLY_AUTH_ERROR;
    }

    // Si viene con detalle específico de negocio (RFC 7807), mostrarlo
    return err.detail ? err.detail : `[${err.title}] ${USER_FRIENDLY_NETWORK_ERROR}`;
  }

  if (isNetworkOrFetchError(err)) {
    return USER_FRIENDLY_NETWORK_ERROR;
  }

  if (err instanceof Error && err.message) {
    const msg = err.message;
    const lower = msg.toLowerCase();
    if (
      lower.includes('session user does not match') ||
      lower.includes('apikey') ||
      lower.includes('api key')
    ) {
      return USER_FRIENDLY_AUTH_ERROR;
    }
    if (!lower.includes('fetch')) {
      return msg;
    }
  }

  return `Ha ocurrido un error inesperado al ${fallbackAction}. Vuelva a intentarlo. En caso de persistir, contáctese con administración.`;
}
