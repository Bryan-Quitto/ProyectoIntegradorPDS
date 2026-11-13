# Ficha Técnica de Decisión de Diseño: DD-04
## Patrón de Diseño: Strategy (GoF - Comportamiento)
### Módulo: Extensibilidad de Métodos de Pago (`Pos.Application` / `Pos.Infrastructure`)

---

### 1. Problema Concreto en el POS
El Punto de Venta (POS) requiere procesar transacciones comerciales mediante múltiples formas y pasarelas de pago:
- **Efectivo (`CashPaymentProcessor`):** Liquidación inmediata en caja física, sin recargos ni pasarelas externas.
- **Transferencia Bancaria (`BankTransferPaymentProcessor`):** Conciliación contra cuenta de banco o depósito directo.
- **Extensibilidad a Futuro (RF-10 / P10):** Capacidad de incorporar nuevos métodos de pago comerciales (ej. Deuna Ecuador, Tarjeta de Crédito/Débito, Pagos QR o Criptomonedas) sin modificar la lógica del caso de uso de venta (`CreateSaleUseCase`) ni la entidad `SaleOrder`.

El problema técnico consistía en evitar que la orquestación del cobro dependiera de estructuras `switch(paymentMethod)` o bloques condicionales anidados dentro de `CreateSaleUseCase`. Dicho acoplamiento viola flagrantemente el Principio de Abierto/Cerrado (OCP), ya que la adición de una pasarela de pago obligaría a reabrir, editar y arriesgar la estabilidad del flujo transaccional central.

---

### 2. Alternativas Investigadas y Comparativa Técnica

| Alternativa | Descripción Técnica | Ventajas | Desventajas / Razones de Descarte |
| :--- | :--- | :--- | :--- |
| **Alternativa 1: Sin Patrón (Bloque `switch` con Enum en el Caso de Uso)** | Evaluar un enum `PaymentMethodType` dentro de `CreateSaleUseCase` y ejecutar la lógica de autorización inline. | Implementación directa y rápida para 1 o 2 formas fijas de pago. | **Violación de OCP y SRP.** Cada nuevo método de pago obliga a modificar el caso de uso central, arriesga regresiones y mezcla la orquestación de la venta con reglas particulares de pasarelas. |
| **Alternativa 2: Factory Method Acoplada** | Una clase `PaymentFactory` con métodos estáticos que retorna instancias concretas, dependiendo de configuraciones locales. | Centraliza la instanciación de procesadores. | **Acoplamiento rígido.** Si la fábrica no se combina con inyección de dependencias y contratos polimórficos, la resolución de pasarelas que requieren servicios externos (APIs bancarias, tokens HTTP) se vuelve difícil de probar y extender. |
| **Alternativa 3 (Elegida): Patrón Strategy (GoF Comportamiento con DI)** | Definición del puerto `IPaymentProcessor` e inyección de una colección polimórfica `IEnumerable<IPaymentProcessor>` en el contenedor de dependencias. | **Cumplimiento riguroso de OCP.** Incorporar una nueva pasarela (ej. `DeunaPaymentProcessor`) solo requiere crear una nueva clase y registrarla; `CreateSaleUseCase` permanece 100% inalterado y testeable. | Mayor cantidad de clases e interfaces en la capa de Aplicación. |

> **¿Cuándo NO aportaría valor este patrón?**  
> Si el Punto de Venta estuviera diseñado exclusivamente para una caja registradora cerrada que solo y para siempre admita cobros manuales en efectivo (sin registro electrónico de bancos, tarjetas ni pasarelas QR), la creación de una jerarquía de procesadores de pago constituiría sobreingeniería. El patrón se justifica plenamente debido al requisito RF-10, la coexistencia de Efectivo/Transferencia y la necesidad académica y empresarial de soportar extensiones de pago plug-and-play.

---

### 3. Solución Elegida y su Categoría

* **Categoría del Patrón:** **Patrón GoF de Comportamiento**.
* **Distinción Conceptual Crítica:**
  * **Clean Architecture:** El puerto `IPaymentProcessor` se define en `Pos.Application/Ports/`, gobernando el contrato de procesamiento. Las estrategias estándar o simuladas (`CashPaymentProcessor`, `BankTransferPaymentProcessor`) residen en la capa de Aplicación/Infraestructura sin contaminar el núcleo del Dominio (`Pos.Domain`).
  * **SOLID:** Cumple con **OCP (Open/Closed Principle)**: el sistema está cerrado a la modificación pero abierto a la extensión. Cumple con **DIP (Dependency Inversion Principle)**: `CreateSaleUseCase` depende de la abstracción `IPaymentProcessor`, no de implementaciones concretas. Cumple con **SRP**: cada procesador atiende exclusivamente su protocolo de cobro.
  * **Patrón GoF:** Provee la estrategia intercambiable para ejecutar algoritmos de pago polimórficos seleccionados en tiempo de ejecución según el payload del cajero.

