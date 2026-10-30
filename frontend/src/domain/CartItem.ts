export interface CartItem {
  productId: number;
  productName: string;
  unitPrice: number;
  quantity: number;
  subtotal: number;
  productCode?: string;
}

export type DiscountType = 'NONE' | 'PERCENTAGE' | 'THRESHOLD_FIXED';

export interface DiscountPolicyConfig {
  type: DiscountType;
  value: number;
}
