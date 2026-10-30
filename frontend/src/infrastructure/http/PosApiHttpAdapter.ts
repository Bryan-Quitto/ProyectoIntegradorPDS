import { Product } from '@/domain/Product';
import {
  IPosApiGateway,
  PosSession,
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
      const auth = localStorage.getItem('auth_token');
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

  public async getProducts(search?: string): Promise<Product[]> {
    const url = new URL(`${this._baseUrl}/api/products`);
    if (search?.trim()) {
      url.searchParams.set('search', search.trim());
    }

    const response = await fetch(url.toString(), {
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

  public async getActiveSession(): Promise<PosSession | null> {
    try {
      const response = await fetch(`${this._baseUrl}/api/pos/sessions/active`, {
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
    const response = await fetch(`${this._baseUrl}/api/sales`, {
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

  public async getSalesHistory(): Promise<SaleOrderResult[]> {
    const response = await fetch(`${this._baseUrl}/api/sales`, {
      method: 'GET',
      headers: {
        Accept: 'application/json',
        ...this.getAuthHeader(),
      },
    });

    return await this.handleResponse<SaleOrderResult[]>(response);
  }

  public async getSaleById(id: string): Promise<SaleOrderDetailDto | null> {
    const response = await fetch(`${this._baseUrl}/api/sales/${id}`, {
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
