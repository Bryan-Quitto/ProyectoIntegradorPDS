import { Cart } from '@/domain/Cart';
import { Product } from '@/domain/Product';
import { DiscountType } from '@/domain/CartItem';
import { validateEcuadorianId } from '@/domain/ecuadorianIdValidator';
import { IPosApiGateway, CreateSaleRequest, SaleOrderResult, PosApiError, PosSession } from '../ports/IPosApiGateway';
export { GetSaleDetailUseCase } from './GetSaleDetailUseCase';

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

  public async execute(queryOrGateway?: string | IPosApiGateway, query?: string): Promise<Product[]> {
    if (queryOrGateway && typeof queryOrGateway === 'object' && 'getProducts' in queryOrGateway) {
      return await queryOrGateway.getProducts(query);
    }
    const gateway = this._gateway;
    if (!gateway) {
      throw new Error('IPosApiGateway no provisto a SearchCatalogUseCase');
    }
    return await gateway.getProducts(queryOrGateway as string | undefined);
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

  public async execute(): Promise<SaleOrderResult[]> {
    return await this._gateway.getSalesHistory();
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
    const { cart, posSessionId, customerDocument, customerName, idempotencyKey, paymentMethod } = input;

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
