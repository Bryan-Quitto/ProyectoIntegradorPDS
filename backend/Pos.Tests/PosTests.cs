using Microsoft.EntityFrameworkCore;
using NSubstitute.ExceptionExtensions;
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
    // RF-08 / RN-04: Rechazo de política de descuento no autorizada con mensaje claro
    // [GUÍA ESTEBAN - FIX AUDITORÍA ELI/VERO]: Antes caía silenciosamente a NoDiscountPolicy
    // ocultando peticiones maliciosas o tipos no soportados.
    // =========================================================================
    [Theory]
    [InlineData("HACK_DISCOUNT")]
    [InlineData("VIP_ILLEGAL")]
    [InlineData("DESCUENTO_MAGICO")]
    public async Task RF08_PoliticaDescuentoNoAutorizada_LanzaDomainValidationException(string unauthorizedType)
    {
        // Arrange
        var odooGateway = Substitute.For<IOdooPosGateway>();
        var idempotencyStore = Substitute.For<IIdempotencyStore>();
        var historyRepo = Substitute.For<ISaleHistoryRepository>();

        odooGateway.GetActiveSessionAsync(Arg.Any<CancellationToken>())
            .Returns(new PosSessionDto(1, "Main POS", "opened", 2, IsOpen: true));

        var useCase = new CreateSaleUseCase(odooGateway, idempotencyStore, historyRepo);
        var command = new CreateSaleCommand(
            IdempotencyKey: "key-rf08",
            PosSessionId: 1,
            CustomerDocument: "1712345678",
            CustomerName: "Juan Perez",
            DiscountType: unauthorizedType,
            DiscountValue: 10m,
            Items: new List<CreateSaleItemDto> { new(1, 1) }
        );

        // Act
        var act = () => useCase.ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*no está autorizada*");
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
    // P10 (Turno 3 - Esteban): Extensibilidad de Métodos de Pago (Principio Abierto/Cerrado - OCP)
    // [GUÍA ESTEBAN - TURNO 3 - RF-10 / P10 / SOLID]:
    // Demuestra matemáticamente que se puede registrar e inyectar un nuevo procesador de pago
    // (ej. Bitcoin Lightning) sin modificar una sola línea de código en 'CreateSaleUseCase'.
    // =========================================================================
    [Fact]
    public async Task P10_ExtensibilidadMetodosPago_PermiteNuevaEstrategiaSinModificarCasoDeUso()
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
                new(1, "Teclado Mecánico", 50.00m, "TEC-01", 10m)
            });

        odooGateway.SyncSaleOrderAsync(Arg.Any<SaleOrder>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new OdooSyncResultDto(true, "POS/2026/00100", 100, null));

        // Inyectamos una estrategia personalizada que no existía en el diseño original: Cripto / Bitcoin
        var customCryptoProcessor = new MockCustomCryptoPaymentProcessor();
        var processors = new IPaymentProcessor[]
        {
            new CashPaymentProcessor(odooGateway),
            customCryptoProcessor
        };

        var useCase = new CreateSaleUseCase(odooGateway, idempotencyStore, historyRepo, processors);
        var command = new CreateSaleCommand(
            IdempotencyKey: "esteban-p10-crypto-key",
            PosSessionId: 1,
            CustomerDocument: "1712345678",
            CustomerName: "Esteban Villalba",
            DiscountType: "NONE",
            DiscountValue: 0m,
            Items: new List<CreateSaleItemDto> { new(1, 1) },
            PaymentMethod: "CRYPTO_BTC"
        );

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert: Se procesó mediante la nueva estrategia sin alterar el caso de uso
        result.Should().NotBeNull();
        result.PaymentMethod.Should().Be("Bitcoin Lightning");
        result.OdooOrderReference.Should().Be("POS/2026/00100");
        customCryptoProcessor.WasExecuted.Should().BeTrue();
    }

    private sealed class MockCustomCryptoPaymentProcessor : IPaymentProcessor
    {
        public bool WasExecuted { get; private set; }
        public string MethodName => "Bitcoin Lightning";

        public bool CanHandle(string paymentMethod) =>
            string.Equals(paymentMethod, "CRYPTO_BTC", StringComparison.OrdinalIgnoreCase);

        public Task<PaymentProcessingResult> ProcessPaymentAsync(
            SaleOrder saleOrder,
            int posSessionId,
            CancellationToken cancellationToken = default)
        {
            WasExecuted = true;
            return Task.FromResult(new PaymentProcessingResult(
                Success: true,
                MethodName: "Bitcoin Lightning",
                AmountPaid: saleOrder.TotalAmount,
                ResolvedOdooMethodId: 999,
                TransactionReference: "BTC-TX-883921"
            ));
        }
    }

    // =========================================================================
    // P11, P12, P22, P23: AutenticaciÃ³n de Operadores / Vendedores con Odoo 20 (Fase 4)
    // =========================================================================
    [Fact]
    public async Task P11_LoginOperador_CredencialesValidas_GeneraTokenCorrectamente()
    {
        // Arrange
        var odooGateway = Substitute.For<IOdooPosGateway>();
        var jwtProvider = Substitute.For<IJwtProvider>();

        odooGateway.AuthenticateUserAsync("cashier1", "securePassword123", Arg.Any<CancellationToken>())
            .Returns(new OdooUserAuthDto(2, "Cajero Principal", "cashier1", "Cashier", 4, "Caja Principal"));

        jwtProvider.GenerateToken("cashier1", "Cajero Principal", "Cashier", 4, "Caja Principal", 2)
            .Returns("jwt.mock.token.xyz");

        var useCase = new LoginOperatorUseCase(odooGateway, jwtProvider);
        var command = new LoginCommand("cashier1", "securePassword123");

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result!.Token.Should().Be("jwt.mock.token.xyz");
        result.Username.Should().Be("cashier1");
        result.FullName.Should().Be("Cajero Principal");
        result.Role.Should().Be("Cashier");
        result.PosSessionId.Should().Be(4);
        result.PosConfigName.Should().Be("Caja Principal");
    }

    [Fact]
    public async Task P12_LoginOperador_CredencialesInvalidas_RetornaNull()
    {
        // Arrange
        var odooGateway = Substitute.For<IOdooPosGateway>();
        var jwtProvider = Substitute.For<IJwtProvider>();

        odooGateway.AuthenticateUserAsync("cashier1", "wrongPass", Arg.Any<CancellationToken>())
            .Returns((OdooUserAuthDto?)null);

        var useCase = new LoginOperatorUseCase(odooGateway, jwtProvider);
        var command = new LoginCommand("cashier1", "wrongPass");

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        result.Should().BeNull();
        jwtProvider.DidNotReceive().GenerateToken(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task P22_LoginOperador_SinSesionAbiertaEnOdoo_PermiteLoginConSesionNula()
    {
        // Arrange
        var odooGateway = Substitute.For<IOdooPosGateway>();
        var jwtProvider = Substitute.For<IJwtProvider>();

        odooGateway.AuthenticateUserAsync("vendedor_sin_caja", "pass123", Arg.Any<CancellationToken>())
            .Returns(new OdooUserAuthDto(10, "Vendedor Sin Caja", "vendedor_sin_caja", "Cashier", null, "Sin Caja Asignada"));

        jwtProvider.GenerateToken("vendedor_sin_caja", "Vendedor Sin Caja", "Cashier", null, "Sin Caja Asignada", 10)
            .Returns("jwt.mock.token.sin_caja");

        var useCase = new LoginOperatorUseCase(odooGateway, jwtProvider);
        var command = new LoginCommand("vendedor_sin_caja", "pass123");

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert: Permite login sin caja asignada (modo consulta)
        result.Should().NotBeNull();
        result!.Token.Should().Be("jwt.mock.token.sin_caja");
        result.PosSessionId.Should().BeNull();
        result.PosConfigName.Should().Be("Sin Caja Asignada");
    }

    [Fact]
    public void P23b_JwtService_GeneratesToken_WithoutSession_EmitsEmptySessionClaim()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new Pos.Infrastructure.Security.JwtOptions
        {
            SecurityKey = "UnSecretoSuperSeguroParaElProyectoIntegradorPDS2026!ClaveLarga",
            ValidIssuer = "PosApi",
            ValidAudience = "PosWebClient",
            ExpirationMinutes = 60
        });
        var jwtService = new Pos.Infrastructure.Security.JwtService(options);

        // Act: Usuario sin caja asignada (null)
        var token = jwtService.GenerateToken("cajero_sin_caja@empresa.com", "Carlos Cajero", "Cashier", null, null, 18);

        // Assert
        token.Should().NotBeNullOrWhiteSpace();

        var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(token);

        jwt.Claims.First(c => c.Type == "pos_session_id").Value.Should().Be("");
        jwt.Claims.First(c => c.Type == "pos_config_name").Value.Should().Be("Sin Caja Asignada");
    }

    [Fact]
    public void P23_JwtService_GeneratesToken_WithAllRequiredClaims()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new Pos.Infrastructure.Security.JwtOptions
        {
            SecurityKey = "UnSecretoSuperSeguroParaElProyectoIntegradorPDS2026!ClaveLarga",
            ValidIssuer = "PosApi",
            ValidAudience = "PosWebClient",
            ExpirationMinutes = 60
        });
        var jwtService = new Pos.Infrastructure.Security.JwtService(options);

        // Act
        var token = jwtService.GenerateToken("cajero@empresa.com", "Juan Cajero", "Cashier", 7, "Caja Norte", 15);

        // Assert
        token.Should().NotBeNullOrWhiteSpace();

        var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(token);

        jwt.Issuer.Should().Be("PosApi");
        jwt.Audiences.Should().Contain("PosWebClient");
        jwt.Claims.First(c => c.Type == "FullName").Value.Should().Be("Juan Cajero");
        jwt.Claims.First(c => c.Type == "pos_session_id").Value.Should().Be("7");
        jwt.Claims.First(c => c.Type == "pos_config_name").Value.Should().Be("Caja Norte");
        jwt.Claims.First(c => c.Type == "odoo_user_id").Value.Should().Be("15");
        jwt.Claims.First(c => c.Type == "email").Value.Should().Be("cajero@empresa.com");
    }
    // =========================================================================
    // P09 (Turno 4 - Bryan): Venta Real de 2 Productos y Pago en Efectivo (Anexo F)
    // [GUÍA BRYAN - TURNO 4 - QA / ANEXO F / INTEGRACIÓN ATÓMICA SIN PARCHES]:
    // Demuestra que la solución en .NET ejecuta la venta atómica y transaccional
    // de 2 productos ($15 + $20 = $35) en efectivo, con IVA 15% ($4.57) y conciliación
    // directa en Odoo ERP 20 vía pos.order.sync_from_ui sin parches manuales 'write'.
    // =========================================================================
    [Fact]
    public async Task P09_VentaDosProductosPagoEfectivo_ConciliadaSinParchesManuales_AnexoF()
    {
        // Arrange
        var odooGateway = Substitute.For<IOdooPosGateway>();
        var idempotencyStore = Substitute.For<IIdempotencyStore>();
        var historyRepo = Substitute.For<ISaleHistoryRepository>();

        const int sessionId = 4;
        var idempotencyKey = Guid.NewGuid().ToString();

        odooGateway.GetActiveSessionAsync(Arg.Any<CancellationToken>())
            .Returns(new PosSessionDto(sessionId, "My Company/00002", "opened", 2, IsOpen: true));

        // Productos configurados exactamente como en Anexo F:
        // Producto A (ID 3, $15.00, IVA 15% incl.)
        // Producto B (ID 4, $20.00, IVA 15% incl.)
        odooGateway.GetProductsByIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ProductDto>
            {
                new(3, "Producto A", 15.00m, "", 100m, "PROD-001", TaxRate: 0.15m, PriceIncludesTax: true, TaxIds: new[] { 1 }),
                new(4, "Producto B", 20.00m, "", 100m, "PROD-002", TaxRate: 0.15m, PriceIncludesTax: true, TaxIds: new[] { 1 })
            });

        odooGateway.ResolvePaymentMethodIdAsync(sessionId, "cash", Arg.Any<CancellationToken>())
            .Returns(1); // Método ID 1: Efectivo

        SaleOrder? orderSentToOdoo = null;
        string? stateWhenSent = null;
        int resolvedMethodReceived = 0;

        odooGateway.SyncSaleOrderAsync(Arg.Any<SaleOrder>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                orderSentToOdoo = ci.Arg<SaleOrder>();
                resolvedMethodReceived = ci.Arg<int>();
                stateWhenSent = orderSentToOdoo.State.StateName;
                return new OdooSyncResultDto(true, "260-1-000003", 5, null);
            });

        var useCase = new CreateSaleUseCase(
            odooGateway,
            idempotencyStore,
            historyRepo,
            new IPaymentProcessor[] { new CashPaymentProcessor(odooGateway) });

        var command = new CreateSaleCommand(
            IdempotencyKey: idempotencyKey,
            PosSessionId: sessionId,
            CustomerDocument: "1712345678",
            CustomerName: "Juan Perez",
            DiscountType: "NONE",
            DiscountValue: 0m,
            Items: new List<CreateSaleItemDto> { new(3, 1), new(4, 1) },
            PaymentMethod: "Efectivo"
        );

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert - Anexo F Valores Exactos:
        // Subtotal = $35.00 (Prod A $15.00 + Prod B $20.00)
        // Descuento = $0.00
        // Base Imponible (NetSubTotal) = $30.43 ($13.04 + $17.39)
        // IVA 15% (TaxAmount) = $4.57 ($1.96 + $2.61)
        // Total = $35.00
        // Pago = $35.00
        result.SubTotal.Should().Be(35.00m);
        result.DiscountAmount.Should().Be(0.00m);
        result.TaxAmount.Should().Be(4.57m);
        result.TotalAmount.Should().Be(35.00m);
        result.AmountPaid.Should().Be(35.00m);
        result.PaymentMethod.Should().Be("Efectivo");
        resolvedMethodReceived.Should().Be(1);

        // Assert - Trazabilidad y Estado Odoo
        result.State.Should().Be("Synced");
        result.OdooOrderId.Should().Be(5);
        result.OdooOrderReference.Should().Be("260-1-000003");

        // Assert - Inmutabilidad y Transacción Atómica
        stateWhenSent.Should().Be("Confirmed");
        orderSentToOdoo!.State.StateName.Should().Be("Synced");
        orderSentToOdoo.NetSubTotal.Should().Be(30.43m);

        // Assert - Persistencia Local (SQLite)
        await idempotencyStore.Received(1).SaveKeyAsync(idempotencyKey, result.OrderId, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await historyRepo.Received(1).SaveSaleAsync(Arg.Is<SaleOrderResponseDto>(s => s.OrderId == result.OrderId), Arg.Any<CancellationToken>());
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

    // =========================================================================
    // P14: Consulta de Venta por ID con Detalle Completo (RF-14)
    // =========================================================================
    [Fact]
    public async Task P14_GetSaleById_ExistingOrder_ReturnsFullDetailDto()
    {
        // Arrange
        var historyRepo = Substitute.For<ISaleHistoryRepository>();
        var orderId = Guid.NewGuid();

        var detailDto = new SaleOrderResponseDto(
            OrderId: orderId,
            PosSessionId: 4,
            CustomerDocument: "1712345678",
            CustomerName: "Juan Perez",
            SubTotal: 35.00m,
            DiscountAmount: 3.50m,
            TaxAmount: 4.11m,
            TotalAmount: 31.50m,
            State: "Synced",
            OdooOrderReference: "260-1-000003",
            OdooOrderId: 5,
            CreatedAtUtc: DateTime.UtcNow,
            Lines: new List<SaleOrderLineDto>
            {
                new(3, "Producto A", 1, 15.00m, 15.00m, ProductCode: "PROD-001", TaxRate: 0.15m, NetSubTotal: 11.74m, TaxAmount: 1.76m, TotalWithTax: 13.50m),
                new(4, "Producto B", 1, 20.00m, 20.00m, ProductCode: "PROD-002", TaxRate: 0.15m, NetSubTotal: 15.65m, TaxAmount: 2.35m, TotalWithTax: 18.00m)
            },
            DiscountPolicyName: "Descuento Porcentual (10%)",
            PaymentMethod: "Efectivo",
            AmountPaid: 31.50m,
            SessionName: "My Company/00002",
            OperatorName: "Cajero POS"
        );

        historyRepo.GetSaleByIdAsync(orderId, Arg.Any<CancellationToken>())
            .Returns(detailDto);

        var useCase = new GetSaleByIdUseCase(historyRepo);

        // Act
        var result = await useCase.ExecuteAsync(orderId.ToString());

        // Assert
        result.Should().NotBeNull();
        result!.OrderId.Should().Be(orderId);
        result.OdooOrderReference.Should().Be("260-1-000003");
        result.DiscountPolicyName.Should().Be("Descuento Porcentual (10%)");
        result.PaymentMethod.Should().Be("Efectivo");
        result.Lines.Should().HaveCount(2);
        result.Lines[0].ProductCode.Should().Be("PROD-001");
        result.Lines[1].ProductCode.Should().Be("PROD-002");
    }

    // =========================================================================
    // RN-06: Inmutabilidad de la Orden Confirmada y Sincronizada (Patrón State - Turno 2 Vero)
    // =========================================================================
    [Fact]
    public void RN06_OrdenConfirmada_LanzaSaleOrderStateException_AlIntentarModificarLineasOPoliticas()
    {
        // Arrange: Crear orden y agregar un producto en estado Draft
        var order = new SaleOrder(posSessionId: 1);
        var productA = new Product(1, "Producto A", new Money(15.00m));
        var productB = new Product(2, "Producto B", new Money(20.00m));
        order.AddLine(productA, new Quantity(1));

        // Act: Confirmar la orden (transición Draft -> Confirmed)
        order.Confirm();
        order.State.StateName.Should().Be("Confirmed");

        // Assert: RN-06 Inmutabilidad estricta en ConfirmedState
        // 1. Prohibido agregar líneas
        var actAdd = () => order.AddLine(productB, new Quantity(2));
        actAdd.Should().Throw<SaleOrderStateException>()
            .WithMessage("*RN-06*");

        // 2. Prohibido eliminar líneas
        var actRemove = () => order.RemoveLine(1);
        actRemove.Should().Throw<SaleOrderStateException>()
            .WithMessage("*RN-06*");

        // 3. Prohibido alterar políticas de descuento
        var actDiscount = () => order.SetDiscountPolicy(new PercentageDiscountPolicy(10m));
        actDiscount.Should().Throw<SaleOrderStateException>()
            .WithMessage("*RN-06*");

        // 4. Prohibido re-confirmar
        var actReconfirm = () => order.Confirm();
        actReconfirm.Should().Throw<SaleOrderStateException>()
            .WithMessage("*ya se encuentra confirmada*");
    }

    [Fact]
    public void RN06_OrdenSincronizadaConOdoo_LanzaSaleOrderStateException_AlIntentarModificarOReconfirmar()
    {
        // Arrange: Crear orden, agregar producto, confirmar y marcar como sincronizada con Odoo
        var order = new SaleOrder(posSessionId: 1);
        var productA = new Product(1, "Producto A", new Money(15.00m));
        order.AddLine(productA, new Quantity(1));
        order.Confirm();
        order.MarkAsSynced("260-1-000005", 105);

        // Act & Assert
        order.State.StateName.Should().Be("Synced");
        order.OdooOrderReference.Should().Be("260-1-000005");
        order.OdooOrderId.Should().Be(105);

        // 1. Prohibido agregar líneas una vez sincronizada
        var actAdd = () => order.AddLine(productA, new Quantity(1));
        actAdd.Should().Throw<SaleOrderStateException>()
            .WithMessage("*RN-06*");

        // 2. Prohibido eliminar líneas
        var actRemove = () => order.RemoveLine(1);
        actRemove.Should().Throw<SaleOrderStateException>()
            .WithMessage("*RN-06*");

        // 3. Prohibido cambiar descuentos
        var actDiscount = () => order.SetDiscountPolicy(new NoDiscountPolicy());
        actDiscount.Should().Throw<SaleOrderStateException>()
            .WithMessage("*RN-06*");

        // 4. Prohibido re-confirmar o re-sincronizar
        var actConfirm = () => order.Confirm();
        actConfirm.Should().Throw<SaleOrderStateException>()
            .WithMessage("*ya fue sincronizada*");

        var actResync = () => order.MarkAsSynced("260-1-000005", 105);
        actResync.Should().Throw<SaleOrderStateException>()
            .WithMessage("*ya fue sincronizada*");
    }

    // =========================================================================
    // P20: Consulta de Clientes Odoo en res.partner (Punto 5)
    // =========================================================================
    [Fact]
    public async Task P20_SearchCustomersUseCase_DelegatesToGateway()
    {
        var gateway = Substitute.For<IOdooPosGateway>();
        var sampleCustomers = new List<CustomerDto>
        {
            new(10, "Juan Perez", "1712345678", "juan@test.com", "Av. Amazonas", "0991234567"),
            new(11, "Empresa ABC Cía. Ltda.", "1790012345001", "contacto@abc.com", "Av. Shyris", "022345678")
        };
        gateway.SearchCustomersAsync("Perez", Arg.Any<CancellationToken>())
            .Returns(sampleCustomers);

        var useCase = new SearchCustomersUseCase(gateway);
        var result = await useCase.ExecuteAsync("Perez");

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Juan Perez");
        result[0].Vat.Should().Be("1712345678");
        result[1].Vat.Should().Be("1790012345001");
    }

    // =========================================================================
    // P21: Builder Odoo incluye partner_id al asociar cliente (Punto 5)
    // =========================================================================
    [Fact]
    public void P21_OdooPosOrderPayloadBuilder_IncludesPartnerId_WhenSpecified()
    {
        var order = new SaleOrder(
            posSessionId: 1,
            customerDocument: "1712345678",
            customerName: "Juan Perez",
            partnerId: 42);

        var product = new Product(1, "Producto Test", new Money(10m), "123", 100, new TaxRule(0.15m, true, new[] { 1 }));
        order.AddLine(product, new Quantity(1));

        var payload = new Pos.Infrastructure.Odoo.OdooPosOrderPayloadBuilder()
            .FromSaleOrder(order)
            .WithPayment(7)
            .Build();

        payload.Should().ContainKey("partner_id");
        payload["partner_id"].Should().Be(42);
    }

    // =========================================================================
    // P22: Aislamiento de Historial de Ventas por Caja/Sesión (Least Privilege)
    // =========================================================================
    [Fact]
    public async Task P22_GetSalesHistoryUseCase_FiltersBySessionId_WhenCashierSessionProvided()
    {
        var historyRepo = Substitute.For<ISaleHistoryRepository>();
        const int cashierSessionId = 4;
        var filteredSales = new List<SaleOrderResponseDto>
        {
            new(Guid.NewGuid(), cashierSessionId, "1712345678", "Juan Perez", 20m, 0m, 3m, 23m, "Synced", "260-1-000001", 1, DateTime.UtcNow, new List<SaleOrderLineDto>())
        };

        historyRepo.GetSalesAsync(cashierSessionId, Arg.Any<CancellationToken>())
            .Returns(filteredSales);

        var useCase = new GetSalesHistoryUseCase(historyRepo);

        // Act
        var result = await useCase.ExecuteAsync(cashierSessionId);

        // Assert
        result.Should().HaveCount(1);
        result[0].PosSessionId.Should().Be(cashierSessionId);
        await historyRepo.Received(1).GetSalesAsync(cashierSessionId, Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // P23: Historial Consolidado para Administrador (posSessionId = null)
    // =========================================================================
    [Fact]
    public async Task P23_GetSalesHistoryUseCase_ReturnsAllSales_WhenAdminRequestsConsolidatedHistory()
    {
        var historyRepo = Substitute.For<ISaleHistoryRepository>();
        var allSales = new List<SaleOrderResponseDto>
        {
            new(Guid.NewGuid(), 4, "1712345678", "Cliente 1", 20m, 0m, 3m, 23m, "Synced", "260-1-000001", 1, DateTime.UtcNow, new List<SaleOrderLineDto>()),
            new(Guid.NewGuid(), 5, "1790012345001", "Cliente 2", 50m, 5m, 6.75m, 51.75m, "Synced", "260-2-000001", 2, DateTime.UtcNow, new List<SaleOrderLineDto>())
        };

        historyRepo.GetSalesAsync(null, Arg.Any<CancellationToken>())
            .Returns(allSales);

        var useCase = new GetSalesHistoryUseCase(historyRepo);

        // Act
        var result = await useCase.ExecuteAsync(null);

        // Assert
        result.Should().HaveCount(2);
        await historyRepo.Received(1).GetSalesAsync(null, Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // P24: Repositorio SQLite aísla estrictamente registros por PosSessionId
    // =========================================================================
    [Fact]
    public async Task P24_SqliteSaleHistoryRepository_GetSalesAsync_FiltersAccuratelyByPosSessionId()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<Pos.Infrastructure.Persistence.PosDbContext>()
            .UseSqlite(connection)
            .Options;

        using var dbContext = new Pos.Infrastructure.Persistence.PosDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var repo = new Pos.Infrastructure.Persistence.SqliteSaleHistoryRepository(dbContext);

        // Seed 2 sales in session 4, 1 sale in session 7
        var saleSession4A = new SaleOrderResponseDto(Guid.NewGuid(), 4, "1712345678", "Cliente A", 10m, 0m, 1.5m, 11.5m, "Synced", "260-1-000001", 1, DateTime.UtcNow.AddMinutes(-10), new List<SaleOrderLineDto>());
        var saleSession4B = new SaleOrderResponseDto(Guid.NewGuid(), 4, "1712345679", "Cliente B", 20m, 0m, 3.0m, 23.0m, "Synced", "260-1-000002", 2, DateTime.UtcNow.AddMinutes(-5), new List<SaleOrderLineDto>());
        var saleSession7 = new SaleOrderResponseDto(Guid.NewGuid(), 7, "1790012345001", "Cliente C", 50m, 0m, 7.5m, 57.5m, "Synced", "260-2-000001", 3, DateTime.UtcNow, new List<SaleOrderLineDto>());

        await repo.SaveSaleAsync(saleSession4A);
        await repo.SaveSaleAsync(saleSession4B);
        await repo.SaveSaleAsync(saleSession7);

        // Act: Filter by Session 4 (Cajero Caja 4)
        var session4Sales = await repo.GetSalesAsync(4);

        // Act: Filter by Session 7 (Cajero Caja 7)
        var session7Sales = await repo.GetSalesAsync(7);

        // Act: Admin query (null -> todas las órdenes)
        var allSales = await repo.GetSalesAsync(null);

        // Assert: Caja 4 solo ve sus 2 ventas
        session4Sales.Should().HaveCount(2);
        session4Sales.Should().OnlyContain(s => s.PosSessionId == 4);

        // Assert: Caja 7 solo ve su 1 venta
        session7Sales.Should().HaveCount(1);
        session7Sales[0].PosSessionId.Should().Be(7);

        // Assert: Consolidado Admin ve las 3 ventas
        allSales.Should().HaveCount(3);
    }

    // =========================================================================
    // [GUÍA ESTEBAN - TURNO 2 (SEMANA 4) - RNF-06 / CA-07 / AISLAMIENTO MULTI-CAJA]
    // P25: Rechazo de venta cuando el operador intenta registrar en una caja ajena
    // =========================================================================
    [Fact]
    public async Task P25_AislamientoCaja_CajeroNoPuedeRegistrarEnCajaAjena_LanzaApplicationValidationException()
    {
        // Arrange
        var odooGateway = Substitute.For<IOdooPosGateway>();
        var idempotencyStore = Substitute.For<IIdempotencyStore>();
        var historyRepo = Substitute.For<ISaleHistoryRepository>();

        // La caja autorizada y abierta en Odoo es la #5 (Furniture Shop)
        odooGateway.GetActiveSessionAsync(5, Arg.Any<CancellationToken>())
            .Returns(new PosSessionDto(5, "Furniture Shop", "opened", 2, IsOpen: true));

        // Pero si el cajero consulta o intenta vender en la caja #9 (Clothes Shop no autorizada)
        odooGateway.GetActiveSessionAsync(9, Arg.Any<CancellationToken>())
            .Returns((PosSessionDto?)null);

        var useCase = new CreateSaleUseCase(odooGateway, idempotencyStore, historyRepo);
        var command = new CreateSaleCommand(
            IdempotencyKey: "key-esteban-isolation-p25",
            PosSessionId: 9, // Intento de emisión en caja ajena #9
            CustomerDocument: "1710034065",
            CustomerName: "Acme Corporation",
            DiscountType: "NONE",
            DiscountValue: 0m,
            Items: new List<CreateSaleItemDto> { new(62, 1) }
        );

        // Act
        var act = () => useCase.ExecuteAsync(command);

        // Assert: El caso de uso rechaza la venta garantizando aislamiento estricto (RN-07)
        await act.Should().ThrowAsync<ApplicationValidationException>()
            .WithMessage("*sesión de POS no se encuentra abierta o no coincide*");
    }

    // =========================================================================
    // [GUÍA ESTEBAN - TURNO 2 (SEMANA 4) - RNF-06 / CA-07 / ROLES Y PRIVILEGIOS]
    // P26: Rechazo de venta cuando el operador no tiene ninguna sesión de POS activa
    // =========================================================================
    [Fact]
    public async Task P26_AislamientoCaja_OperadorSinCajaAsignada_LanzaApplicationValidationException()
    {
        // Arrange
        var odooGateway = Substitute.For<IOdooPosGateway>();
        var idempotencyStore = Substitute.For<IIdempotencyStore>();
        var historyRepo = Substitute.For<ISaleHistoryRepository>();

        // Sin sesión activa en Odoo
        odooGateway.GetActiveSessionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((PosSessionDto?)null);
        odooGateway.GetActiveSessionAsync(Arg.Any<CancellationToken>())
            .Returns((PosSessionDto?)null);

        var useCase = new CreateSaleUseCase(odooGateway, idempotencyStore, historyRepo);
        var command = new CreateSaleCommand(
            IdempotencyKey: "key-esteban-no-session-p26",
            PosSessionId: 0,
            CustomerDocument: "1710034065",
            CustomerName: "Acme Corporation",
            DiscountType: "NONE",
            DiscountValue: 0m,
            Items: new List<CreateSaleItemDto> { new(62, 1) }
        );

        // Act
        var act = () => useCase.ExecuteAsync(command);

        // Assert: No se permite operar a usuarios sin sesión activa (Principio de Menor Privilegio)
        await act.Should().ThrowAsync<ApplicationValidationException>()
            .WithMessage("*sesión de POS*");
    }

    // =========================================================================
    // [GUÍA ESTEBAN - TURNO 2 (SEMANA 4) - RNF-06 / CA-07 / CONSULTA SEGURA]
    // P27: Historial de ventas devuelve lista vacía si la sesión solicitada no tiene registros
    // =========================================================================
    [Fact]
    public async Task P27_AislamientoHistorial_CajaSinVentas_DevuelveListaVaciaSinErrores()
    {
        // Arrange
        var historyRepo = Substitute.For<ISaleHistoryRepository>();
        historyRepo.GetSalesAsync(999, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SaleOrderResponseDto>>(Array.Empty<SaleOrderResponseDto>()));

        var useCase = new GetSalesHistoryUseCase(historyRepo);

        // Act
        var result = await useCase.ExecuteAsync(999);

        // Assert: Garantiza consistencia sin nulos ni excepciones no controladas
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}
