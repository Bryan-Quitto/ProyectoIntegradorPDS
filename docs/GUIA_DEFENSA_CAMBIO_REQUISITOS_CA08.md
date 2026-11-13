# 🛡️ GUÍA MAESTRA DE DEFENSA TÉCNICA ANTE CAMBIO DE REQUISITOS (CRITERIO CA-08)
## Sistema Punto de Venta (POS) con Arquitectura Limpia e Integración Odoo 20 Community
**Asignatura:** Patrones de Software — Quinto Semestre  
**Criterio de Evaluación Evaluado:** **CA-08 (Defensa Técnica Individual y Adaptación en Vivo ante Cambio de Requisitos)**  
**Marco Arquitectónico:** Clean Architecture (Robert C. Martin), Principios SOLID y Patrones de Diseño GoF  
**Responsable de Elaboración:** Bryan Lopez (Lead QA / Turno 4 - Semana 4)

---

## 🎯 1. Objetivo y Marco Conceptual para la Defensa

El Criterio de Aceptación **CA-08** establece textualmente:
> *"La defensa técnica individual demuestra comprensión profunda de la arquitectura implementada y la capacidad de responder a un cambio de requisito propuesto por el evaluador en tiempo real sin degradar el diseño."*

Esta guía proporciona **4 simulaciones completas con código exacto, justificación teórica y respuestas verbales tipo** para que cualquier integrante del equipo (*Bryan Quitto, Eli, Esteban, Vero o Bryan Lopez*) defienda con total solvencia, seguridad y precisión técnica ante el docente.

### Principios Clave a Mencionar Durante la Defensa:
1. **Regla de Dependencia de Clean Architecture:** El código del Dominio (`Pos.Domain`) no conoce frameworks, HTTP, bases de datos ni a Odoo ERP. Las dependencias siempre apuntan hacia adentro.
2. **Open/Closed Principle (OCP):** Las entidades y casos de uso están *abiertos a la extensión pero cerrados a la modificación*. Agregamos comportamientos creando nuevas clases polimórficas, no modificando clases existentes con condicionales `if/else`.
3. **Single Responsibility Principle (SRP):** Cada clase tiene una única razón para cambiar. Los controladores no calculan impuestos ni procesan pagos; delegan a casos de uso y al dominio.
4. **Interface Segregation Principle (ISP) & Dependency Inversion Principle (DIP):** Las capas de alto nivel dependen de abstracciones (`IDiscountPolicy`, `IPaymentProcessor`, `IOdooPosGateway`), nunca de implementaciones concretas.

---

## 🚀 2. Banco de Simulación: 4 Cambios de Requisitos Resueltos Paso a Paso

---

### 🟢 ESCENARIO 1: Nueva Política de Descuento (Patrón Strategy GoF)

#### Requisito Planteado por el Evaluador:
> *"La gerencia comercial ha creado una promoción VIP Gold: si una compra supera los $40.00 USD, se debe aplicar un 20% de descuento sobre el subtotal de la venta. Demuestre cómo implementarlo en el código sin modificar la entidad `SaleOrder`."*

#### Análisis Arquitectónico:
- En nuestro diseño, `SaleOrder` implementa el patrón **Strategy (GoF Comportamiento)** mediante la interfaz `IDiscountPolicy`.
- `SaleOrder` delega el cálculo a `DiscountPolicy.CalculateDiscount(subtotal)` sin conocer la regla concreta.
- Para cumplir con **OCP**, creamos una nueva clase `VipGoldDiscountPolicy` en `Pos.Domain/Strategies/`. `SaleOrder` permanece **100% inalterada**.

#### Paso 1: Implementación de la Estrategia en Dominio
Ubicación: `backend/Pos.Domain/Strategies/DiscountPolicies.cs`

```csharp
namespace Pos.Domain.Strategies;

/// <summary>
/// Política de descuento VIP Gold: 20% de descuento si la orden supera $40.00 USD.
/// Demuestra extensibilidad pura bajo el Patrón Strategy y principio OCP.
/// </summary>
public sealed class VipGoldDiscountPolicy : IDiscountPolicy
{
    private const decimal Threshold = 40.00m;
    private const decimal DiscountRate = 0.20m;

    public string PolicyName => "Descuento VIP Gold (20% superando $40.00)";

    public decimal CalculateDiscount(decimal subtotal)
    {
        if (subtotal <= 0m || subtotal < Threshold)
        {
            return 0m;
        }

        var discount = subtotal * DiscountRate;
        return decimal.Round(discount, 2, MidpointRounding.AwayFromZero);
    }
}
```

