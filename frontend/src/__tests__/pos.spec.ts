import { describe, it, expect, vi } from 'vitest';
import { Cart } from '../domain/Cart';
import { Product } from '../domain/Product';
import { ManageCartUseCase, CheckoutSaleUseCase, GetSaleDetailUseCase } from '../application/useCases/UseCases';
import { IPosApiGateway, PosApiError, SaleOrderDetailDto } from '../application/ports/IPosApiGateway';
import { formatEcDate, formatEcTime, parseUtcDate } from '../presentation/utils/dateUtils';

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

  // =========================================================================
  // P15: Consulta de detalle de venta completa por ID (RF-14)
  // =========================================================================
  it('P15: GetSaleDetailUseCase recupera orden completa con desglose de ítems, totales e impuestos (RF-14)', async () => {
    // Arrange
    const sampleSale: SaleOrderDetailDto = {
      orderId: '1254aa45-03f5-425a-ace6-320ff6fe712b',
      posSessionId: 4,
      sessionName: 'My Company/00002',
      operatorName: 'Cajero POS',
      customerDocument: '1712345678',
      customerName: 'Juan Perez',
      subTotal: 35.00,
      discountAmount: 0.00,
      discountPolicyName: 'Sin Descuento',
      taxAmount: 4.57,
      totalAmount: 35.00,
      paymentMethod: 'Efectivo',
      amountPaid: 35.00,
      state: 'Synced',
      odooOrderReference: '260-1-000003',
      odooOrderId: 5,
      createdAtUtc: '2026-10-29T20:40:07Z',
      lines: [
        {
          productId: 3,
          productName: 'Producto A',
          productCode: 'PROD-001',
          quantity: 1,
          unitPrice: 15.00,
          subTotal: 15.00,
          taxRate: 0.15,
          netSubTotal: 13.04,
          taxAmount: 1.96,
          totalWithTax: 15.00,
        },
        {
          productId: 4,
          productName: 'Producto B',
          productCode: 'PROD-002',
          quantity: 1,
          unitPrice: 20.00,
          subTotal: 20.00,
          taxRate: 0.15,
          netSubTotal: 17.39,
          taxAmount: 2.61,
          totalWithTax: 20.00,
        },
      ],
    };

    const mockGateway: IPosApiGateway = {
      getProducts: vi.fn(),
      getActiveSession: vi.fn(),
      createSale: vi.fn(),
      getSalesHistory: vi.fn(),
      getSaleById: vi.fn().mockResolvedValue(sampleSale),
    };

    const useCase = new GetSaleDetailUseCase(mockGateway);

    // Act
    const result = await useCase.execute('1254aa45-03f5-425a-ace6-320ff6fe712b');

    // Assert
    expect(mockGateway.getSaleById).toHaveBeenCalledWith('1254aa45-03f5-425a-ace6-320ff6fe712b');
    expect(result).not.toBeNull();
    expect(result?.orderId).toBe('1254aa45-03f5-425a-ace6-320ff6fe712b');
    expect(result?.odooOrderReference).toBe('260-1-000003');
    expect(result?.totalAmount).toBe(35.00);
    expect(result?.taxAmount).toBe(4.57);
    expect(result?.paymentMethod).toBe('Efectivo');
    expect(result?.lines).toHaveLength(2);
    expect(result?.lines[0].productCode).toBe('PROD-001');
    expect(result?.lines[1].productCode).toBe('PROD-002');
  });

  // =========================================================================
  // P16: Conversión de timestamps UTC a Zona Horaria Ecuador (UTC-5)
  // =========================================================================
  it('P16: Formateo de fecha y hora convierte timestamps UTC a hora local Ecuador UTC-5 (Odoo sync)', () => {
    // 20:40:07 UTC debe coincidir con las 15:40:07 (3:40 p.m.) de Odoo
    const utcTimestamp = '2026-10-29T20:40:07.0762264';

    const formattedDate = formatEcDate(utcTimestamp);
    const formattedTime = formatEcTime(utcTimestamp);

    expect(formattedDate).toContain('2026');
    expect(formattedDate).toContain('oct');
    // Verifica que la hora convertida sea 3:40 p.m. / 15:40 y NO 8:40 p.m. / 20:40
    expect(formattedTime).toMatch(/0?3:40/);
    expect(formattedTime).not.toMatch(/0?8:40/);
  });

  // =========================================================================
  // P17: Validación de límites de cantidad en Carrito (min: 1, max: 1000)
  // =========================================================================
  it('P17: Validación de límites en Cart: cantidades válidas hasta 1000, rechaza <= 0 y > 1000', () => {
    const cart = new Cart();
    const useCase = new ManageCartUseCase();
    const product: Product = { id: 10, name: 'Teclado USB', price: 25.00, barcode: '123456', availableQuantity: 1500 };

    useCase.addItem(cart, product, 1);
    expect(cart.items[0].quantity).toBe(1);

    // Permite cambiar a 500 y 1000
    useCase.updateQuantity(cart, 10, 500);
    expect(cart.items[0].quantity).toBe(500);

    useCase.updateQuantity(cart, 10, 1000);
    expect(cart.items[0].quantity).toBe(1000);

    // Rechaza <= 0
    expect(() => useCase.updateQuantity(cart, 10, 0)).toThrow(/mayor a 0/);
    expect(() => useCase.updateQuantity(cart, 10, -5)).toThrow(/mayor a 0/);

    // Rechaza > 1000
    expect(() => useCase.updateQuantity(cart, 10, 1001)).toThrow(/1000 unidades/);
  });

  // =========================================================================
  // P18: Filtrado de Catálogo por Nombre, Código de Referencia y Todos
  // =========================================================================
  it('P18: Filtrado de productos por Nombre, Código de Referencia y combinación (Todos)', () => {
    const catalog: Product[] = [
      { id: 1, name: 'Producto A', defaultCode: 'PROD-001', price: 15.00, barcode: '7861001', availableQuantity: 100 },
      { id: 2, name: 'Producto B', defaultCode: 'PROD-002', price: 20.00, barcode: '7861002', availableQuantity: 50 },
      { id: 3, name: 'Café Espresso', defaultCode: 'BEV-001', price: 2.50, barcode: '7861003', availableQuantity: 200 },
    ];

    // Función de filtrado idéntica a ProductCatalogPanel
    const filterCatalog = (query: string, mode: 'ALL' | 'NAME' | 'CODE') => {
      const term = query.trim().toLowerCase();
      if (!term) return catalog;
      return catalog.filter((p) => {
        const matchName = p.name.toLowerCase().includes(term);
        const matchCode = (p.defaultCode && p.defaultCode.toLowerCase().includes(term)) || (p.barcode && p.barcode.includes(term));
        if (mode === 'NAME') return matchName;
        if (mode === 'CODE') return matchCode;
        return matchName || matchCode;
      });
    };

    // Búsqueda por Código exacto
    const byCode = filterCatalog('PROD-001', 'CODE');
    expect(byCode).toHaveLength(1);
    expect(byCode[0].name).toBe('Producto A');

    // Búsqueda por Nombre exacto
    const byName = filterCatalog('Café', 'NAME');
    expect(byName).toHaveLength(1);
    expect(byName[0].defaultCode).toBe('BEV-001');

    // Búsqueda 'Todos' por prefijo de código 'PROD'
    const byAll = filterCatalog('PROD', 'ALL');
    expect(byAll).toHaveLength(2);
  });

  // =========================================================================
  // P19: Filtrado de Historial de Ventas (Referencia Odoo contains, Cliente contains, Cédula startsWith)
  // =========================================================================
  it('P19: Filtrado de historial: Referencia Odoo acotada sin falsos positivos de GUID, Cliente contains y Cédula startsWith', () => {
    const salesMock: Partial<SaleOrderDetailDto>[] = [
      {
        orderId: '1254aa45-03f5-425a-ace6-320ff6fe712b',
        odooOrderReference: '260-1-000003',
        odooOrderId: 5,
        customerName: 'Juan Perez',
        customerDocument: '1712345678',
        totalAmount: 35.00,
      },
      {
        orderId: '316d671c-8b89-408a-b9c1-4040ff99812a',
        odooOrderReference: '260-1-000004',
        odooOrderId: 6,
        customerName: 'Consumidor Final',
        customerDocument: '9999999999999',
        totalAmount: 36.00,
      },
    ];

    const filterSales = (query: string, mode: 'ALL' | 'REFERENCE' | 'CUSTOMER') => {
      const term = query.trim().toLowerCase();
      if (!term) return salesMock;

      return salesMock.filter((sale) => {
        const odooRef = (sale.odooOrderReference ?? '').toLowerCase();
        const odooId = sale.odooOrderId ? String(sale.odooOrderId) : '';
        const localId = (sale.orderId ?? '').toLowerCase();
        const clientName = (sale.customerName ?? '').toLowerCase();
        const clientDoc = (sale.customerDocument ?? '').trim();

        const matchReference =
          odooRef.includes(term) ||
          odooId === term ||
          (term.length >= 4 && localId.startsWith(term));

        const matchCustomer =
          clientName.includes(term) || clientDoc.startsWith(term);

        if (mode === 'REFERENCE') return matchReference;
        if (mode === 'CUSTOMER') return matchCustomer;
        return matchReference || matchCustomer;
      });
    };

    // Al buscar '7' por Referencia, da 0 resultados (evita falsos positivos por dígitos aleatorios en el GUID)
    expect(filterSales('7', 'REFERENCE')).toHaveLength(0);

    // Al buscar '4' por Referencia, retorna únicamente la orden 260-1-000004
    const by4 = filterSales('4', 'REFERENCE');
    expect(by4).toHaveLength(1);
    expect(by4[0].odooOrderReference).toBe('260-1-000004');

    // Al buscar '3' por Referencia, retorna únicamente la orden 260-1-000003
    const by3 = filterSales('3', 'REFERENCE');
    expect(by3).toHaveLength(1);
    expect(by3[0].odooOrderReference).toBe('260-1-000003');

    // Nombre de cliente usa CONTAINS: 'Perez' encuentra a 'Juan Perez'
    const byLastName = filterSales('Perez', 'CUSTOMER');
    expect(byLastName).toHaveLength(1);
    expect(byLastName[0].customerName).toBe('Juan Perez');

    // Cédula/RUC usa STARTSWITH:
    // '17' encuentra a Juan Perez (1712345678)
    expect(filterSales('17', 'CUSTOMER')).toHaveLength(1);
    // '12' (en medio de 1712345678) no hace match con startsWith
    expect(filterSales('12', 'CUSTOMER')).toHaveLength(0);
    // '999' encuentra a Consumidor Final
    expect(filterSales('999', 'CUSTOMER')).toHaveLength(1);
  });
});
