using FluentAssertions;
using NSubstitute;
using Pos.Application.DTOs;
using Pos.Application.Exceptions;
using Pos.Application.Ports;
using Pos.Application.UseCases;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Strategies;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Tests;

public class PosTests
{
    // =========================================================================
    // P01: RN-01 Rechazar venta sin productos
    // =========================================================================
    [Fact]
    public void P01_RechazarVentaSinProductos_LanzaDomainValidationException()
    {
        // Arrange
        var order = new SaleOrder(posSessionId: 1);

        // Act
        var act = () => order.Confirm();

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*RN-01*");
    }

    // =========================================================================
    // P02: RN-02 Rechazar cantidad menor o igual a cero
    // =========================================================================
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void P02_RechazarCantidadMenorOIgualACero_LanzaDomainValidationException(int invalidQuantity)
    {
        // Act
        var act = () => new Quantity(invalidQuantity);

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("*RN-02*");
    }

    // =========================================================================
    // P03: Sesión de POS cerrada o inexistente rechaza la venta
    // =========================================================================
    [Fact]
    public async Task P03_SesionPosCerrada_LanzaApplicationValidationException()
    {
        // Arrange
        var odooGateway = Substitute.For<IOdooPosGateway>();
        var idempotencyStore = Substitute.For<IIdempotencyStore>();
        var historyRepo = Substitute.For<ISaleHistoryRepository>();

        // Sesión cerrada
        odooGateway.GetActiveSessionAsync(Arg.Any<CancellationToken>())
            .Returns(new PosSessionDto(1, "Main POS", "closed", 2, IsOpen: false));

        var useCase = new CreateSaleUseCase(odooGateway, idempotencyStore, historyRepo);
        var command = new CreateSaleCommand(
            IdempotencyKey: "key-p03",
            PosSessionId: 1,
            CustomerDocument: "1712345678",
            CustomerName: "Juan Perez",
            DiscountType: "NONE",
            DiscountValue: 0m,
            Items: new List<CreateSaleItemDto> { new(1, 2) }
        );

        // Act
        var act = () => useCase.ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ApplicationValidationException>()
            .WithMessage("*sesión de POS*");
    }

    // =========================================================================
    // P04: Producto no encontrado en catálogo de Odoo
    // =========================================================================
    [Fact]
    public async Task P04_ProductoInvalidoOInexistente_LanzaApplicationValidationException()
    {
        // Arrange
        var odooGateway = Substitute.For<IOdooPosGateway>();
        var idempotencyStore = Substitute.For<IIdempotencyStore>();
        var historyRepo = Substitute.For<ISaleHistoryRepository>();

        odooGateway.GetActiveSessionAsync(Arg.Any<CancellationToken>())
            .Returns(new PosSessionDto(1, "Main POS", "opened", 2, IsOpen: true));

        // Catálogo vacío
        odooGateway.GetProductsByIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ProductDto>());

        var useCase = new CreateSaleUseCase(odooGateway, idempotencyStore, historyRepo);
        var command = new CreateSaleCommand(
            IdempotencyKey: "key-p04",
            PosSessionId: 1,
            CustomerDocument: "1712345678",
            CustomerName: "Juan Perez",
            DiscountType: "NONE",
            DiscountValue: 0m,
            Items: new List<CreateSaleItemDto> { new(999, 1) } // Producto 999 no existe
        );