#### Paso 2: Habilitación en el Caso de Uso (Fábrica de Estrategia)
Ubicación: `backend/Pos.Application/UseCases/UseCases.cs` (dentro de `CreateSaleUseCase.cs`):

```csharp
// Se añade el nuevo caso al pattern matching sin tocar SaleOrder:
IDiscountPolicy discountPolicy = normalizedDiscountType switch
{
    "NONE" or "" => new NoDiscountPolicy(),
    "PERCENTAGE" => new PercentageDiscountPolicy(command.DiscountValue),
    "THRESHOLD_FIXED" => new ThresholdFixedDiscountPolicy(50m, command.DiscountValue > 0 ? command.DiscountValue : 5m),
    "VIP_GOLD" => new VipGoldDiscountPolicy(), // <-- NUEVA ESTRATEGIA
    _ => throw new DomainValidationException(
        nameof(command.DiscountType),
        $"La política de descuento '{command.DiscountType}' no está autorizada por el sistema (RF-08 / RN-04).")
};
```

#### Paso 3: Prueba Automatizada en xUnit (.NET 10)
Ubicación: `backend/Pos.Tests/PosTests.cs`

```csharp
[Fact]
public void CA08_Escenario1_VipGoldDiscountPolicy_Aplica20PorCiento_SoloSiSupera40Dolares()
{
    // Arrange: Orden con $50.00 de subtotal
    var policy = new VipGoldDiscountPolicy();
    var order = new SaleOrder(posSessionId: 1, discountPolicy: policy);
    order.AddLine(productId: 1, productName: "Producto VIP", 
                  quantity: new Quantity(1), unitPrice: new Money(50.00m), tax: TaxRule.EcuadorVatExcluded);

    // Act
    var discount = policy.CalculateDiscount(order.SubTotal);
    var discountPercent = order.DiscountPercent;

    // Assert: $50.00 * 20% = $10.00 de descuento
    discount.Should().Be(10.00m);
    discountPercent.Should().Be(20.00m);
    order.TotalAmount.Should().Be(46.00m); // ($50 - $10) + IVA 15% ($6.00) = $46.00
}
```

#### 🎙️ Guión Verbal ante el Docente:
> *"Ingeniero, gracias a la aplicación del patrón Strategy en la entidad `SaleOrder`, la política de cálculo está desacoplada del ciclo de vida de la orden. Implementé la clase `VipGoldDiscountPolicy` implementando la interfaz `IDiscountPolicy`. `SaleOrder` no tuvo que ser modificada en una sola línea, cumpliendo estrictamente con el Principio Abierto/Cerrado (OCP). La lógica de validación de umbral y porcentaje queda encapsulada en el Dominio, y la prueba unitaria xUnit demuestra que el cálculo y el desglose de IVA funcionan de forma determinista."*

---

### 🟢 ESCENARIO 2: Nuevo Método de Pago — Billetera Móvil DeUna (Strategy + OCP + DI)

#### Requisito Planteado por el Evaluador:
> *"El negocio desea aceptar cobros mediante la billetera digital 'DeUna' (código QR). Muestre cómo añadir este nuevo método de pago sin alterar el código de `CreateSaleUseCase`."*

#### Análisis Arquitectónico:
- En nuestro backend, `CreateSaleUseCase` recibe una colección inyectada `IEnumerable<IPaymentProcessor>`.
- Para seleccionar el procesador adecuado, consulta polimórficamente `p.CanHandle(methodName)`.
- Creamos `DeunaPaymentProcessor` implementando `IPaymentProcessor` y lo registramos en el contenedor de Inyección de Dependencias (`DependencyInjection.cs`). `CreateSaleUseCase` **permanece 100% inalterado**.

#### Paso 1: Implementación del Procesador de Pago
Ubicación: `backend/Pos.Application/UseCases/PaymentProcessors.cs`

