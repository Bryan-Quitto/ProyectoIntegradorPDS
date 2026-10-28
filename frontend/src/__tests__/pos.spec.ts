import { describe, it, expect, vi } from 'vitest';
import { Cart } from '../domain/Cart';
import { Product } from '../domain/Product';
import { ManageCartUseCase, CheckoutSaleUseCase } from '../application/useCases/UseCases';
import { IPosApiGateway, PosApiError } from '../application/ports/IPosApiGateway';

describe('Frontend Clean Architecture - Suite de Pruebas Unitarias', () => {
  // =========================================================================
  // P07: Prueba unitaria sobre Cart / ManageCartUseCase (Dominio y Aplicación)
  // =========================================================================
  it('P07: Actualización de carrito, cantidades y cálculo de total provisional con IVA 15%', () => {
    // Arrange
    const cart = new Cart();
    const useCase = new ManageCartUseCase();

    const prod1: Product = { id: 1, name: 'Café Espresso', price: 2.50, barcode: '78612345', availableQuantity: 10 };
    const prod2: Product = { id: 2, name: 'Sándwich Jamón', price: 4.00, barcode: '78612346', availableQuantity: 5 };

    // Act: Agregar items
    useCase.addItem(cart, prod1, 2); // 2 * $2.50 = $5.00
    useCase.addItem(cart, prod2, 1); // 1 * $4.00 = $4.00

    // Assert: Subtotal inicial = $9.00
    expect(cart.items).toHaveLength(2);
    expect(cart.subtotal).toBe(9.00);

    // Act: Modificar cantidad de café a 4
    useCase.updateQuantity(cart, 1, 4); // 4 * $2.50 = $10.00. Subtotal nuevo = $14.00
    expect(cart.subtotal).toBe(14.00);

    // Act: Aplicar política de descuento del 10%
    useCase.applyDiscount(cart, 'PERCENTAGE', 10);

    // Assert:
    // Subtotal: $14.00
    // Descuento 10%: $1.40
    // Subtotal Neto: $12.60
    // IVA Ecuador (15% sobre 12.60): $1.89
    // Total: $14.49
    expect(cart.discountAmount).toBe(1.40);
    expect(cart.netSubtotal).toBe(12.60);
    expect(cart.taxAmount).toBe(1.89);
    expect(cart.total).toBe(14.49);
  });

  // =========================================================================
  // P08: Traducción de ProblemDetails de Odoo en CheckoutSaleUseCase (RN-07)
  // =========================================================================
  it('P08: Traduce ProblemDetails del Gateway en estado de error comprensible sin declarar falso éxito (RN-07)', async () => {
    // Arrange: Mock del IPosApiGateway que simula respuesta ProblemDetails 502 Bad Gateway desde Odoo
    const mockGateway: IPosApiGateway = {
      getProducts: vi.fn(),
      getActiveSession: vi.fn(),
      createSale: vi.fn().mockRejectedValue(
        new PosApiError(502, 'Fallo de Comunicación con ERP Odoo', 'Odoo rechazó la orden: Sesión cerrada en el servidor.')
      ),
      getSalesHistory: vi.fn(),
      getSaleById: vi.fn(),
    };

    const cart = new Cart();
    cart.addItem({ id: 1, name: 'Producto Test', price: 10, barcode: '123', availableQuantity: 2 }, 1);

    const checkoutUseCase = new CheckoutSaleUseCase(mockGateway);

    // Act
    const result = await checkoutUseCase.execute({
      cart,
      posSessionId: 1,
      customerDocument: '9999999999999',
      customerName: 'Consumidor Final',
    });

    // Assert: Debe reportar fallo sin marcar la venta como exitosa
    expect(result.success).toBe(false);
    expect(result.order).toBeUndefined();
    expect(result.errorMessage).toBeDefined();
    expect(result.errorMessage).toContain('Fallo de Comunicación con ERP Odoo');
    expect(result.errorMessage).toContain('Sesión cerrada en el servidor.');
  });
});
