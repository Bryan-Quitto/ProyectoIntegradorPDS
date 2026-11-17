import { Cart } from '@/domain/Cart';
import { Product } from '@/domain/Product';
import { DiscountType } from '@/domain/CartItem';
import { validateEcuadorianId } from '@/domain/ecuadorianIdValidator';
import {
  IPosApiGateway,
  LoginRequest,
  LoginResponse,
  CreateSaleRequest,
  SaleOrderResult,
  PosApiError,
  PosSession,
  Customer,
} from '../ports/IPosApiGateway';
export { GetSaleDetailUseCase } from './GetSaleDetailUseCase';

export class LoginUseCase {
  private readonly _gateway: IPosApiGateway;

  constructor(gateway: IPosApiGateway) {
    this._gateway = gateway;
  }

  public async execute(request: LoginRequest): Promise<LoginResponse> {
    if (!request.username || !request.username.trim() || !request.password) {
      throw new Error('El usuario y la contraseña son campos obligatorios.');
    }
    if (!this._gateway.login) {
      throw new Error('El método login no está implementado en el gateway provisto.');
    }
    return await this._gateway.login(request);
  }
}

export class ManageCartUseCase {
  public addItem(cart: Cart, product: Product, quantity = 1): void {
    cart.addItem(product, quantity);
  }

  public updateQuantity(cart: Cart, productId: number, quantity: number): void {
    cart.updateQuantity(productId, quantity);
  }

  public removeItem(cart: Cart, productId: number): void {
    cart.removeItem(productId);
  }

  public clearCart(cart: Cart): void {
    cart.clear();
  }

  public applyDiscount(cart: Cart, type: DiscountType, value: number): void {
    cart.setDiscount({ type, value });
  }
}

export class SearchCatalogUseCase {
  private readonly _gateway?: IPosApiGateway;

  constructor(gateway?: IPosApiGateway) {
    this._gateway = gateway;
  }

  public async execute(
    gateway: IPosApiGateway,
    query?: string,
    page?: number,
    pageSize?: number,
    criteria?: string,
    onlyInStock?: boolean,
    signal?: AbortSignal
  ): Promise<Product[]>;
  public async execute(
    query?: string,
    page?: number,
    pageSize?: number,
    criteria?: string,
    onlyInStock?: boolean,
    signal?: AbortSignal
  ): Promise<Product[]>;
  public async execute(
    queryOrGateway?: string | IPosApiGateway,
    queryOrPage?: string | number,
    pageOrPageSize?: number,
    pageSizeOrCriteria?: number | string,
    criteriaOrOnlyInStock?: string | boolean,
    onlyInStockOrSignal?: boolean | AbortSignal,
    signalParam?: AbortSignal
  ): Promise<Product[]> {
    if (queryOrGateway && typeof queryOrGateway === 'object' && 'getProducts' in queryOrGateway) {
      const q = typeof queryOrPage === 'string' ? queryOrPage : undefined;
      const p = typeof pageOrPageSize === 'number' ? pageOrPageSize : undefined;
      const ps = typeof pageSizeOrCriteria === 'number' ? pageSizeOrCriteria : undefined;
      const crit = typeof criteriaOrOnlyInStock === 'string' ? criteriaOrOnlyInStock : undefined;
      const stock = typeof onlyInStockOrSignal === 'boolean' ? onlyInStockOrSignal : undefined;
      const sig = signalParam;
      if (sig !== undefined) {
        return await queryOrGateway.getProducts(q, p, ps, crit, stock, sig);
      }
      if (stock !== undefined) {
        return await queryOrGateway.getProducts(q, p, ps, crit, stock);
      }
      if (p !== undefined || ps !== undefined || crit !== undefined) {
        return await queryOrGateway.getProducts(q, p, ps, crit);
      }
      return await queryOrGateway.getProducts(q);
    }

    const gateway = this._gateway;
    if (!gateway) {
      throw new Error('IPosApiGateway no provisto a SearchCatalogUseCase');
    }

    const q = typeof queryOrGateway === 'string' ? queryOrGateway : undefined;
    const p = typeof queryOrPage === 'number' ? queryOrPage : undefined;
    const ps = typeof pageOrPageSize === 'number' ? pageOrPageSize : undefined;
    const crit = typeof pageSizeOrCriteria === 'string' ? pageSizeOrCriteria : undefined;
    const stock = typeof criteriaOrOnlyInStock === 'boolean' ? criteriaOrOnlyInStock : undefined;
    const sig =
      onlyInStockOrSignal && typeof onlyInStockOrSignal === 'object' && 'aborted' in onlyInStockOrSignal
        ? onlyInStockOrSignal
        : undefined;

    if (sig !== undefined) {
      return await gateway.getProducts(q, p, ps, crit, stock, sig);
    }
    if (stock !== undefined) {
      return await gateway.getProducts(q, p, ps, crit, stock);
    }
    if (p !== undefined || ps !== undefined || crit !== undefined) {
      return await gateway.getProducts(q, p, ps, crit);
    }
    return await gateway.getProducts(q);
  }
}