#### Diagrama de Clases
```
+-------------------------------------------------------------+
|                      <<interface>>                          |
|                    IPaymentProcessor                        |
+-------------------------------------------------------------+
| + PaymentMethodCode: string                                 |
| + ProcessPaymentAsync(request: PaymentRequest): Task<Result>|
+-------------------------------------------------------------+
          ^                         ^                        ^
          |                         |                        |
+-------------------+   +-------------------------+   +------------------------------+
|CashPaymentProc.   |   |BankTransferPaymentProc. |   | MockCustomCryptoPaymentProc. |
+-------------------+   +-------------------------+   +------------------------------+
| Code: "CASH"      |   | Code: "BANK_TRANSFER"   |   | Code: "CRYPTO_USDT" (Test)   |
| + ProcessPayment()|   | + ProcessPayment()      |   | + ProcessPayment()           |
+-------------------+   +-------------------------+   +------------------------------+
                                    |
                                    | Inyectado en colección
                                    v
                    +-------------------------------+
                    |       CreateSaleUseCase       |
                    +-------------------------------+
                    | - _paymentProcessors          |
                    | + ExecuteAsync(command)       |
                    +-------------------------------+
```

---

### 4. Consecuencias y Compromisos (Trade-offs)

* **Ventajas Obtenidas:**
  1. **Aislamiento Total de Cambios:** Agregar pasarelas bancarias, billeteras móviles (Deuna) o pasarelas de tarjeta no altera ni una línea de código existente de venta.
  2. **Facilidad de Pruebas Unitarias:** Se puede verificar el soporte de pagos con procesadores simulados (`MockCustomCryptoPaymentProcessor`) en tiempo de test en memoria.
  3. **Trazabilidad en Odoo:** Cada estrategia conoce su mapeo contable de diarios en el ERP (`cash` vs `bank`).
* **Costo de Complejidad Asumido:**
  1. Requiere resolver el procesador adecuado en la capa de orquestación consultando la colección de estrategias registradas.

---

### 5. Ubicación Exacta en el Código

| Componente | Tipo | Capa Arquitectónica | Ruta en el Repositorio |
| :--- | :--- | :--- | :--- |
| `IPaymentProcessor` | Interfaz (Puerto) | `Pos.Application` | `backend/Pos.Application/Ports/IPaymentProcessor.cs` |
| `CashPaymentProcessor` | Estrategia Concreta | `Pos.Application` | `backend/Pos.Application/UseCases/PaymentProcessors.cs` |
| `BankTransferPaymentProcessor` | Estrategia Concreta | `Pos.Application` | `backend/Pos.Application/UseCases/PaymentProcessors.cs` |
| `PaymentMethodSelector` | Componente UI React | `Frontend Presentation` | `frontend/src/presentation/components/pos/PaymentModal.tsx` |
| `CreateSaleUseCase` | Orquestador | `Pos.Application` | `backend/Pos.Application/UseCases/UseCases.cs` |

---

### 6. Prueba y Demostración Verificable

La extensibilidad y correcto funcionamiento del patrón Strategy para pagos se comprueban mediante pruebas automatizadas en backend y frontend:
* **Test de Extensibilidad (OCP):** `P10_ExtensibilidadMetodosPago_PermiteNuevaEstrategiaSinModificarCasoDeUso` en `backend/Pos.Tests/PosTests.cs`.
  * Demuestra que un nuevo procesador (`MockCustomCryptoPaymentProcessor`) se registra en la colección y procesa exitosamente una orden sin tocar el código fuente de `CreateSaleUseCase`.
* **Test de Frontend (Vitest):** `P21: CheckoutSaleUseCase propaga el método de pago seleccionado al Gateway de Odoo (RF-10)` en `frontend/src/__tests__/pos.spec.ts`.

**Comando de Verificación:**
```powershell
dotnet test "backend/Pos.Tests/Pos.Tests.csproj" --filter "FullyQualifiedName~P10_ExtensibilidadMetodosPago" --logger "console;verbosity=normal"
```