```csharp
namespace Pos.Application.UseCases;

/// <summary>
/// Estrategia concreta de cobro mediante Billetera Móvil DeUna (QR).
/// Demuestra extensibilidad OCP en la capa de Aplicación.
/// </summary>
public class DeunaPaymentProcessor : IPaymentProcessor
{
    private readonly IOdooPosGateway _odooGateway;

    public string MethodName => "DeUna QR";

    public DeunaPaymentProcessor(IOdooPosGateway odooGateway)
    {
        _odooGateway = odooGateway;
    }

    public bool CanHandle(string paymentMethod)
    {
        if (string.IsNullOrWhiteSpace(paymentMethod)) return false;
        var m = paymentMethod.Trim().ToUpperInvariant();
        return m is "DEUNA" or "DEUNA_QR" or "BILLETERA_MOVIL";
    }

    public async Task<PaymentProcessingResult> ProcessPaymentAsync(
        SaleOrder saleOrder,
        int posSessionId,
        CancellationToken cancellationToken = default)
    {
        // En Odoo POS, los pagos digitales/bancarios se asocian al método de tipo 'bank'
        var methodId = await _odooGateway.ResolvePaymentMethodIdAsync(posSessionId, "bank", cancellationToken);
        if (methodId <= 0)
        {
            methodId = await _odooGateway.ResolvePaymentMethodIdAsync(posSessionId, "cash", cancellationToken);
        }

        var qrTransactionId = $"DEUNA-TX-{Guid.NewGuid().ToString()[..8].ToUpper()}";

        return new PaymentProcessingResult(
            Success: true,
            MethodName: "DeUna QR",
            AmountPaid: saleOrder.TotalAmount,
            ResolvedOdooMethodId: methodId,
            TransactionReference: qrTransactionId
        );
    }
}
```

#### Paso 2: Registro en Inyección de Dependencias (Cero Cambios en el Caso de Uso)
Ubicación: `backend/Pos.Infrastructure/DependencyInjection.cs`

```csharp
// Se registra en el ServiceCollection:
services.AddScoped<IPaymentProcessor, CashPaymentProcessor>();
services.AddScoped<IPaymentProcessor, BankTransferPaymentProcessor>();
services.AddScoped<IPaymentProcessor, DeunaPaymentProcessor>(); // <-- NUEVO PROCESADOR REGISTRADO
```

#### Paso 3: Prueba Automatizada en xUnit (.NET 10)
Ubicación: `backend/Pos.Tests/PosTests.cs`

```csharp
[Fact]
public async Task CA08_Escenario2_DeunaPaymentProcessor_ProcesaExitosamente_SinTocarCreateSaleUseCase()
{
    // Arrange
    var odooGateway = Substitute.For<IOdooPosGateway>();
    odooGateway.ResolvePaymentMethodIdAsync(Arg.Any<int>(), "bank", Arg.Any<CancellationToken>())
        .Returns(Task.FromResult(7)); // ID 7 en Odoo para banco/QR

    var processor = new DeunaPaymentProcessor(odooGateway);
    var order = new SaleOrder(posSessionId: 1);
    order.AddLine(1, "Prod A", new Quantity(1), new Money(10m), TaxRule.EcuadorVatExcluded);
    order.Confirm();

    // Act
    var canHandle = processor.CanHandle("DEUNA");
    var result = await processor.ProcessPaymentAsync(order, posSessionId: 1);

    // Assert
    canHandle.Should().BeTrue();
    result.Success.Should().BeTrue();
    result.MethodName.Should().Be("DeUna QR");
    result.ResolvedOdooMethodId.Should().Be(7);
    result.TransactionReference.Should().StartWith("DEUNA-TX-");
}
```

#### 🎙️ Guión Verbal ante el Docente:
> *"Ingeniero, nuestro caso de uso `CreateSaleUseCase` cumple de forma estricta con el Principio de Responsabilidad Única y Open/Closed. En lugar de un switch-case cableado con los métodos de pago, el caso de uso inyecta `IEnumerable<IPaymentProcessor>`. Para soportar DeUna, simplemente creé la clase `DeunaPaymentProcessor` y la agregué al contenedor IoC en `DependencyInjection.cs`. El caso de uso jamás se enteró de la modificación de código fuente, demostrando desacoplamiento e Inversión de Dependencias (DIP)."*

---