export class GetProductsByIdsUseCase {
  private readonly _gateway: IPosApiGateway;

  constructor(gateway: IPosApiGateway) {
    this._gateway = gateway;
  }

  public async execute(ids: number[]): Promise<Product[]> {
    if (!ids || ids.length === 0) {
      return [];
    }
    return await this._gateway.getProductsByIds(ids);
  }
}

export class SearchCustomersUseCase {
  private readonly _gateway?: IPosApiGateway;

  constructor(gateway?: IPosApiGateway) {
    this._gateway = gateway;
  }

  public async execute(
    gateway: IPosApiGateway,
    query?: string,
    page?: number,
    pageSize?: number,
    criteria?: string,
    signal?: AbortSignal
  ): Promise<Customer[]>;
  public async execute(
    query?: string,
    page?: number,
    pageSize?: number,
    criteria?: string,
    signal?: AbortSignal
  ): Promise<Customer[]>;
  public async execute(
    queryOrGateway?: string | IPosApiGateway,
    queryOrPage?: string | number,
    pageOrPageSize?: number,
    pageSizeOrCriteria?: number | string,
    criteriaOrSignal?: string | AbortSignal,
    signalParam?: AbortSignal
  ): Promise<Customer[]> {
    if (queryOrGateway && typeof queryOrGateway === 'object' && 'getCustomers' in queryOrGateway) {
      const q = typeof queryOrPage === 'string' ? queryOrPage : undefined;
      const p = typeof pageOrPageSize === 'number' ? pageOrPageSize : undefined;
      const ps = typeof pageSizeOrCriteria === 'number' ? pageSizeOrCriteria : undefined;
      const crit =
        typeof criteriaOrSignal === 'string'
          ? criteriaOrSignal
          : typeof pageSizeOrCriteria === 'string'
          ? pageSizeOrCriteria
          : undefined;
      const sig =
        signalParam ??
        (criteriaOrSignal && typeof criteriaOrSignal === 'object' && 'aborted' in criteriaOrSignal
          ? criteriaOrSignal
          : undefined);

      if (sig !== undefined) {
        return await queryOrGateway.getCustomers(q, p, ps, crit, sig);
      }
      if (p !== undefined || ps !== undefined || crit !== undefined) {
        return await queryOrGateway.getCustomers(q, p, ps, crit);
      }
      return await queryOrGateway.getCustomers(q);
    }

    const gateway = this._gateway;
    if (!gateway) {
      throw new Error('IPosApiGateway no provisto a SearchCustomersUseCase');
    }

    const q = typeof queryOrGateway === 'string' ? queryOrGateway : undefined;
    const p =
      typeof queryOrPage === 'number'
        ? queryOrPage
        : typeof pageOrPageSize === 'number'
        ? pageOrPageSize
        : undefined;
    const ps =
      typeof pageSizeOrCriteria === 'number'
        ? pageSizeOrCriteria
        : typeof queryOrPage === 'number' && typeof pageOrPageSize === 'number'
        ? pageOrPageSize
        : undefined;
    const crit =
      typeof criteriaOrSignal === 'string'
        ? criteriaOrSignal
        : typeof pageSizeOrCriteria === 'string'
        ? pageSizeOrCriteria
        : undefined;
    const sig =
      criteriaOrSignal && typeof criteriaOrSignal === 'object' && 'aborted' in criteriaOrSignal
        ? criteriaOrSignal
        : signalParam;

    if (sig !== undefined) {
      return await gateway.getCustomers(q, p, ps, crit, sig);
    }
    if (p !== undefined || ps !== undefined || crit !== undefined) {
      return await gateway.getCustomers(q, p, ps, crit);
    }
    return await gateway.getCustomers(q);
  }
}

