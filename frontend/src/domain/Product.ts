export interface Product {
  id: number;
  name: string;
  price: number;
  barcode: string;
  availableQuantity: number;
  defaultCode?: string;
}
