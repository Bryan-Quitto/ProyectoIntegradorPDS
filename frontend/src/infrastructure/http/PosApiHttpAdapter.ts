import { Product } from '@/domain/Product';
import {
  IPosApiGateway,
  LoginRequest,
  LoginResponse,
  PosSession,
  Customer,
  CreateSaleRequest,
  SaleOrderResult,
  SaleOrderDetailDto,
  PosApiError,
} from '@/application/ports/IPosApiGateway';

export class PosApiHttpAdapter implements IPosApiGateway {
  private readonly _baseUrl: string;

  constructor(baseUrl?: string) {
    this._baseUrl = (baseUrl || import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000').replace(/\/$/, '');
  }

  private getAuthHeader(): Record<string, string> {
    if (typeof localStorage !== 'undefined') {
      let auth = localStorage.getItem('auth_token');
      if (!auth) {
        const tokensRaw = localStorage.getItem('tokens');
        if (tokensRaw) {
          try {
            const parsed = JSON.parse(tokensRaw);
            auth = parsed?.accessToken || parsed?.token || (typeof parsed === 'string' ? parsed : null);
          } catch {
            auth = tokensRaw;
          }
        }
      }
      if (auth) {
        return { Authorization: `Bearer ${auth}` };
      }
    }
    return {};
  }

  private async handleResponse<T>(response: Response): Promise<T> {
    if (!response.ok) {
      let title = 'Error en la petición';
      let detail = `Error HTTP ${response.status}: ${response.statusText}`;

      try {
        const errorJson = await response.json();
        title = errorJson.title || errorJson.error || title;
        detail = errorJson.detail || errorJson.message || detail;
      } catch {
        // Fallback to text if not JSON
        const text = await response.text();
        if (text) detail = text;
      }

      throw new PosApiError(response.status, title, detail);
    }

    if (response.status === 204) {
      return {} as T;
    }

    return (await response.json()) as T;
  }

  private async safeFetch(url: string | URL, init?: RequestInit): Promise<Response> {
    try {
      return await fetch(url.toString(), init);
    } catch (err: unknown) {
      if (
        (err instanceof Error && err.name === 'AbortError') ||
        (typeof DOMException !== 'undefined' && err instanceof DOMException && err.name === 'AbortError')
      ) {
        throw err;
      }
      if (
        err instanceof TypeError ||
        (err instanceof Error &&
          (err.message.toLowerCase().includes('fetch') ||
            err.message.toLowerCase().includes('network') ||
            err.message.toLowerCase().includes('load failed') ||
            err.message.toLowerCase().includes('connection refused')))
      ) {
        throw new PosApiError(
          503,
          'Servicio No Disponible',
          'Ha ocurrido un error inesperado al conectar con los servicios. Vuelva a intentarlo. En caso de persistir, contáctese con administración.'
        );
      }
      throw err;
    }
  }

  public async login(request: LoginRequest): Promise<LoginResponse> {
    const url = `${this._baseUrl}/api/auth/login`;
    const response = await this.safeFetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
      },
      body: JSON.stringify(request),
    });

    return await this.handleResponse<LoginResponse>(response);
  }

  public async getProducts(
    search?: string,
    page?: number,
    pageSize?: number,
    criteria?: string,
    onlyInStock?: boolean,
    signal?: AbortSignal
  ): Promise<Product[]> {
    const url = new URL(`${this._baseUrl}/api/products`);
    if (search?.trim()) {
      url.searchParams.set('search', search.trim());
    }
    if (page) {
      url.searchParams.set('page', String(page));
    }
    if (pageSize) {
      url.searchParams.set('pageSize', String(pageSize));
    }
    if (criteria?.trim()) {
      url.searchParams.set('criteria', criteria.trim());
    }
    if (onlyInStock) {
      url.searchParams.set('onlyInStock', 'true');
    }

    const response = await this.safeFetch(url.toString(), {
      method: 'GET',
      headers: {
        Accept: 'application/json',
        ...this.getAuthHeader(),
      },
      ...(signal ? { signal } : {}),
    });

    const totalCountHeader = response.headers.get('x-total-count') || response.headers.get('X-Total-Count');
    const raw = await this.handleResponse<any[]>(response);
    const result = raw.map((p) => ({
      id: p.id,
      name: p.name,
      price: p.price,
      barcode: p.barcode ?? '',
      availableQuantity: p.availableQuantity ?? 0,
      defaultCode: p.defaultCode || p.default_code || '',
    }));
    if (totalCountHeader) {
      const parsed = parseInt(totalCountHeader, 10);
      if (!isNaN(parsed)) {
        (result as any).totalCount = parsed;
      }
    }
    return result;
  }

  public async getProductsByIds(ids: number[]): Promise<Product[]> {
    if (!ids || ids.length === 0) {
      return [];
    }
    const distinctIds = Array.from(new Set(ids));
    const url = new URL(`${this._baseUrl}/api/products`);
    url.searchParams.set('ids', distinctIds.join(','));

    const response = await this.safeFetch(url.toString(), {
      method: 'GET',
      headers: {
        Accept: 'application/json',
        ...this.getAuthHeader(),
      },
    });

    const raw = await this.handleResponse<any[]>(response);
    return raw.map((p) => ({
      id: p.id,
      name: p.name,
      price: p.price,
      barcode: p.barcode ?? '',
      availableQuantity: p.availableQuantity ?? 0,
      defaultCode: p.defaultCode || p.default_code || '',
    }));
  }

  public async getCustomers(
    search?: string,
    page?: number,
    pageSize?: number,
    criteria?: string,
    signal?: AbortSignal
  ): Promise<Customer[]> {
    const url = new URL(`${this._baseUrl}/api/customers`);
    if (search?.trim()) {
      url.searchParams.set('search', search.trim());
    }
    if (page) {
      url.searchParams.set('page', String(page));
    }
    if (pageSize) {
      url.searchParams.set('pageSize', String(pageSize));
    }
    if (criteria?.trim()) {
      url.searchParams.set('criteria', criteria.trim());
    }

    const response = await this.safeFetch(url.toString(), {
      method: 'GET',
      headers: {
        Accept: 'application/json',
        ...this.getAuthHeader(),
      },
      ...(signal ? { signal } : {}),
    });

    const totalCountHeader = response.headers.get('x-total-count') || response.headers.get('X-Total-Count');
    const raw = await this.handleResponse<any[]>(response);
    const result = raw.map((c) => ({
      id: c.id,
      name: c.name,
      vat: c.vat ?? '',
      email: c.email ?? '',
      street: c.street ?? '',
      phone: c.phone ?? '',
    }));
    if (totalCountHeader) {
      const parsed = parseInt(totalCountHeader, 10);
      if (!isNaN(parsed)) {
        (result as any).totalCount = parsed;
      }
    }
    return result;
  }

  public async getActiveSession(): Promise<PosSession | null> {
    try {
      const response = await this.safeFetch(`${this._baseUrl}/api/pos/sessions/active`, {
        method: 'GET',
        headers: {
          Accept: 'application/json',
          ...this.getAuthHeader(),
        },
      });

      if (response.status === 404) {
        return null;
      }

      return await this.handleResponse<PosSession>(response);
    } catch (err: unknown) {
      if (err instanceof PosApiError && err.status === 404) {
        return null;
      }
      throw err;
    }
  }

  public async createSale(request: CreateSaleRequest): Promise<SaleOrderResult> {
    const response = await this.safeFetch(`${this._baseUrl}/api/sales`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
        'Idempotency-Key': request.idempotencyKey,
        ...this.getAuthHeader(),
      },
      body: JSON.stringify(request),
    });

    return await this.handleResponse<SaleOrderResult>(response);
  }

  public async getSalesHistory(
    page = 1,
    pageSize = 5,
    search?: string,
    filter?: string,
    signal?: AbortSignal
  ): Promise<SaleOrderResult[]> {
    const params = new URLSearchParams();
    if (page) params.append('page', String(page));
    if (pageSize) params.append('pageSize', String(pageSize));
    if (search && search.trim()) params.append('search', search.trim());
    if (filter && filter !== 'ALL') params.append('filter', filter);

    const queryString = params.toString() ? `?${params.toString()}` : '';
    const response = await this.safeFetch(`${this._baseUrl}/api/sales${queryString}`, {
      method: 'GET',
      headers: {
        Accept: 'application/json',
        ...this.getAuthHeader(),
      },
      ...(signal ? { signal } : {}),
    });

    const totalCountHeader = response.headers.get('x-total-count') || response.headers.get('X-Total-Count');
    const items = await this.handleResponse<SaleOrderResult[]>(response);
    if (Array.isArray(items)) {
      if (totalCountHeader) {
        const parsed = parseInt(totalCountHeader, 10);
        if (!isNaN(parsed)) {
          (items as any).totalCount = parsed;
        }
      }
    }
    return items;
  }

  public async getSaleById(id: string): Promise<SaleOrderDetailDto | null> {
    const response = await this.safeFetch(`${this._baseUrl}/api/sales/${id}`, {
      method: 'GET',
      headers: {
        Accept: 'application/json',
        ...this.getAuthHeader(),
      },
    });

    if (response.status === 404) {
      return null;
    }

    return await this.handleResponse<SaleOrderDetailDto>(response);
  }
}
