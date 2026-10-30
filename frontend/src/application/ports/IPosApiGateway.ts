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

export interface SaleOrderLineDetail {
  productId: number;
  productName: string;
  productCode?: string;
  quantity: number;
  unitPrice: number;
  subTotal: number;
  taxRate?: number;
  netSubTotal?: number;
  taxAmount?: number;
  totalWithTax?: number;
}

export interface SaleOrderDetailDto {
  orderId: string;
  posSessionId: number;
  sessionName?: string;
  operatorName?: string;
  customerDocument: string;
  customerName: string;
  subTotal: number;
  discountAmount: number;
  discountPolicyName?: string;
  taxAmount: number;
  totalAmount: number;
  paymentMethod?: string;
  amountPaid?: number;
  state: string;
  odooOrderReference?: string;
  odooOrderId?: number;
  createdAtUtc: string;
  lines: SaleOrderLineDetail[];
}

export type SaleOrderResult = SaleOrderDetailDto;

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
  getSaleById(id: string): Promise<SaleOrderDetailDto | null>;
}