export class GetActiveSessionUseCase {
  private readonly _gateway: IPosApiGateway;

  constructor(gateway: IPosApiGateway) {
    this._gateway = gateway;
  }

  public async execute(): Promise<PosSession | null> {
    return await this._gateway.getActiveSession();
  }
}

export class GetSalesHistoryUseCase {
  private readonly _gateway: IPosApiGateway;

  constructor(gateway: IPosApiGateway) {
    this._gateway = gateway;
  }

  public async execute(
    page?: number,
    pageSize?: number,
    search?: string,
    filter?: string,
    signal?: AbortSignal
  ): Promise<SaleOrderResult[]> {
    return await this._gateway.getSalesHistory(page, pageSize, search, filter, signal);
  }
}

export interface CheckoutInput {
  cart: Cart;
  posSessionId: number;
  customerDocument?: string;
  customerName?: string;
  idempotencyKey?: string;
  // [GUÍA ESTEBAN - TURNO 3 - RF-10]: Método de pago dinámico
  paymentMethod?: string;
  partnerId?: number | null;
}

export interface CheckoutResult {
  success: boolean;
  order?: SaleOrderResult;
  errorMessage?: string;
}

export class CheckoutSaleUseCase {
  private readonly _gateway: IPosApiGateway;

  constructor(gateway: IPosApiGateway) {
    this._gateway = gateway;
  }

  public async execute(input: CheckoutInput): Promise<CheckoutResult> {
    const { cart, posSessionId, customerDocument, customerName, idempotencyKey, paymentMethod, partnerId } = input;

    // 1. RN-01: Rechazar venta sin productos
    if (cart.items.length === 0) {
      return {
        success: false,
        errorMessage: 'No se puede procesar el cobro: el carrito está vacío (RN-01).',
      };
    }

    // 2. Validación de Cédula/RUC si se proporciona
    const doc = customerDocument?.trim() || '9999999999999';
    if (doc !== '9999999999999') {
      const validation = validateEcuadorianId(doc);
      if (!validation.isValid) {
        return {
          success: false,
          errorMessage: `Identificación del cliente inválida: ${validation.error}`,
        };
      }
    }

    // 3. Generar o reutilizar Idempotency Key (RN-08)
    const effectiveKey = idempotencyKey || (typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `key-${Date.now()}`);

    const request: CreateSaleRequest = {
      idempotencyKey: effectiveKey,
      posSessionId,
      customerDocument: doc,
      customerName: customerName?.trim() || 'Consumidor Final',
      discountType: cart.discountPolicy.type,
      discountValue: cart.discountPolicy.value,
      items: cart.items.map((i) => ({
        productId: i.productId,
        quantity: i.quantity,
      })),
      paymentMethod: paymentMethod || 'Efectivo',
      partnerId: partnerId ?? null,
    };

    try {
      const result = await this._gateway.createSale(request);
      return {
        success: true,
        order: result,
      };
    } catch (err: unknown) {
      // RN-07: Prohibido declarar falsos éxitos
      if (err instanceof PosApiError) {
        let detail = err.detail || '';
        const idMatch = detail.match(/ID (\d+)/i);
        if (idMatch) {
          const prodId = parseInt(idMatch[1], 10);
          const cartItem = cart.items.find((i) => i.productId === prodId);
          if (cartItem) {
            const refText = cartItem.productCode ? `[${cartItem.productCode}] ` : '';
            detail = detail.replace(
              new RegExp(`El producto con ID ${prodId}`, 'i'),
              `El producto ${refText}'${cartItem.productName}' (ID: ${prodId})`
            );
          }
        }
        return {
          success: false,
          errorMessage: `[${err.title}] ${detail}`,
        };
      } else if (err instanceof Error) {
        const msg = (err.message || '').toLowerCase();
        if (
          msg.includes('fetch') ||
          msg.includes('network') ||
          msg.includes('load failed') ||
          msg.includes('connection refused')
        ) {
          return {
            success: false,
            errorMessage:
              'Ha ocurrido un error inesperado al conectar con los servicios. Vuelva a intentarlo. En caso de persistir, contáctese con administración.',
          };
        }
        return {
          success: false,
          errorMessage: err.message,
        };
      }
      return {
        success: false,
        errorMessage:
          'Ha ocurrido un error inesperado al procesar la venta. Vuelva a intentarlo. En caso de persistir, contáctese con administración.',
      };
    }
  }
}