### 🟢 ESCENARIO 3: Nueva Regla de Negocio — Monto Máximo por Orden (Invariante de Dominio DDD)

#### Requisito Planteado por el Evaluador:
> *"Por políticas de prevención de fraudes y lavado de activos, ninguna venta individual en el POS puede exceder los $1,000.00 USD. ¿Dónde debe residir esta validación y cómo se asegura de que no se pueda eludir?"*

#### Análisis Arquitectónico:
- **Error Típico (Anti-patrón):** Colocar la validación en el Endpoint HTTP (`SaleEndpoints.cs`) o en el formulario de React. Esto crearía un *Modelo de Dominio Anémico* y permitiría eludir la regla si la orden se crea desde otra interfaz, cola o proceso batch.
- **Solución Correcta (Clean Architecture & DDD):** La regla de negocio es un **Invariante de Dominio** y debe protegerse dentro del Agregado `SaleOrder` (específicamente en el método de transición `Confirm()`).

#### Paso 1: Implementación de la Invariante en la Entidad del Dominio
Ubicación: `backend/Pos.Domain/Entities/SaleOrder.cs`

```csharp
public class SaleOrder
{
    // Constante de regla de negocio corporativa
    public const decimal MaximumOrderAmountLimit = 1000.00m;

    // ... propiedades existentes ...

    public void Confirm()
    {
        EnsureCanModify();

        if (_lines.Count == 0)
        {
            throw new DomainValidationException(nameof(Lines), "No se puede confirmar una orden sin productos (RN-01).");
        }

        // INVARIANTE DE DOMINIO: Límite de monto máximo por transacción
        if (TotalAmount > MaximumOrderAmountLimit)
        {
            throw new DomainValidationException(
                nameof(TotalAmount),
                $"El monto total de la venta (${TotalAmount:F2}) excede el límite máximo permitido de ${MaximumOrderAmountLimit:F2} USD por políticas de seguridad financiera.");
        }

        State.Confirm(this);
    }
}
```

#### Paso 2: Propagación Automática a la API (RFC 7807)
Dado que nuestro middleware [`ExceptionHandlingMiddleware.cs`](../../backend/Pos.Api/Middleware/ExceptionHandlingMiddleware.cs) captura cualquier `DomainValidationException`, esta excepción se serializa automáticamente hacia el cliente HTTP como un **ProblemDetails RFC 7807** con código de estado **400 Bad Request**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Error de validación en las reglas del dominio.",
  "status": 400,
  "detail": "El monto total de la venta ($1250.00) excede el límite máximo permitido de $1000.00 USD por políticas de seguridad financiera.",
  "instance": "/api/sales"
}
```

#### Paso 3: Prueba Automatizada en xUnit (.NET 10)
Ubicación: `backend/Pos.Tests/PosTests.cs`

```csharp
[Fact]
public void CA08_Escenario3_MontoMayorA1000Dolares_LanzaDomainValidationException_AlConfirmar()
{
    // Arrange: Crear orden con monto superior a $1000 ($1,200)
    var order = new SaleOrder(posSessionId: 1);
    order.AddLine(productId: 99, productName: "Laptop de Alta Gama", 
                  quantity: new Quantity(1), unitPrice: new Money(1200.00m), tax: TaxRule.EcuadorVatExcluded);

    // Act
    var action = () => order.Confirm();

    // Assert: La entidad del Dominio rechaza la confirmación
    action.Should().Throw<DomainValidationException>()
        .WithMessage("*excede el límite máximo permitido de $1000.00 USD*");
    
    // Verificar que la orden no cambió a estado Confirmed
    order.State.StateName.Should().Be("Draft");
}
```

#### 🎙️ Guión Verbal ante el Docente:
> *"Ingeniero, esta validación es una Invariante de Dominio en términos de Domain-Driven Design (DDD). Si la ubicáramos en el endpoint de la Minimal API o en React, estaríamos cometiendo el anti-patrón de Dominio Anémico. La validación se colocó en el método `Confirm()` de la entidad `SaleOrder`. De este modo, la entidad se encarga de proteger su propia consistencia interna sin importar si la llamada proviene de la API REST, de un comando de consola o de una prueba unitaria. El middleware de excepciones captura la `DomainValidationException` y genera la respuesta RFC 7807 con código HTTP 400."*

---

### 🟢 ESCENARIO 4: Nuevo Requisito Fiscal Local — Código de Retención SRI sin Afectar el Adaptador de Odoo

#### Requisito Planteado por el Evaluador:
> *"El SRI de Ecuador exige registrar un código de retención de impuestos (ej. Código 332 para retención en la fuente del 1.75%) en cada venta. ¿Cómo se incorpora este dato fiscal en el Dominio y el historial local sin romper ni alterar el adaptador de integración con Odoo 20?"*

#### Análisis Arquitectónico:
- En Clean Architecture, las fuentes externas (como Odoo ERP) están desacopladas del modelo conceptual interno.
- La interfaz del puerto [`IOdooPosGateway.cs`](../../backend/Pos.Application/Ports/IPorts.cs) y el constructor [`OdooPosOrderPayloadBuilder.cs`](../../backend/Pos.Infrastructure/Odoo/OdooPosOrderPayloadBuilder.cs) solo traducen los campos que el comando `pos.order.sync_from_ui` de Odoo requiere.
- Añadimos la propiedad de retención en la entidad `SaleOrder`, en los DTOs de auditoría y en SQLite, **sin modificar una sola línea de `OdooJson2PosAdapter.cs`**.

#### Paso 1: Extensión en la Entidad del Dominio
Ubicación: `backend/Pos.Domain/Entities/SaleOrder.cs`

```csharp
public class SaleOrder
{
    // ... campos existentes ...
    public string? WithholdingTaxCode { get; private set; }