        // Act
        var act = () => useCase.ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ApplicationValidationException>()
            .WithMessage("*no existe en el catálogo de Odoo*");
    }

    // =========================================================================
    // P05: Error de comunicación o timeout con Odoo no declara falso éxito (RN-07)
    // =========================================================================
    [Fact]
    public async Task P05_ErrorComunicacionConOdoo_LanzaOdooCommunicationExceptionSinFalsoExito()
    {
        // Arrange
        var odooGateway = Substitute.For<IOdooPosGateway>();
        var idempotencyStore = Substitute.For<IIdempotencyStore>();
        var historyRepo = Substitute.For<ISaleHistoryRepository>();

        odooGateway.GetActiveSessionAsync(Arg.Any<CancellationToken>())
            .Returns(new PosSessionDto(1, "Main POS", "opened", 2, IsOpen: true));

        odooGateway.GetProductsByIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ProductDto>
            {
                new(1, "Coca Cola", 1.50m, "123456", 20m)
            });

        // Odoo falla en sincronización
        odooGateway.SyncSaleOrderAsync(Arg.Any<SaleOrder>(), Arg.Any<CancellationToken>())
            .Returns(new OdooSyncResultDto(false, string.Empty, 0, "Connection timeout with Odoo ERP"));

        var useCase = new CreateSaleUseCase(odooGateway, idempotencyStore, historyRepo);
        var command = new CreateSaleCommand(
            IdempotencyKey: "key-p05",
            PosSessionId: 1,
            CustomerDocument: "1712345678",
            CustomerName: "Juan Perez",
            DiscountType: "NONE",
            DiscountValue: 0m,
            Items: new List<CreateSaleItemDto> { new(1, 2) }
        );

        // Act
        var act = () => useCase.ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<OdooCommunicationException>()
            .WithMessage("*Connection timeout*");

        // No se debe persistir historial local como exitoso
        await historyRepo.DidNotReceive().SaveSaleAsync(Arg.Any<SaleOrderResponseDto>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // P06: Control de idempotencia (RN-08 / RF-16)
    // =========================================================================
    [Fact]
    public async Task P06_LlamadaConMismaIdempotencyKey_RetornaRespuestaPreviaSinDuplicar()
    {
        // Arrange
        var odooGateway = Substitute.For<IOdooPosGateway>();
        var idempotencyStore = Substitute.For<IIdempotencyStore>();
        var historyRepo = Substitute.For<ISaleHistoryRepository>();

        const string idempotencyKey = "uuid-idempotent-1234";

        var cachedResponse = new SaleOrderResponseDto(
            OrderId: Guid.NewGuid(),
            PosSessionId: 1,
            CustomerDocument: "1712345678",
            CustomerName: "Cliente Frecuente",
            SubTotal: 10m,
            DiscountAmount: 0m,
            TaxAmount: 1.5m,
            TotalAmount: 11.5m,
            State: "Synced",
            OdooOrderReference: "POS-001",
            OdooOrderId: 55,
            CreatedAtUtc: DateTime.UtcNow,
            Lines: new List<SaleOrderLineDto>()
        );

        string cachedJson = System.Text.Json.JsonSerializer.Serialize(cachedResponse);

        // Simular que la clave ya fue procesada
        idempotencyStore.HasKeyAsync(idempotencyKey, Arg.Any<CancellationToken>()).Returns(true);
        idempotencyStore.GetResponseAsync(idempotencyKey, Arg.Any<CancellationToken>()).Returns(cachedJson);

        var useCase = new CreateSaleUseCase(odooGateway, idempotencyStore, historyRepo);
        var command = new CreateSaleCommand(
            IdempotencyKey: idempotencyKey,
            PosSessionId: 1,
            CustomerDocument: "1712345678",
            CustomerName: "Cliente Frecuente",
            DiscountType: "NONE",
            DiscountValue: 0m,
            Items: new List<CreateSaleItemDto> { new(1, 1) }
        );

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().Be(cachedResponse.OrderId);
        result.OdooOrderReference.Should().Be("POS-001");

        // No debe llamar nuevamente a Odoo
        await odooGateway.DidNotReceive().SyncSaleOrderAsync(Arg.Any<SaleOrder>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // P10: Extensibilidad de Política de Descuento (Open/Closed Principle)
    // =========================================================================
    [Fact]
    public void P10_InyectarNuevaPoliticaDeDescuento_SinModificarSaleOrder()
    {
        // Arrange: Crear una nueva política de descuento (VIP 25%) que no existía antes
        var customVipPolicy = new CustomVipDiscountPolicy(25m);

        var order = new SaleOrder(posSessionId: 1, discountPolicy: customVipPolicy);
        var product = new Product(1, "Notebook Gamer", new Money(100m));
        order.AddLine(product, new Quantity(2)); // Subtotal = $200

        // Act
        order.Confirm();

        // Assert: $200 con 25% de descuento = $50 de descuento, neto $150, IVA 15% ($22.50) -> Total $172.50
        order.SubTotal.Should().Be(200m);
        order.DiscountAmount.Should().Be(50m);
        order.NetSubTotal.Should().Be(150m);
        order.TaxAmount.Should().Be(22.50m);
        order.TotalAmount.Should().Be(172.50m);
    }

    private sealed class CustomVipDiscountPolicy : IDiscountPolicy
    {
        private readonly decimal _percentage;
        public string PolicyName => $"VIP Custom Discount ({_percentage}%)";

        public CustomVipDiscountPolicy(decimal percentage)
        {
            _percentage = percentage;
        }

        public decimal CalculateDiscount(decimal subtotal)
        {
            return subtotal * (_percentage / 100m);
        }
    }

    // =========================================================================
    // P11 y P12: Autenticación Desacoplada vía Caso de Uso (ARQ-01 / ARQ-02)
    // =========================================================================
    [Fact]
    public async Task P11_LoginOperador_CredencialesValidas_GeneraTokenCorrectamente()
    {
        // Arrange
        var operatorRepo = Substitute.For<IOperatorRepository>();
        var jwtProvider = Substitute.For<IJwtProvider>();

        operatorRepo.HashPassword("securePassword123").Returns("hash_123");
        operatorRepo.GetActiveOperatorByCredentialsAsync("cashier1", "hash_123", Arg.Any<CancellationToken>())
            .Returns(new OperatorDto(1, "cashier1", "Cajero Principal", "Cashier", true));

        jwtProvider.GenerateToken("cashier1", "Cajero Principal", "Cashier")
            .Returns("jwt.mock.token.xyz");

        var useCase = new LoginOperatorUseCase(operatorRepo, jwtProvider);
        var command = new LoginCommand("cashier1", "securePassword123");

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result!.Token.Should().Be("jwt.mock.token.xyz");
        result.Username.Should().Be("cashier1");
        result.Role.Should().Be("Cashier");
    }

    [Fact]
    public async Task P12_LoginOperador_CredencialesInvalidas_RetornaNull()
    {
        // Arrange
        var operatorRepo = Substitute.For<IOperatorRepository>();
        var jwtProvider = Substitute.For<IJwtProvider>();

        operatorRepo.HashPassword("wrongPass").Returns("hash_wrong");
        operatorRepo.GetActiveOperatorByCredentialsAsync("cashier1", "hash_wrong", Arg.Any<CancellationToken>())
            .Returns((OperatorDto?)null);

        var useCase = new LoginOperatorUseCase(operatorRepo, jwtProvider);
        var command = new LoginCommand("cashier1", "wrongPass");

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        result.Should().BeNull();
        jwtProvider.DidNotReceive().GenerateToken(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // =========================================================================
    // P13: Happy path - venta en efectivo de 2 productos (Sección 13)
    // Replica la configuración REAL de Odoo 20 local: PROD-001 ($15) y PROD-002 ($20)
    // con impuesto "VAT 15% G" (id 1) INCLUIDO en el precio.
    // =========================================================================
    [Fact]
    public async Task P13_CreateSale_ValidTwoProductsCashOrder_ReturnsSyncedOrderWithVerifiedTotals()
    {
        // Arrange
        var odooGateway = Substitute.For<IOdooPosGateway>();
        var idempotencyStore = Substitute.For<IIdempotencyStore>();
        var historyRepo = Substitute.For<ISaleHistoryRepository>();

        const int sessionId = 4;
        var idempotencyKey = Guid.NewGuid().ToString();

        odooGateway.GetActiveSessionAsync(Arg.Any<CancellationToken>())
            .Returns(new PosSessionDto(sessionId, "My Company/00002", "opened", 2, IsOpen: true));

        odooGateway.GetProductsByIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ProductDto>
            {
                new(3, "Producto A", 15.00m, "", 100m, "PROD-001", TaxRate: 0.15m, PriceIncludesTax: true, TaxIds: new[] { 1 }),
                new(4, "Producto B", 20.00m, "", 100m, "PROD-002", TaxRate: 0.15m, PriceIncludesTax: true, TaxIds: new[] { 1 })
            });

        SaleOrder? orderSentToOdoo = null;
        string? stateWhenSent = null;
        odooGateway.SyncSaleOrderAsync(Arg.Any<SaleOrder>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                orderSentToOdoo = ci.Arg<SaleOrder>();
                stateWhenSent = orderSentToOdoo.State.StateName;
                return new OdooSyncResultDto(true, "00002-001-0001", 101, null);
            });

        var useCase = new CreateSaleUseCase(odooGateway, idempotencyStore, historyRepo);
        var command = new CreateSaleCommand(
            IdempotencyKey: idempotencyKey,
            PosSessionId: sessionId,
            CustomerDocument: "9999999999999",
            CustomerName: "Consumidor Final",
            DiscountType: "PERCENTAGE",
            DiscountValue: 10m,
            Items: new List<CreateSaleItemDto> { new(3, 1), new(4, 1) });

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert - RN-03: precios consultados en Odoo SOLO para los productos pedidos
        await odooGateway.Received(1).GetProductsByIdsAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.Count == 2 && ids.Contains(3) && ids.Contains(4)),
            Arg.Any<CancellationToken>());

        // Assert - Totales (IVA 15% incluido, descuento 10% Strategy, redondeo por línea):
        //   Subtotal 35.00 | Descuento 3.50 | A: 13.50 -> base 11.74 + IVA 1.76 | B: 18.00 -> base 15.65 + IVA 2.35
        result.SubTotal.Should().Be(35.00m);
        result.DiscountAmount.Should().Be(3.50m);
        result.TaxAmount.Should().Be(4.11m);
        result.TotalAmount.Should().Be(31.50m);
        result.Lines.Should().HaveCount(2);
        result.Lines.Select(l => l.UnitPrice).Should().Equal(15.00m, 20.00m);

        // Assert - State: Odoo recibe la orden Confirmed y el agregado termina Synced
        stateWhenSent.Should().Be("Confirmed");
        orderSentToOdoo!.State.StateName.Should().Be("Synced");
        orderSentToOdoo.NetSubTotal.Should().Be(27.39m);
        result.State.Should().Be("Synced");
        result.OdooOrderId.Should().Be(101);
        result.OdooOrderReference.Should().Be("00002-001-0001");

        // Assert - Idempotencia: el ID de la orden (uuid enviado a Odoo) se deriva de la Idempotency-Key
        result.OrderId.Should().Be(Guid.Parse(idempotencyKey));
        await idempotencyStore.Received(1).SaveKeyAsync(idempotencyKey, result.OrderId, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await historyRepo.Received(1).SaveSaleAsync(Arg.Is<SaleOrderResponseDto>(s => s.OrderId == result.OrderId), Arg.Any<CancellationToken>());
    }
}
