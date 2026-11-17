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
using Pos.Infrastructure;
using Pos.Infrastructure.Persistence;
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
        odooGateway.GetActiveSessionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
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
            .WithMessage("*cerrada o inactiva*");
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
        odooGateway.GetActiveSessionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
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
        odooGateway.GetActiveSessionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
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
        odooGateway.GetActiveSessionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
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
        odooGateway.GetActiveSessionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
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
        odooGateway.GetActiveSessionAsync(sessionId, Arg.Any<CancellationToken>())
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
        odooGateway.GetActiveSessionAsync(sessionId, Arg.Any<CancellationToken>())
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
            .WithMessage("*cerrada o inactiva*");
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
            .WithMessage("*cerrada o inactiva*");
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

    // =========================================================================
    // P28: SearchProductsUseCase propaga criterio de búsqueda a Gateway (Catálogo 1M+)
    // =========================================================================
    [Fact]
    public async Task P28_SearchProductsUseCase_PropagatesCriteriaToGateway()
    {
        var gateway = Substitute.For<IOdooPosGateway>();
        var sampleProducts = new List<ProductDto>
        {
            new(18, "Producto 18", 10.0m, "BC18", 5, "CODE18", 0.15m)
        };
        gateway.SearchProductsPagedAsync("18", 1, 8, "ID", false, Arg.Any<CancellationToken>())
            .Returns((sampleProducts, 1));

        var useCase = new SearchProductsUseCase(gateway);
        var (items, total) = await useCase.ExecutePagedAsync("18", 1, 8, "ID");

        await gateway.Received(1).SearchProductsPagedAsync("18", 1, 8, "ID", false, Arg.Any<CancellationToken>());
        items.Should().HaveCount(1);
        total.Should().Be(1);
        items[0].Id.Should().Be(18);
    }

    [Fact]
    public async Task P28_B_SearchProductsUseCase_PropagatesOnlyInStockToGateway()
    {
        var gateway = Substitute.For<IOdooPosGateway>();
        var sampleProducts = new List<ProductDto>
        {
            new(18, "Producto 18", 10.0m, "BC18", 5, "CODE18", 0.15m)
        };
        gateway.SearchProductsPagedAsync("18", 1, 8, "ID", true, Arg.Any<CancellationToken>())
            .Returns((sampleProducts, 1));

        var useCase = new SearchProductsUseCase(gateway);
        var (items, total) = await useCase.ExecutePagedAsync("18", 1, 8, "ID", onlyInStock: true);

        await gateway.Received(1).SearchProductsPagedAsync("18", 1, 8, "ID", true, Arg.Any<CancellationToken>());
        items.Should().HaveCount(1);
        total.Should().Be(1);
    }

    // =========================================================================
    // P29: SearchCustomersUseCase propaga criterio de búsqueda a Gateway (Catálogo 1M+)
    // =========================================================================
    [Fact]
    public async Task P29_SearchCustomersUseCase_PropagatesCriteriaToGateway()
    {
        var gateway = Substitute.For<IOdooPosGateway>();
        var sampleCustomers = new List<CustomerDto>
        {
            new(15, "Cliente 15", "1712345678", "c15@test.com", "Av. 15", "099123456")
        };
        gateway.SearchCustomersPagedAsync("15", 1, 8, "id", Arg.Any<CancellationToken>())
            .Returns((sampleCustomers, 1));

        var useCase = new SearchCustomersUseCase(gateway);
        var (items, total) = await useCase.ExecutePagedAsync("15", 1, 8, "id");

        await gateway.Received(1).SearchCustomersPagedAsync("15", 1, 8, "id", Arg.Any<CancellationToken>());
        items.Should().HaveCount(1);
        total.Should().Be(1);
        items[0].Id.Should().Be(15);
    }

    // =========================================================================
    // P30: GetSalesPagedAsync optimiza rendimiento sin deserializar líneas (Lazy Loading)
    // mientras GetSaleByIdAsync deserializa las líneas completas bajo demanda
    // =========================================================================
    [Fact]
    public async Task P30_SqliteSaleHistoryRepository_GetSalesPagedAsync_LazyLoadsLines_EmptyInSummary_LoadedInDetail()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<Pos.Infrastructure.Persistence.PosDbContext>()
            .UseSqlite(connection)
            .Options;

        using var dbContext = new Pos.Infrastructure.Persistence.PosDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var repo = new Pos.Infrastructure.Persistence.SqliteSaleHistoryRepository(dbContext);

        var orderId = Guid.NewGuid();
        var lines = new List<SaleOrderLineDto>
        {
            new(1, "Producto Test", 2, 10m, 20m, "P-001", 0.15m, 17.39m, 2.61m, 20m)
        };
        var sale = new SaleOrderResponseDto(
            orderId,
            4,
            "1712345678",
            "Cliente Test",
            17.39m,
            0m,
            2.61m,
            20m,
            "Synced",
            "260-1-000001",
            1,
            DateTime.UtcNow,
            lines,
            SessionName: "Caja Principal"
        );

        await repo.SaveSaleAsync(sale);

        // Act 1: GetSalesPagedAsync (listado de órdenes)
        var (items, totalCount) = await repo.GetSalesPagedAsync(null, 1, 10);

        // Assert 1: Lines debe estar vacío para evitar deserialización pesada y consumo innecesario de ancho de banda
        items.Should().HaveCount(1);
        totalCount.Should().Be(1);
        items[0].Lines.Should().BeEmpty();

        // Act 2: GetSaleByIdAsync (detalle bajo demanda)
        var detail = await repo.GetSaleByIdAsync(orderId);

        // Assert 2: Detail debe contener las líneas completas
        detail.Should().NotBeNull();
        detail!.Lines.Should().HaveCount(1);
        detail.Lines[0].ProductName.Should().Be("Producto Test");
    }

    // =========================================================================
    // P31: GetSalesPagedAsync filtra por SESSION (numérico o texto por coincidencia)
    // =========================================================================
    [Fact]
    public async Task P31_SqliteSaleHistoryRepository_GetSalesPagedAsync_FiltersBySession()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<Pos.Infrastructure.Persistence.PosDbContext>()
            .UseSqlite(connection)
            .Options;

        using var dbContext = new Pos.Infrastructure.Persistence.PosDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var repo = new Pos.Infrastructure.Persistence.SqliteSaleHistoryRepository(dbContext);

        var saleSession4 = new SaleOrderResponseDto(
            Guid.NewGuid(), 4, "1712345678", "Cliente A", 10m, 0m, 1.5m, 11.5m, "Synced", "260-1-000001", 1,
            DateTime.UtcNow, new List<SaleOrderLineDto>(), SessionName: "Caja Principal"
        );
        var saleSession5 = new SaleOrderResponseDto(
            Guid.NewGuid(), 5, "1712345679", "Cliente B", 20m, 0m, 3.0m, 23.0m, "Synced", "260-2-000002", 2,
            DateTime.UtcNow, new List<SaleOrderLineDto>(), SessionName: "Furniture Shop"
        );

        await repo.SaveSaleAsync(saleSession4);
        await repo.SaveSaleAsync(saleSession5);

        // Act 1: Buscar por ID de sesión numérico "4"
        var (byNumericId, countNumeric) = await repo.GetSalesPagedAsync(null, 1, 10, search: "4", filter: "SESSION");
        byNumericId.Should().HaveCount(1);
        byNumericId[0].PosSessionId.Should().Be(4);
        countNumeric.Should().Be(1);

        // Act 2: Buscar por nombre de sesión "Furniture"
        var (byName, countName) = await repo.GetSalesPagedAsync(null, 1, 10, search: "Furniture", filter: "SESSION");
        byName.Should().HaveCount(1);
        byName[0].PosSessionId.Should().Be(5);
        countName.Should().Be(1);

        // Act 3: Buscar sesión inexistente
        var (byNone, countNone) = await repo.GetSalesPagedAsync(null, 1, 10, search: "NonExistent", filter: "SESSION");
        byNone.Should().BeEmpty();
        countNone.Should().Be(0);
    }

    // =========================================================================
    // P32: Cajero reasignado en Odoo ERP mientras está en turno lanza excepción
    //      (Validación de Titularidad de Caja en Caliente / Zero-Trust)
    // =========================================================================
    [Fact]
    public async Task P32_CreateSale_CajeroReasignadoEnOdoo_LanzaExcepcion()
    {
        // Arrange
        var odooGateway = Substitute.For<IOdooPosGateway>();
        var idempotencyStore = Substitute.For<IIdempotencyStore>();
        var historyRepo = Substitute.For<ISaleHistoryRepository>();

        // La caja #1 en Odoo fue reasignada al usuario 10 ("Maria Lopez")
        var activeSessionReassigned = new PosSessionDto(1, "POS Central", "opened", UserId: 10, IsOpen: true, UserName: "Maria Lopez");
        odooGateway.GetActiveSessionAsync(1, Arg.Any<CancellationToken>())
            .Returns(activeSessionReassigned);

        var useCase = new CreateSaleUseCase(odooGateway, idempotencyStore, historyRepo);
        var command = new CreateSaleCommand(
            IdempotencyKey: "key-p32-reassigned",
            PosSessionId: 1,
            CustomerDocument: "1712345678",
            CustomerName: "Consumidor Final",
            DiscountType: "NONE",
            DiscountValue: 0m,
            Items: new List<CreateSaleItemDto> { new(1, 1) },
            UserId: 5, // Cajero autenticado original con ID 5
            OperatorName: "Juan Perez",
            IsAdmin: false
        );

        // Act
        var act = () => useCase.ExecuteAsync(command);

        // Assert: Zero-Trust rechaza la venta porque la caja ya no pertenece a este operador
        await act.Should().ThrowAsync<ApplicationValidationException>()
            .WithMessage("*ya no está asignada al cajero 'Juan Perez'*")
            .WithMessage("*El responsable actual asignado en Odoo ERP es 'Maria Lopez'*")
            .WithMessage("*Por favor cierre sesión y vuelva a ingresar*");
    }

    // =========================================================================
    // P33: Administrador con rol privilegiado puede operar en caja reasignada
    // =========================================================================
    [Fact]
    public async Task P33_CreateSale_AdministradorEnCajaReasignada_PermiteVenta()
    {
        // Arrange
        var odooGateway = Substitute.For<IOdooPosGateway>();
        var idempotencyStore = Substitute.For<IIdempotencyStore>();
        var historyRepo = Substitute.For<ISaleHistoryRepository>();

        // La caja #1 en Odoo está asignada a otro cajero (UserId: 10, "Maria Lopez")
        var activeSession = new PosSessionDto(1, "POS Central", "opened", UserId: 10, IsOpen: true, UserName: "Maria Lopez");
        odooGateway.GetActiveSessionAsync(1, Arg.Any<CancellationToken>())
            .Returns(activeSession);

        odooGateway.GetProductsByIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ProductDto>
            {
                new(1, "Teclado USB", 20.00m, "TEC-01", 10m, TaxRate: 0.15m, PriceIncludesTax: false)
            });

        odooGateway.ResolvePaymentMethodIdAsync(1, "cash", Arg.Any<CancellationToken>())
            .Returns(1);

        odooGateway.SyncSaleOrderAsync(Arg.Any<SaleOrder>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new OdooSyncResultDto(true, "POS/2026/00999", 999, null));

        var useCase = new CreateSaleUseCase(
            odooGateway,
            idempotencyStore,
            historyRepo,
            new IPaymentProcessor[] { new CashPaymentProcessor(odooGateway) });

        var command = new CreateSaleCommand(
            IdempotencyKey: "key-p33-admin",
            PosSessionId: 1,
            CustomerDocument: "1712345678",
            CustomerName: "Consumidor Final",
            DiscountType: "NONE",
            DiscountValue: 0m,
            Items: new List<CreateSaleItemDto> { new(1, 1) },
            PaymentMethod: "Efectivo",
            UserId: 99, // Administrador autenticado (ID 99 != 10)
            OperatorName: "Admin General",
            IsAdmin: true // Privilegio administrativo
        );

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert: El administrador completa la venta exitosamente
        result.Should().NotBeNull();
        result.State.Should().Be("Synced");
        result.OdooOrderReference.Should().Be("POS/2026/00999");
        await odooGateway.Received(1).SyncSaleOrderAsync(Arg.Any<SaleOrder>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // P33: GetSalesPagedAsync con filtro ALL incluye nombre de sesión/caja e ID numérico de sesión
    // =========================================================================
    [Fact]
    public async Task P33_SqliteSaleHistoryRepository_GetSalesPagedAsync_FiltersByAll_IncludesSessionAndBoxName()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<Pos.Infrastructure.Persistence.PosDbContext>()
            .UseSqlite(connection)
            .Options;

        using var dbContext = new Pos.Infrastructure.Persistence.PosDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var repo = new Pos.Infrastructure.Persistence.SqliteSaleHistoryRepository(dbContext);

        var saleSession4 = new SaleOrderResponseDto(
            Guid.Parse("a0000000-0000-0000-0000-000000000001"), 4, "1712345678", "Cliente Alfa", 10m, 0m, 1.5m, 11.5m, "Synced", "REF-001", 1,
            DateTime.UtcNow, new List<SaleOrderLineDto>(), SessionName: "Caja Principal"
        );
        var saleSession5 = new SaleOrderResponseDto(
            Guid.Parse("b0000000-0000-0000-0000-000000000002"), 5, "1799999999", "Cliente Beta", 20m, 0m, 3.0m, 23.0m, "Synced", "REF-002", 2,
            DateTime.UtcNow, new List<SaleOrderLineDto>(), SessionName: "Furniture Shop"
        );

        await repo.SaveSaleAsync(saleSession4);
        await repo.SaveSaleAsync(saleSession5);

        // 1. Buscar por nombre de sesión (ej. "Furniture") con filter: "ALL"
        var (byNameAll, countNameAll) = await repo.GetSalesPagedAsync(null, 1, 10, search: "Furniture", filter: "ALL");
        byNameAll.Should().HaveCount(1);
        byNameAll[0].PosSessionId.Should().Be(5);
        countNameAll.Should().Be(1);

        // 2. Buscar por nombre de sesión con filter: null (por defecto actúa como ALL)
        var (byNameNull, countNameNull) = await repo.GetSalesPagedAsync(null, 1, 10, search: "Principal", filter: null);
        byNameNull.Should().HaveCount(1);
        byNameNull[0].PosSessionId.Should().Be(4);
        countNameNull.Should().Be(1);

        // 3. Buscar por ID numérico de sesión (ej. "4") con filter: "ALL"
        var (byIdAll, countIdAll) = await repo.GetSalesPagedAsync(null, 1, 10, search: "4", filter: "ALL");
        byIdAll.Should().HaveCount(1);
        byIdAll[0].PosSessionId.Should().Be(4);
        countIdAll.Should().Be(1);

        // 4. Buscar por cliente bajo "ALL"
        var (byCustomer, countCustomer) = await repo.GetSalesPagedAsync(null, 1, 10, search: "Alfa", filter: "ALL");
        byCustomer.Should().HaveCount(1);
        byCustomer[0].CustomerName.Should().Be("Cliente Alfa");
        countCustomer.Should().Be(1);

        // 5. Buscar por documento de cliente bajo "ALL"
        var (byDoc, countDoc) = await repo.GetSalesPagedAsync(null, 1, 10, search: "1799999999", filter: "ALL");
        byDoc.Should().HaveCount(1);
        byDoc[0].CustomerName.Should().Be("Cliente Beta");
        countDoc.Should().Be(1);

        // 6. Buscar por referencia Odoo bajo "ALL"
        var (byRef, countRef) = await repo.GetSalesPagedAsync(null, 1, 10, search: "REF-001", filter: "ALL");
        byRef.Should().HaveCount(1);
        byRef[0].OdooOrderReference.Should().Be("REF-001");
        countRef.Should().Be(1);
    }

    // =========================================================================
    // P34: FTS5 (trigram) soporta caracteres especiales ('/', '-', ':', '*', '"', '(', ')')
    //      y preserva semántica exacta de prefijo para OrderId / CustomerDocument
    // =========================================================================
    [Fact]
    public async Task P34_SqliteSaleHistoryRepository_Fts5Trigram_SearchesSpecialCharsAndPreservesPrefixSemantics()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite(connection)
            .Options;

        using var dbContext = new PosDbContext(options);
        dbContext.EnsurePosSchemaAndFtsInitialized();
        // Verificar idempotencia de inicialización
        dbContext.EnsurePosSchemaAndFtsInitialized();

        var repo = new SqliteSaleHistoryRepository(dbContext);

        var guid1 = Guid.Parse("11111111-aaaa-bbbb-cccc-000000000001");
        var guid2 = Guid.Parse("22222222-dddd-eeee-ffff-000000000002");
        var guid3 = Guid.Parse("33333333-4444-5555-6666-000000000003");

        await repo.SaveSaleAsync(new SaleOrderResponseDto(
            guid1, 7, "1712345678", "Jose Esteban Salazar", 10m, 0m, 1.5m, 11.5m, "Synced",
            "260-1-000003", 1, DateTime.UtcNow.AddMinutes(-3), new List<SaleOrderLineDto>(),
            SessionName: "Caja 3 - Esteban/00007"));

        await repo.SaveSaleAsync(new SaleOrderResponseDto(
            guid2, 4, "9999999999999", "Consumidor Final", 20m, 0m, 3.0m, 23.0m, "Synced",
            "POS/2026-01:*(test)\"a", 2, DateTime.UtcNow.AddMinutes(-2), new List<SaleOrderLineDto>(),
            SessionName: "POS/0001"));

        await repo.SaveSaleAsync(new SaleOrderResponseDto(
            guid3, 8, "0101712345", "Carlos Mendoza", 30m, 0m, 4.5m, 34.5m, "Synced",
            "REF-999", 3, DateTime.UtcNow.AddMinutes(-1), new List<SaleOrderLineDto>(),
            SessionName: "Caja - 3 / Principal"));

        // 1. Búsqueda "esteban" (insensible a mayúsculas)
        var (estebanItems, estebanCount) = await repo.GetSalesPagedAsync(null, 1, 10, search: "esteban", filter: "ALL");
        estebanCount.Should().Be(1);
        estebanItems.Should().ContainSingle(s => s.OrderId == guid1);

        // 2. Búsqueda con '/' ("POS/0001") en ALL y SESSION
        var (posItems, posCount) = await repo.GetSalesPagedAsync(null, 1, 10, search: "POS/0001", filter: "SESSION");
        posCount.Should().Be(1);
        posItems.Should().ContainSingle(s => s.OrderId == guid2);

        // 3. Búsqueda con '-' ("Caja - 3") sin syntax error de FTS5
        var (cajaItems, cajaCount) = await repo.GetSalesPagedAsync(null, 1, 10, search: "Caja - 3", filter: "SESSION");
        cajaCount.Should().Be(1);
        cajaItems.Should().ContainSingle(s => s.OrderId == guid3);

        // 4. Búsqueda con caracteres especiales : * ( ) " / - sin syntax error
        var (specialItems, specialCount) = await repo.GetSalesPagedAsync(null, 1, 10, search: ":*(test)\"a", filter: "REFERENCE");
        specialCount.Should().Be(1);
        specialItems.Should().ContainSingle(s => s.OrderId == guid2);

        var (parenItems, parenCount) = await repo.GetSalesPagedAsync(null, 1, 10, search: "()*-/:\"", filter: "ALL");
        parenCount.Should().Be(0);
        parenItems.Should().BeEmpty();

        // 5. Semántica de prefijo estricta para CustomerDocument y OrderId (sin falsos positivos en medio)
        var (docPrefixItems, docPrefixCount) = await repo.GetSalesPagedAsync(null, 1, 10, search: "171", filter: "CUSTOMER");
        docPrefixCount.Should().Be(1);
        docPrefixItems.Should().ContainSingle(s => s.OrderId == guid1);

        var (midGuidItems, midGuidCount) = await repo.GetSalesPagedAsync(null, 1, 10, search: "aaaa-bbbb", filter: "REFERENCE");
        midGuidCount.Should().Be(0);
        midGuidItems.Should().BeEmpty();

        // 6. Término inexistente devuelve vacío inmediatamente
        var (noneItems, noneCount) = await repo.GetSalesPagedAsync(null, 1, 10, search: "InexistenteXYZ", filter: "ALL");
        noneCount.Should().Be(0);
        noneItems.Should().BeEmpty();
    }

    // =========================================================================
    // P35: Protección contra alta cardinalidad en FTS5 (>= 20,000 coincidencias)
    //      evita materializar cientos de miles de rowids y pagina tanto en skip bajo como profundo
    // =========================================================================
    [Fact]
    public async Task P35_SqliteSaleHistoryRepository_HighCardinality_HandlesOver20000RowsAccurately()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite(connection)
            .Options;

        using var dbContext = new PosDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        // Insertar 20,500 registros en transacción antes de inicializar FTS5 para probar rebuild
        const int totalSeeded = 20_500;
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        using (var tx = connection.BeginTransaction())
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO "SaleAuditRecords"
                    ("OrderId", "PosSessionId", "SessionName", "OperatorName", "CustomerDocument", "CustomerName",
                     "SubTotal", "DiscountAmount", "DiscountPolicyName", "TaxAmount", "TotalAmount",
                     "PaymentMethod", "AmountPaid", "State", "OdooOrderReference", "OdooOrderId",
                     "OrderLinesJson", "SyncedAtUtc")
                VALUES
                    (@orderId, 4, 'POS/0001', 'Cajero POS', '9999999999999', @customerName,
                     '10.0', '0.0', 'Sin Descuento', '1.5', '11.5',
                     'Efectivo', '11.5', 'Synced', @ref, @odooId,
                     '[{"ProductId":1,"ProductName":"ItemPesado"}]', @syncedAt);
                """;

            var pOrderId = cmd.CreateParameter(); pOrderId.ParameterName = "@orderId"; cmd.Parameters.Add(pOrderId);
            var pCustName = cmd.CreateParameter(); pCustName.ParameterName = "@customerName"; cmd.Parameters.Add(pCustName);
            var pRef = cmd.CreateParameter(); pRef.ParameterName = "@ref"; cmd.Parameters.Add(pRef);
            var pOdooId = cmd.CreateParameter(); pOdooId.ParameterName = "@odooId"; cmd.Parameters.Add(pOdooId);
            var pSyncedAt = cmd.CreateParameter(); pSyncedAt.ParameterName = "@syncedAt"; cmd.Parameters.Add(pSyncedAt);

            for (var i = 1; i <= totalSeeded; i++)
            {
                pOrderId.Value = Guid.NewGuid().ToString().ToUpperInvariant();
                pCustName.Value = i == 42 ? "Cliente Especial Esteban" : $"Cliente Generico {i}";
                pRef.Value = $"POS-{i:D6}";
                pOdooId.Value = i;
                pSyncedAt.Value = baseTime.AddSeconds(i).ToString("yyyy-MM-dd HH:mm:ss");
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        // Rebuild inicial de FTS5 + verificación idempotente
        dbContext.EnsurePosSchemaAndFtsInitialized();
        dbContext.EnsurePosSchemaAndFtsInitialized();

        var repo = new SqliteSaleHistoryRepository(dbContext);

        // 1. Alta cardinalidad con skip bajo (página 1): usa IX_SaleAuditRecords_SyncedAtUtc
        var (topPage, topCount) = await repo.GetSalesPagedAsync(null, page: 1, pageSize: 5, search: "POS/0001", filter: "ALL");
        topCount.Should().Be(totalSeeded);
        topPage.Should().HaveCount(5);
        topPage[0].OdooOrderReference.Should().Be($"POS-{totalSeeded:D6}");
        topPage[4].OdooOrderReference.Should().Be($"POS-{totalSeeded - 4:D6}");
        topPage.Should().OnlyContain(s => s.Lines.Count == 0);

        // 2. Alta cardinalidad con skip profundo (page=2500, pageSize=5 => skip=12,495 >= 10,000)
        var (deepPage, deepCount) = await repo.GetSalesPagedAsync(null, page: 2500, pageSize: 5, search: "Cliente", filter: "CUSTOMER");
        deepCount.Should().Be(totalSeeded);
        deepPage.Should().HaveCount(5);
        deepPage[0].OdooOrderReference.Should().Be($"POS-{totalSeeded - 12495:D6}");

        // 3. Búsqueda específica en medio de 20,500 filas (baja cardinalidad FTS5)
        var (singlePage, singleCount) = await repo.GetSalesPagedAsync(null, page: 1, pageSize: 5, search: "Esteban", filter: "CUSTOMER");
        singleCount.Should().Be(1);
        singlePage.Should().ContainSingle(s => s.OdooOrderReference == "POS-000042");
    }

    // =========================================================================
    // P36: Fallback seguro para términos < 3 caracteres e interceptor de PRAGMAs SQLite
    // =========================================================================
    [Fact]
    public async Task P36_SqliteSaleHistoryRepository_ShortTermFallback_And_PragmaInterceptor()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"pos_p36_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<PosDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .AddInterceptors(SqlitePragmaConnectionInterceptor.Instance)
                .Options;

            using var dbContext = new PosDbContext(options);
            dbContext.EnsurePosSchemaAndFtsInitialized();

            // Verificar PRAGMAs aplicados por SqlitePragmaConnectionInterceptor al abrir EF Core la conexión
            var connection = dbContext.Database.GetDbConnection();
            await connection.OpenAsync();
            try
            {
                using var pragmaCmd = connection.CreateCommand();
                pragmaCmd.CommandText = "PRAGMA cache_size;";
                Convert.ToInt64(pragmaCmd.ExecuteScalar()).Should().Be(-24576);

                pragmaCmd.CommandText = "PRAGMA temp_store;";
                Convert.ToInt64(pragmaCmd.ExecuteScalar()).Should().Be(2); // MEMORY

                pragmaCmd.CommandText = "PRAGMA synchronous;";
                Convert.ToInt64(pragmaCmd.ExecuteScalar()).Should().Be(1); // NORMAL
            }
            finally
            {
                await connection.CloseAsync();
            }

            var repo = new SqliteSaleHistoryRepository(dbContext);
            await repo.SaveSaleAsync(new SaleOrderResponseDto(
                Guid.NewGuid(), 4, "1712345678", "Ana Maria", 10m, 0m, 1.5m, 11.5m, "Synced",
                "260-1-000001", 1, DateTime.UtcNow.AddMinutes(-1), new List<SaleOrderLineDto>(),
                SessionName: "POS/0001"));
            await repo.SaveSaleAsync(new SaleOrderResponseDto(
                Guid.NewGuid(), 9, "0998765432", "Luis Torres", 15m, 0m, 2.25m, 17.25m, "Synced",
                "260-2-000002", 2, DateTime.UtcNow, new List<SaleOrderLineDto>(),
                SessionName: "Caja 9"));

            // Término < 3 caracteres ("17"): fallback seguro con prefijo en CustomerDocument
            var (byDocPrefix, docCount) = await repo.GetSalesPagedAsync(null, 1, 10, search: "17", filter: "CUSTOMER");
            docCount.Should().Be(1);
            byDocPrefix.Should().ContainSingle(s => s.CustomerName == "Ana Maria");

            // Término < 3 caracteres ("9"): coincide con PosSessionId == 9 en SESSION
            var (bySessionId, sessionCount) = await repo.GetSalesPagedAsync(null, 1, 10, search: "9", filter: "SESSION");
            sessionCount.Should().Be(1);
            bySessionId.Should().ContainSingle(s => s.PosSessionId == 9);

            // Término < 3 caracteres sin coincidencias ("zz"): retorna vacío sin error
            var (noMatch, noMatchCount) = await repo.GetSalesPagedAsync(null, 1, 10, search: "zz", filter: "ALL");
            noMatchCount.Should().Be(0);
            noMatch.Should().BeEmpty();
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(tempDbPath))
            {
                File.Delete(tempDbPath);
            }
        }
    }

    // =========================================================================
    // P37: OdooJson2PosAdapter - Búsqueda dividida indexada para filtro "ALL"
    // =========================================================================
    [Fact]
    public async Task P37_OdooJson2PosAdapter_AllCriteria_ZeroMatches_ReturnsEmptyWithoutSearchRead()
    {
        var recordedCalls = new System.Collections.Concurrent.ConcurrentBag<(string Path, string Body)>();
        var handler = new MockOdooHttpMessageHandler((path, body) =>
        {
            recordedCalls.Add((path, body));
            if (path.EndsWith("/product.product/search_count", StringComparison.Ordinal))
            {
                return "0";
            }
            throw new InvalidOperationException($"Llamada inesperada: {path}");
        });

        var adapter = CreateOdooAdapter(handler);
        var (items, totalCount) = await adapter.SearchProductsPagedAsync("inexistente", page: 1, pageSize: 8, criteria: "ALL");

        items.Should().BeEmpty();
        totalCount.Should().Be(0);
        recordedCalls.Should().HaveCount(2); // Solo nameDomain + codeDomain search_count
        recordedCalls.Should().OnlyContain(c => c.Path.EndsWith("/search_count", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P37_B_OdooJson2PosAdapter_AllCriteria_NameOnlyMatch_ReadsDirectlyWithoutIdUnion()
    {
        var recordedCalls = new System.Collections.Concurrent.ConcurrentBag<(string Path, string Body)>();
        var handler = new MockOdooHttpMessageHandler((path, body) =>
        {
            recordedCalls.Add((path, body));
            if (path.EndsWith("/product.product/search_count", StringComparison.Ordinal))
            {
                return body.Contains("\"name\"", StringComparison.Ordinal) ? "1" : "0";
            }
            if (path.EndsWith("/product.product/search_read", StringComparison.Ordinal))
            {
                return """
                    [
                      {
                        "id": 7,
                        "name": "Comestibles",
                        "default_code": false,
                        "lst_price": 5.50,
                        "barcode": false,
                        "qty_available": 25.0,
                        "taxes_id": []
                      }
                    ]
                    """;
            }
            throw new InvalidOperationException($"Llamada inesperada: {path}");
        });

        var adapter = CreateOdooAdapter(handler);
        var (items, totalCount) = await adapter.SearchProductsPagedAsync("comestibles", page: 1, pageSize: 8, criteria: "ALL");

        totalCount.Should().Be(1);
        items.Should().ContainSingle(p => p.Id == 7 && p.Name == "Comestibles");
        recordedCalls.Should().NotContain(c => c.Path.EndsWith("/product.product/search", StringComparison.Ordinal));
        recordedCalls.Count(c => c.Path.EndsWith("/product.product/search_read", StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact]
    public async Task P37_C_OdooJson2PosAdapter_AllCriteria_CodeOnlyMatch_ReadsDirectlyWithoutIdUnion()
    {
        var recordedCalls = new System.Collections.Concurrent.ConcurrentBag<(string Path, string Body)>();
        var handler = new MockOdooHttpMessageHandler((path, body) =>
        {
            recordedCalls.Add((path, body));
            if (path.EndsWith("/product.product/search_count", StringComparison.Ordinal))
            {
                return body.Contains("\"default_code\"", StringComparison.Ordinal) ? "1" : "0";
            }
            if (path.EndsWith("/product.product/search_read", StringComparison.Ordinal))
            {
                return """
                    [
                      {
                        "id": 55,
                        "name": "Teclado Mecánico",
                        "default_code": "SKU-55",
                        "lst_price": 45.00,
                        "barcode": "7860055",
                        "qty_available": 10.0,
                        "taxes_id": []
                      }
                    ]
                    """;
            }
            throw new InvalidOperationException($"Llamada inesperada: {path}");
        });

        var adapter = CreateOdooAdapter(handler);
        var (items, totalCount) = await adapter.SearchProductsPagedAsync("SKU-55", page: 1, pageSize: 8, criteria: "ALL");

        totalCount.Should().Be(1);
        items.Should().ContainSingle(p => p.Id == 55 && p.DefaultCode == "SKU-55");
        recordedCalls.Should().NotContain(c => c.Path.EndsWith("/product.product/search", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P37_D_OdooJson2PosAdapter_AllCriteria_MultipleBranchesMatch_DeduplicatesUnionAndPaginates()
    {
        var recordedCalls = new System.Collections.Concurrent.ConcurrentBag<(string Path, string Body)>();
        var handler = new MockOdooHttpMessageHandler((path, body) =>
        {
            recordedCalls.Add((path, body));
            if (path.EndsWith("/product.product/search_count", StringComparison.Ordinal))
            {
                if (body.Contains("\"name\"", StringComparison.Ordinal)) return "2";
                if (body.Contains("\"default_code\"", StringComparison.Ordinal)) return "2";
                if (body.Contains("\"id\"", StringComparison.Ordinal)) return "1";
                return "0";
            }
            if (path.EndsWith("/product.product/search", StringComparison.Ordinal))
            {
                if (body.Contains("\"name\"", StringComparison.Ordinal)) return "[42, 100]";
                if (body.Contains("\"default_code\"", StringComparison.Ordinal)) return "[42, 50]";
                if (body.Contains("\"id\"", StringComparison.Ordinal)) return "[42]";
                return "[]";
            }
            if (path.EndsWith("/product.product/search_read", StringComparison.Ordinal))
            {
                // Página 1 con pageSize=2 debe pedir [42, 50]
                body.Should().Contain("42");
                body.Should().Contain("50");
                body.Should().NotContain("100");
                return """
                    [
                      { "id": 50, "name": "Cable 42", "default_code": "C-42", "lst_price": 3.0, "barcode": false, "qty_available": 4.0, "taxes_id": [] },
                      { "id": 42, "name": "Producto 42", "default_code": "42-A", "lst_price": 9.0, "barcode": false, "qty_available": 8.0, "taxes_id": [] }
                    ]
                    """;
            }
            throw new InvalidOperationException($"Llamada inesperada: {path}");
        });

        var adapter = CreateOdooAdapter(handler);
        var (items, totalCount) = await adapter.SearchProductsPagedAsync("42", page: 1, pageSize: 2, criteria: "ALL");

        totalCount.Should().Be(3); // Unión sin duplicados de {42, 50, 100}
        items.Select(i => i.Id).Should().Equal(42, 50);
    }

    [Fact]
    public async Task P37_E_OdooJson2PosAdapter_AllCriteria_HighCardinality_FallsBackToStandardDomain()
    {
        var recordedCalls = new System.Collections.Concurrent.ConcurrentBag<(string Path, string Body)>();
        var handler = new MockOdooHttpMessageHandler((path, body) =>
        {
            recordedCalls.Add((path, body));
            if (path.EndsWith("/product.product/search_count", StringComparison.Ordinal))
            {
                // Primeros conteos divididos: name > 5000 y code > 0; luego el fallback devuelve 6000
                if (body.Contains("\"name\"", StringComparison.Ordinal) && body.Contains("\"default_code\"", StringComparison.Ordinal))
                    return "6000";
                if (body.Contains("\"name\"", StringComparison.Ordinal))
                    return "6000";
                if (body.Contains("\"default_code\"", StringComparison.Ordinal))
                    return "10";
                return "0";
            }
            if (path.EndsWith("/product.product/search_read", StringComparison.Ordinal))
            {
                return """
                    [
                      { "id": 1, "name": "Producto 1", "default_code": "P1", "lst_price": 1.0, "barcode": false, "qty_available": 5.0, "taxes_id": [] }
                    ]
                    """;
            }
            throw new InvalidOperationException($"Llamada inesperada: {path}");
        });

        var adapter = CreateOdooAdapter(handler);
        var (items, totalCount) = await adapter.SearchProductsPagedAsync("Producto", page: 1, pageSize: 8, criteria: "ALL");

        totalCount.Should().Be(6000);
        items.Should().ContainSingle(p => p.Id == 1);
        recordedCalls.Should().NotContain(c => c.Path.EndsWith("/product.product/search", StringComparison.Ordinal));
    }

    // =========================================================================
    // P38: OdooJson2PosAdapter - Optimizaciones P2 (Cortocircuito, Tope 10000, Caché, =like, order, cleanNum)
    // =========================================================================
    [Fact]
    public async Task P38_A_SearchProductsPaged_Page1ShortCircuit_SkipsSearchCountAndCachesTotal()
    {
        var recordedCalls = new System.Collections.Concurrent.ConcurrentBag<(string Path, string Body)>();
        var handler = new MockOdooHttpMessageHandler((path, body) =>
        {
            recordedCalls.Add((path, body));
            if (path.EndsWith("/product.product/search_read", StringComparison.Ordinal))
            {
                body.Should().Contain("\"order\":\"id asc\"");
                return """
                    [
                      { "id": 10, "name": "Cafe", "default_code": "C10", "lst_price": 2.5, "barcode": false, "qty_available": 12.0, "taxes_id": [1] },
                      { "id": 11, "name": "Cafe Doble", "default_code": "C11", "lst_price": 3.5, "barcode": false, "qty_available": 8.0, "taxes_id": [1] }
                    ]
                    """;
            }
            if (path.EndsWith("/account.tax/search_read", StringComparison.Ordinal))
            {
                return """
                    [
                      { "id": 1, "amount": 15.0, "amount_type": "percent", "price_include": true }
                    ]
                    """;
            }
            throw new InvalidOperationException($"No debía llamar search_count en cortocircuito page=1: {path}");
        });

        var adapter = CreateOdooAdapter(handler);
        var (items1, total1) = await adapter.SearchProductsPagedAsync("Cafe", page: 1, pageSize: 8, criteria: "NAME");
        var (items2, total2) = await adapter.SearchProductsPagedAsync("Cafe", page: 1, pageSize: 8, criteria: "NAME");

        total1.Should().Be(2);
        total2.Should().Be(2);
        items1.Should().HaveCount(2);
        items1[0].TaxRate.Should().Be(0.15m);
        items2.Should().HaveCount(2);
        recordedCalls.Should().NotContain(c => c.Path.EndsWith("/search_count", StringComparison.Ordinal));
        // account.tax se consulta una sola vez gracias a la caché en memoria
        recordedCalls.Count(c => c.Path.EndsWith("/account.tax/search_read", StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact]
    public async Task P38_B_SearchProductsPaged_FullPage1_CapsSearchCountAt10000AndCaches()
    {
        var recordedCalls = new System.Collections.Concurrent.ConcurrentBag<(string Path, string Body)>();
        var handler = new MockOdooHttpMessageHandler((path, body) =>
        {
            recordedCalls.Add((path, body));
            if (path.EndsWith("/product.product/search_read", StringComparison.Ordinal))
            {
                return """
                    [
                      { "id": 1, "name": "Item 1", "default_code": "I1", "lst_price": 1.0, "barcode": false, "qty_available": 5.0, "taxes_id": [] },
                      { "id": 2, "name": "Item 2", "default_code": "I2", "lst_price": 1.0, "barcode": false, "qty_available": 5.0, "taxes_id": [] }
                    ]
                    """;
            }
            if (path.EndsWith("/product.product/search_count", StringComparison.Ordinal))
            {
                if (body.Contains("\"Item\"", StringComparison.Ordinal))
                {
                    body.Should().Contain("\"limit\":10000");
                    return "10000";
                }

                body.Should().NotContain("\"limit\"");
                return "1000006";
            }
            if (path.EndsWith("/res.users/search_read", StringComparison.Ordinal))
                return """[{ "id": 1, "partner_id": [3, "Admin"] }]""";
            if (path.EndsWith("/res.company/search_read", StringComparison.Ordinal))
                return """[{ "id": 1, "partner_id": [1, "My Company"] }]""";
            if (path.EndsWith("/res.partner/search", StringComparison.Ordinal))
                return "[7]";
            if (path.EndsWith("/res.partner/search_read", StringComparison.Ordinal))
            {
                return """
                    [
                      { "id": 20, "name": "Ana Lopez", "vat": "1712345678", "email": "ana@test.com", "street": "Av 1", "phone": "099" },
                      { "id": 21, "name": "Luis Perez", "vat": "1712345679", "email": "luis@test.com", "street": "Av 2", "phone": "098" }
                    ]
                    """;
            }
            if (path.EndsWith("/res.partner/search_count", StringComparison.Ordinal))
            {
                body.Should().NotContain("\"limit\"");
                return "1000007";
            }
            throw new InvalidOperationException($"Llamada inesperada: {path}");
        });

        var adapter = CreateOdooAdapter(handler);

        // 1. Búsqueda activa en Productos ("Item", criteria: "NAME") sí aplica limit:10000 y cachea 10000
        var (_, searchTotal1) = await adapter.SearchProductsPagedAsync("Item", page: 1, pageSize: 2, criteria: "NAME");
        var (_, searchTotal2) = await adapter.SearchProductsPagedAsync("Item", page: 2, pageSize: 2, criteria: "NAME");
        searchTotal1.Should().Be(10000);
        searchTotal2.Should().Be(10000);

        // 2. Búsqueda vacía en Productos NO envía limit, devuelve 1000006 y cachea 30s
        var (_, emptyProdTotal1) = await adapter.SearchProductsPagedAsync("", page: 1, pageSize: 2, criteria: "ALL");
        var (_, emptyProdTotal2) = await adapter.SearchProductsPagedAsync("", page: 2, pageSize: 2, criteria: "ALL");
        emptyProdTotal1.Should().Be(1000006);
        emptyProdTotal2.Should().Be(1000006);

        // 3. Búsqueda vacía en Clientes NO envía limit, devuelve 1000007 y cachea 30s
        var (_, emptyCustTotal1) = await adapter.SearchCustomersPagedAsync("", page: 1, pageSize: 2, criteria: "ALL");
        var (_, emptyCustTotal2) = await adapter.SearchCustomersPagedAsync("", page: 2, pageSize: 2, criteria: "ALL");
        emptyCustTotal1.Should().Be(1000007);
        emptyCustTotal2.Should().Be(1000007);

        // Cada caso ejecutó search_count exactamente 1 vez (página 2 reutiliza caché TTL 30s)
        recordedCalls.Count(c => c.Path.EndsWith("/product.product/search_count", StringComparison.Ordinal)).Should().Be(2);
        recordedCalls.Count(c => c.Path.EndsWith("/res.partner/search_count", StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact]
    public async Task P38_C_SearchCustomersPaged_UsesExcludedCache_VatPrefixLike_OrderAndNoCustomerRank()
    {
        var recordedCalls = new System.Collections.Concurrent.ConcurrentQueue<(string Path, string Body)>();
        var handler = new MockOdooHttpMessageHandler((path, body) =>
        {
            recordedCalls.Enqueue((path, body));
            if (path.EndsWith("/res.users/search_read", StringComparison.Ordinal))
                return """[{ "id": 1, "partner_id": [3, "Admin"] }]""";
            if (path.EndsWith("/res.company/search_read", StringComparison.Ordinal))
                return """[{ "id": 1, "partner_id": [1, "My Company"] }]""";
            if (path.EndsWith("/res.partner/search", StringComparison.Ordinal))
                return "[7]";
            if (path.EndsWith("/res.partner/search_read", StringComparison.Ordinal))
            {
                body.Should().NotContain("customer_rank");
                body.Should().NotContain("user_ids");
                body.Should().NotContain("ref_company_ids");
                body.Should().NotContain("not ilike");
                body.Should().Contain("\"not in\",[1,3,7]");
                return """
                    [
                      { "id": 20, "name": "Ana Lopez", "vat": "1712345678", "email": "ana@test.com", "street": "Av 1", "phone": "099" }
                    ]
                    """;
            }
            if (path.EndsWith("/res.partner/search_count", StringComparison.Ordinal))
            {
                body.Should().Contain("\"limit\":10000");
                return "1";
            }
            throw new InvalidOperationException($"Llamada inesperada: {path}");
        });

        var adapter = CreateOdooAdapter(handler);

        // 1. Consulta con query vacía: no debe enviar order="id asc" y debe hacer cortocircuito en page=1
        var (emptyItems, emptyTotal) = await adapter.SearchCustomersPagedAsync("", page: 1, pageSize: 8, criteria: "all");
        emptyItems.Should().HaveCount(1);
        emptyTotal.Should().Be(1);

        // 2. Consulta VAT con prefijo de 2 dígitos: usa =like "17%" y order="id asc"
        await adapter.SearchCustomersPagedAsync("17", page: 1, pageSize: 8, criteria: "vat");

        // 3. Consulta ALL numérica "17": usa =like "17%" para vat y order="id asc"
        await adapter.SearchCustomersPagedAsync("17", page: 1, pageSize: 8, criteria: "all");

        var searchReads = recordedCalls.Where(c => c.Path.EndsWith("/res.partner/search_read", StringComparison.Ordinal)).ToList();
        searchReads.Should().HaveCount(3);
        searchReads[0].Body.Should().NotContain("\"order\":\"id asc\"");
        searchReads[1].Body.Should().Contain("\"vat\",\"=like\",\"17%\"");
        searchReads[1].Body.Should().Contain("\"order\":\"id asc\"");
        searchReads[2].Body.Should().Contain("\"vat\",\"=like\",\"17%\"");
        searchReads[2].Body.Should().Contain("\"order\":\"id asc\"");

        // Los IDs excluidos se consultan 1 sola vez gracias a la caché de 5 min
        recordedCalls.Count(c => c.Path.EndsWith("/res.users/search_read", StringComparison.Ordinal)).Should().Be(1);
        recordedCalls.Count(c => c.Path.EndsWith("/res.company/search_read", StringComparison.Ordinal)).Should().Be(1);
        recordedCalls.Count(c => c.Path.EndsWith("/res.partner/search", StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact]
    public async Task P38_D_CleanNum_DoesNotCorruptWordsContainingIdInMiddle()
    {
        var capturedBodies = new System.Collections.Concurrent.ConcurrentBag<string>();
        var handler = new MockOdooHttpMessageHandler((path, body) =>
        {
            capturedBodies.Add(body);
            if (path.EndsWith("/product.product/search_count", StringComparison.Ordinal)) return "0";
            if (path.EndsWith("/product.product/search_read", StringComparison.Ordinal)) return "[]";
            if (path.EndsWith("/res.partner/search_read", StringComparison.Ordinal)) return "[]";
            // Simular mock sin handlers de excluidos para verificar fallback limpio
            throw new InvalidOperationException($"No mockeado: {path}");
        });

        var adapter = CreateOdooAdapter(handler);

        // "comida" y "bebida" NO deben convertirse en "coma" / "beba" ni disparar rama ID;
        // "12id3" NO debe convertirse en ID 123
        await adapter.SearchProductsPagedAsync("comida", page: 1, pageSize: 8, criteria: "ID");
        await adapter.SearchProductsPagedAsync("12id3", page: 1, pageSize: 8, criteria: "ID");
        await adapter.SearchProductsPagedAsync("id:42", page: 1, pageSize: 8, criteria: "ID");
        await adapter.SearchCustomersPagedAsync("bebida", page: 1, pageSize: 8, criteria: "id");
        await adapter.SearchCustomersPagedAsync("#99", page: 1, pageSize: 8, criteria: "id");

        capturedBodies.Should().Contain(b => b.Contains("\"id\",\"=\",42"));
        capturedBodies.Should().Contain(b => b.Contains("\"id\",\"=\",99"));
        capturedBodies.Should().NotContain(b => b.Contains("\"id\",\"=\",123"));
        capturedBodies.Should().NotContain(b => b.Contains("coma", StringComparison.OrdinalIgnoreCase));
        capturedBodies.Should().NotContain(b => b.Contains("beba", StringComparison.OrdinalIgnoreCase));
    }

    private static Pos.Infrastructure.Odoo.OdooJson2PosAdapter CreateOdooAdapter(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var options = Microsoft.Extensions.Options.Options.Create(new Pos.Infrastructure.Odoo.OdooOptions
        {
            BaseUrl = "http://localhost:8069",
            Database = "odoo_test",
            ApiKey = "test-api-key"
        });
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<Pos.Infrastructure.Odoo.OdooJson2PosAdapter>.Instance;
        return new Pos.Infrastructure.Odoo.OdooJson2PosAdapter(httpClient, options, logger);
    }

    private sealed class MockOdooHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<string, string, string> _responder;

        public MockOdooHttpMessageHandler(Func<string, string, string> responder)
        {
            _responder = responder;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is not null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : string.Empty;
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var json = _responder(path, body);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