    public SaleOrder(
        int posSessionId,
        string customerDocument = "9999999999999",
        string customerName = "Consumidor Final",
        IDiscountPolicy? discountPolicy = null,
        Guid? id = null,
        int? partnerId = null,
        int? userId = null,
        string? withholdingTaxCode = null) // <-- NUEVO PARÁMETRO OPCIONAL
    {
        // ... validaciones ...
        WithholdingTaxCode = string.IsNullOrWhiteSpace(withholdingTaxCode) 
            ? "RET-0" 
            : withholdingTaxCode.Trim();
    }
}
```

#### Paso 2: Por qué el Adaptador de Odoo NO sufre modificaciones
En [`OdooPosOrderPayloadBuilder.cs`](../../backend/Pos.Infrastructure/Odoo/OdooPosOrderPayloadBuilder.cs), el método `FromSaleOrder(SaleOrder order)` extrae únicamente los datos requeridos por el protocolo JSON-2 de Odoo 20:
```csharp
public OdooPosOrderPayloadBuilder FromSaleOrder(SaleOrder order)
{
    WithUuid(order.Id)
        .WithSessionId(order.PosSessionId)
        .WithDateOrder(order.CreatedAtUtc)
        .WithTotals(order.TaxAmount, order.TotalAmount)
        .WithPartnerId(order.PartnerId)
        .WithUserId(order.UserId);
    // ... itera líneas y extrae subtotales e IDs fiscales de Odoo ...
    return this;
}
```
Como el Builder utiliza mapeo selectivo mediante el Principio de Segregación de Interfaces (ISP), cualquier campo local adicional añadido a `SaleOrder` para normativa local (SRI, retenciones, facturación electrónica ecuatoriana) es totalmente transparente para el adaptador de Odoo ERP.

#### Paso 3: Prueba Automatizada en xUnit (.NET 10)
Ubicación: `backend/Pos.Tests/PosTests.cs`

```csharp
[Fact]
public void CA08_Escenario4_CodigoRetencionSri_SePreservaEnDominio_SinAlterarPayloadDeOdoo()
{
    // Arrange: Crear orden con código de retención 332 (1.75% SRI)
    var order = new SaleOrder(
        posSessionId: 1, 
        withholdingTaxCode: "SRI-332-1.75%");
    order.AddLine(1, "Servicio Profesional", new Quantity(1), new Money(100m), TaxRule.EcuadorVatExcluded);
    order.Confirm();

    // Act 1: Comprobar que el dominio almacena la retención
    order.WithholdingTaxCode.Should().Be("SRI-332-1.75%");

    // Act 2: Generar el payload de Odoo mediante el Builder
    var builder = new OdooPosOrderPayloadBuilder();
    builder.FromSaleOrder(order).WithPayment(paymentMethodId: 1);
    var payload = builder.Build();

    // Assert: El payload para Odoo sigue siendo válido y cumple el contrato oficial
    payload.Should().ContainKey("data");
    var data = payload["data"] as Dictionary<string, object>;
    data.Should().NotBeNull();
    data!["amount_total"].Should().Be(115.00m); // $100 + IVA 15%
}
```

#### 🎙️ Guión Verbal ante el Docente:
> *"Ingeniero, aquí se hace evidente la ventaja de Clean Architecture. Si nuestro software estuviera acoplado al modelo de datos de Odoo (ActiveRecord o modelo anémico compartido), cualquier requerimiento fiscal local de Ecuador nos obligaría a parchear los modelos del ERP. En nuestra solución, el Dominio es el dueño de la verdad de negocio; incorporamos el código de retención en `SaleOrder` y en la persistencia local de auditoría. El adaptador de Odoo (`OdooJson2PosAdapter`) consume el puerto mediante el `OdooPosOrderPayloadBuilder`, el cual extrae únicamente lo que Odoo 20 necesita, manteniendo intacta la estabilidad del sistema ante cambios externos."*

---

## 💡 3. Cheat Sheet de Preguntas Frecuentes del Evaluador

| Pregunta Típica del Docente | Respuesta Clave y Fundamentación Técnica |
| :--- | :--- |
| **"¿Por qué el frontend no calcula el total definitivo de la venta?"** | Porque el cliente web nunca es fuente confiable de verdad comercial ni fiscal (RN-03). El frontend calcula un total *provisional* para la experiencia de usuario (`Cart.ts`), pero el backend consulta los precios oficiales y reglas fiscales vigentes directamente en Odoo ERP al procesar la venta. |
| **"¿Qué pasa si se cae la red o el contenedor de Odoo mientras se cobra?"** | En estricto cumplimiento con la regla **RN-07** y el Criterio **CA-02**, el adaptador detecta el fallo HTTP/RPC o socket cerrado y eleva una `OdooCommunicationException`. La transacción no se confirma en SQLite y el middleware RFC 7807 retorna un código 503 con título *"Error de Comunicación con Odoo ERP"*, prohibiendo falsos éxitos en la caja. |
| **"¿Cómo impiden que un cajero haga doble cobro si presiona dos veces el botón Pagar?"** | Aplicamos el patrón **Idempotent Receiver** (RN-08 / P06). Cada intento genera un UUID `Idempotency-Key` en React. El backend almacena la clave y la respuesta JSON en SQLite (`pos_local.db`). Si llega una petición con la misma clave, el backend retorna la respuesta previamente calculada desde la caché sin reenviar la orden a Odoo ni duplicar registros. |
| **"¿Cómo aíslan las operaciones para que un cajero no vea ni venda en la caja de otro?"** | Al autenticarse en `POST /api/auth/login`, el token JWT emitido incluye los claims `pos_session_id`, `role` y `sub`. En `POST /api/sales`, el endpoint compara el `pos_session_id` del comando contra el claim del token. Si no coinciden, se rechaza inmediatamente con **HTTP 403 Forbidden** (Pruebas P25 y P26). En el historial `GET /api/sales`, el cajero solo recibe las órdenes asociadas a su sesión activa. |
| **"¿Por qué utilizaron el patrón State en lugar de un simple campo booleano o enum?"** | Un campo `status = "CONFIRMED"` permite que métodos mutadores sigan ejecutándose a menos que se llene el código de `if (status == ...)` repetitivos. Con el patrón **State** (DD-02 / RN-06), `SaleOrder` delega sus transiciones a `DraftState`, `ConfirmedState` y `SyncedState`. Cualquier intento de agregar o modificar ítems en estados sellados arroja `SaleOrderStateException` de forma polimórfica y determinista. |

---

## 🏆 4. Conclusión para la Defensa

La arquitectura implementada en este proyecto integrador no solo cumple al 100% con los Criterios de Aceptación (**CA-01 a CA-08**) y la rúbrica de 10 puntos, sino que constituye una solución real, robusta, extensible y preparada para producción. El dominio puro, la separación de responsabilidades y la inversión de dependencias garantizan que el equipo responda con total solvencia técnica a cualquier cambio de requisito en vivo.