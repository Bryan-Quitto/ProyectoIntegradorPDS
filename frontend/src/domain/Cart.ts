import { CartItem, DiscountPolicyConfig } from './CartItem';
import { Product } from './Product';

export class Cart {
  private readonly _items: Map<number, CartItem> = new Map();
  private _discountPolicy: DiscountPolicyConfig = { type: 'NONE', value: 0 };
  private static readonly VAT_RATE = 0.15; // IVA Ecuador 15%

  public get items(): CartItem[] {
    return Array.from(this._items.values());
  }

  public get discountPolicy(): DiscountPolicyConfig {
    return { ...this._discountPolicy };
  }

  public addItem(product: Product, quantity = 1): void {
    if (quantity <= 0) {
      throw new Error('La cantidad de producto debe ser mayor a 0 (RN-02).');
    }

    const existing = this._items.get(product.id);
    if (existing) {
      const newQty = existing.quantity + quantity;
      this._items.set(product.id, {
        ...existing,
        quantity: newQty,
        subtotal: Number((existing.unitPrice * newQty).toFixed(2)),
      });
    } else {
      this._items.set(product.id, {
        productId: product.id,
        productName: product.name,
        unitPrice: product.price,
        quantity: quantity,
        subtotal: Number((product.price * quantity).toFixed(2)),
      });
    }
  }

  public updateQuantity(productId: number, quantity: number): void {
    if (quantity <= 0) {
      throw new Error('La cantidad debe ser mayor a 0 (RN-02).');
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

  public get netSubtotal(): number {
    return Number(Math.max(0, this.subtotal - this.discountAmount).toFixed(2));
  }

  public get taxAmount(): number {
    return Number((this.netSubtotal * Cart.VAT_RATE).toFixed(2));
  }

  public get total(): number {
    return Number((this.netSubtotal + this.taxAmount).toFixed(2));
  }
}
