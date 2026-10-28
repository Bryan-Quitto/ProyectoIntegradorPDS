import { Product } from '@/domain/Product';

export interface PosSession {
  id: number;
  name: string;
  state: string;
  userId: number;
  isOpen: boolean;
}

export interface CreateSaleRequest {
  idempotencyKey: string;
  posSessionId: number;
  customerDocument: string;
  customerName: string;
  discountType: string;
  discountValue: number;
  items: { productId: number; quantity: number }[];
}

export interface SaleOrderResult {
  orderId: string;
  posSessionId: number;
  customerDocument: string;
  customerName: string;
  subTotal: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  state: string;
  odooOrderReference?: string;
  odooOrderId?: number;
  createdAtUtc: string;
  lines: {
    productId: number;
    productName: string;
    quantity: number;
    unitPrice: number;
    subTotal: number;
  }[];
}

export class PosApiError extends Error {
  public readonly status: number;
  public readonly title: string;
  public readonly detail: string;

  constructor(status: number, title: string, detail: string) {
    super(detail || title);
    this.name = 'PosApiError';
    this.status = status;
    this.title = title;
    this.detail = detail;
  }
}

export interface IPosApiGateway {
  getProducts(search?: string): Promise<Product[]>;
  getActiveSession(): Promise<PosSession | null>;
  createSale(request: CreateSaleRequest): Promise<SaleOrderResult>;
  getSalesHistory(): Promise<SaleOrderResult[]>;
  getSaleById(id: string): Promise<SaleOrderResult | null>;
}
