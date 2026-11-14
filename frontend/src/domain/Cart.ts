import { CartItem, DiscountPolicyConfig } from './CartItem';
import { Product } from './Product';

export class Cart {
  private readonly _items: Map<number, CartItem> = new Map();
  private _discountPolicy: DiscountPolicyConfig = { type: 'NONE', value: 0 };
  public static readonly MAX_QUANTITY = 1000;
  public static readonly MIN_QUANTITY = 1;
  private static readonly VAT_RATE = 0.15; // IVA Ecuador 15%

  public get items(): CartItem[] {
    return Array.from(this._items.values());
  }

  public get discountPolicy(): DiscountPolicyConfig {
    return { ...this._discountPolicy };
  }

  public addItem(product: Product, quantity = 1): void {
    if (quantity < Cart.MIN_QUANTITY) {
      throw new Error('La cantidad de producto debe ser mayor a 0 (RN-02).');
    }

    const existing = this._items.get(product.id);
    if (existing) {
      const newQty = Math.min(Cart.MAX_QUANTITY, existing.quantity + quantity);
      this._items.set(product.id, {
        ...existing,
        quantity: newQty,
        subtotal: Number((existing.unitPrice * newQty).toFixed(2)),
      });
    } else {
      const initialQty = Math.min(Cart.MAX_QUANTITY, quantity);
      this._items.set(product.id, {
        productId: product.id,
        productName: product.name,
        unitPrice: product.price,
        quantity: initialQty,
        subtotal: Number((product.price * initialQty).toFixed(2)),
        productCode: product.defaultCode || '',
      });
    }
  }

  public updateQuantity(productId: number, quantity: number): void {
    if (quantity < Cart.MIN_QUANTITY) {
      throw new Error('La cantidad debe ser mayor a 0 (RN-02).');
    }
    if (quantity > Cart.MAX_QUANTITY) {
      throw new Error(`La cantidad no puede superar las ${Cart.MAX_QUANTITY} unidades (límite de seguridad POS).`);
    }

    const existing = this._items.get(productId);
    if (!existing) {
      throw new Error(`El producto con ID ${productId} no se encuentra en el carrito.`);
    }

    this._items.set(productId, {
      ...existing,
      quantity: quantity,
      subtotal: Number((existing.unitPrice * quantity).toFixed(2)),
    });
  }

  public removeItem(productId: number): void {
    this._items.delete(productId);
  }

  public clear(): void {
    this._items.clear();
    this._discountPolicy = { type: 'NONE', value: 0 };
  }

  public setDiscount(policy: DiscountPolicyConfig): void {
    if (policy.value < 0) {
      throw new Error('El valor del descuento no puede ser negativo.');
    }
    this._discountPolicy = policy;
  }

  public get subtotal(): number {
    const raw = this.items.reduce((sum, item) => sum + item.subtotal, 0);
    return Number(raw.toFixed(2));
  }

  public get discountAmount(): number {
    const sub = this.subtotal;
    if (sub <= 0) return 0;

    let discount = 0;
    if (this._discountPolicy.type === 'PERCENTAGE') {
      discount = sub * (this._discountPolicy.value / 100);
    } else if (this._discountPolicy.type === 'THRESHOLD_FIXED') {
      // Si subtotal >= 50 aplica descuento fijo
      if (sub >= 50) {
        discount = Math.min(this._discountPolicy.value, sub);
      }
    }

    return Number(discount.toFixed(2));
  }

  private getLineFiscalBreakdown() {
    const sub = this.subtotal;
    const discount = this.discountAmount;
    const discountPercent = sub > 0 ? (discount / sub) * 100 : 0;

    return this.items.map((item) => {
      const lineSubtotal = item.subtotal;
      const discountedAmount = Number(
        (lineSubtotal * (1 - discountPercent / 100)).toFixed(2)
      );
      const lineDiscount = Number((lineSubtotal - discountedAmount).toFixed(2));
      const taxBase = discountedAmount;
      const taxAmount = Number((taxBase * Cart.VAT_RATE).toFixed(2));
      const totalWithTax = Number((taxBase + taxAmount).toFixed(2));
      return { lineSubtotal, lineDiscount, taxBase, taxAmount, totalWithTax };
    });
  }

  public get netSubtotal(): number {
    const breakdown = this.getLineFiscalBreakdown();
    const sum = breakdown.reduce((acc, b) => acc + b.taxBase, 0);
    return Number(sum.toFixed(2));
  }

  public get taxAmount(): number {
    const breakdown = this.getLineFiscalBreakdown();
    const sum = breakdown.reduce((acc, b) => acc + b.taxAmount, 0);
    return Number(sum.toFixed(2));
  }

  public get total(): number {
    const breakdown = this.getLineFiscalBreakdown();
    const sum = breakdown.reduce((acc, b) => acc + b.totalWithTax, 0);
    return Number(sum.toFixed(2));
  }
}
