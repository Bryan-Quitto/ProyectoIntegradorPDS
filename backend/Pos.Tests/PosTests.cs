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
        odooGateway.SearchProductsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
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

        odooGateway.SearchProductsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
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
}
