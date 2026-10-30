# Ficha Técnica de Decisión de Diseño: DD-01
## Patrón de Diseño: Strategy (GoF - Comportamiento)
### Módulo: Políticas de Descuento en Punto de Venta (`Pos.Domain`)

---

### 1. Problema Concreto en el POS
El Punto de Venta (POS) requiere soportar múltiples modalidades comerciales de descuento sobre las órdenes de venta según las promociones de la empresa:
- **Sin Descuento:** Operación estándar sin deducción.
- **Descuento Porcentual (RF-06 / RN-04):** Aplicación de un porcentaje variable (ej. 10%, 15%) sobre el subtotal bruto.
- **Descuento por Umbral / Monto Fijo (RF-07 / RF-08):** Aplicación de un descuento fijo (ej. $5.00) únicamente si el subtotal de la compra supera un umbral determinado (ej. $50.00).
- **Extensibilidad a Futuro:** Capacidad de introducir en el futuro políticas comerciales complejas (ej. cliente VIP, convenios empresariales, 2x1 o cupones) sin modificar la lógica interna de cálculo de la orden de venta (`SaleOrder`).

El dolor técnico radica en que concentrar múltiples lógicas promocionales dentro de la entidad `SaleOrder` genera código rígido, plagado de bifurcaciones condicionales, propenso a errores al agregar nuevas reglas y violatorio del principio de Abierto/Cerrado (OCP).

---

### 2. Alternativas Investigadas y Comparativa Técnica

| Alternativa | Descripción Técnica | Ventajas | Desventajas / Razones de Descarte |
| :--- | :--- | :--- | :--- |
| **Alternativa 1: Sin Patrón (Condicionales `if / switch` directos en `SaleOrder`)** | Un enum `DiscountType` y un método `decimal CalculateDiscount()` con un bloque `switch` dentro de `SaleOrder`. | Sencillez inicial; cero archivos adicionales; fácil de leer para 1 o 2 reglas estáticas. | **Rígida y frágil.** Cada nueva promoción obliga a modificar la clase `SaleOrder`, recompilar el dominio y arriesgar regresiones en órdenes existentes. Viola OCP. |
| **Alternativa 2: Patrón Decorator (GoF Estructural)** | Envolver la orden o los ítems en decoradores sucesivos (`DiscountedOrderDecorator`, `TaxedOrderDecorator`) para alterar el total acumulativamente. | Permite encadenar múltiples descuentos y sobrecargos en tiempo de ejecución. | **Sobreingeniería innecesaria.** En el POS la política de descuento a nivel de orden es excluyente o unificada por transacción (se aplica una política a la orden). Decorator añade complejidad en jerarquía de objetos y serialización sin aportar valor para este caso de uso. |
| **Alternativa 3 (Elegida): Patrón Strategy (GoF Comportamiento)** | Abstracción `IDiscountPolicy` que encapsula la familia de algoritmos de descuento como objetos intercambiables. | Desacoplamiento total; la orden delega el cálculo; extensible sin tocar `SaleOrder`; altamente testeable. | Requiere instanciar e inyectar objetos de estrategia en la creación o mutación de la orden. |

> **¿Cuándo NO aportaría valor este patrón?**  
> Si el negocio tuviese una política comercial única, fija e inmutable por ley o regulación (por ejemplo, "únicamente existe 0% o 5% fijo"), la creación de una jerarquía de estrategias representaría sobreingeniería innecesaria. El patrón se justifica plenamente debido a los requerimientos RF-06, RF-07, RF-08 y la variabilidad comercial del POS.

---

### 3. Solución Elegida y su Categoría

* **Categoría del Patrón:** **Patrón GoF de Comportamiento**.
* **Distinción Conceptual Crítica:**
  * **Clean Architecture (Enfoque Arquitectónico):** Ubica `IDiscountPolicy` y sus estrategias concretas estrictamente dentro de la capa `Pos.Domain/Strategies/`. No depende de frameworks, bases de datos ni HTTP.
  * **SOLID (Principios de Diseño):** Cumple con **OCP (Open/Closed Principle)**, ya que `SaleOrder` está abierta a la extensión (podemos inyectar nuevas estrategias) pero cerrada a la modificación. Cumple con **SRP (Single Responsibility Principle)**, separando la contabilidad de la orden del cálculo de algoritmos promocionales.
  * **Patrón de Diseño (GoF):** Provee el mecanismo estructural concreto (`IDiscountPolicy` + implementaciones) para hacer cumplir OCP dentro del modelo de objetos del Dominio.

#### Diagrama de Clases
```
+-------------------------------------------------------------+
|                      <<interface>>                          |
|                     IDiscountPolicy                         |
+-------------------------------------------------------------+
| + PolicyName: string                                        |
| + CalculateDiscount(subtotal: decimal): decimal             |
+-------------------------------------------------------------+
          ^                         ^                        ^
          |                         |                        |
+-------------------+   +-------------------------+   +------------------------------+
| NoDiscountPolicy  |   | PercentageDiscountPolicy|   | ThresholdFixedDiscountPolicy |
+-------------------+   +-------------------------+   +------------------------------+
| + Calculate...()  |   | - Percentage: decimal   |   | - Threshold: decimal         |
|   return 0m;      |   | + Calculate...()        |   | - FixedDiscount: decimal     |
+-------------------+   +-------------------------+   +------------------------------+
                                                              ^
                                                              | (Inyectado en)
                                                    +-------------------+
                                                    |     SaleOrder     |
                                                    +-------------------+
                                                    | + DiscountPolicy  |
                                                    | + SubTotal        |
                                                    | + DiscountAmount  |
                                                    +-------------------+
```

---

### 4. Consecuencias y Compromisos (Trade-offs)

* **Ventajas Obtenidas:**
  1. **Extensibilidad Pura:** Se pueden agregar estrategias (ej. `VipDiscountPolicy`, `BlackFridayDiscountPolicy`) creando una nueva clase sin modificar una sola línea de `SaleOrder.cs`.
  2. **Testabilidad Aislada:** Cada política se prueba unitariamente con valores límite (0%, 100%, montos menores y mayores al umbral) sin necesidad de instanciar órdenes complejas.
  3. **Invariantes Protegidas:** La entidad `SaleOrder` garantiza que el descuento jamás exceda el subtotal bruto ni genere totales negativos.
* **Costo de Complejidad Asumido:**
  1. Incremento en el número de clases y archivos dentro de `Pos.Domain/Strategies`.
  2. La capa de Aplicación (`CreateSaleUseCase`) debe mapear el tipo de descuento solicitado en el comando (`command.DiscountType`) a la instancia de estrategia adecuada.

---

### 5. Ubicación Exacta en el Código

| Componente | Tipo | Ruta en el Repositorio |
| :--- | :--- | :--- |
| `IDiscountPolicy` | Interfaz (Contrato) | `backend/Pos.Domain/Strategies/DiscountPolicies.cs` |
| `NoDiscountPolicy` | Clase Concreta | `backend/Pos.Domain/Strategies/DiscountPolicies.cs` |
| `PercentageDiscountPolicy` | Clase Concreta | `backend/Pos.Domain/Strategies/DiscountPolicies.cs` |
| `ThresholdFixedDiscountPolicy` | Clase Concreta | `backend/Pos.Domain/Strategies/DiscountPolicies.cs` |
| `SaleOrder` | Entidad Raíz de Agregado | `backend/Pos.Domain/Entities/SaleOrder.cs` |
| `CreateSaleUseCase` | Orquestador de Aplicación | `backend/Pos.Application/UseCases/UseCases.cs` |

---

### 6. Prueba y Demostración Verificable

La extensibilidad y correcto funcionamiento del patrón se verifican mediante tests automatizados en la suite de pruebas del backend:
* **Test de Extensibilidad (OCP):** `P10_InyectarNuevaPoliticaDeDescuento_SinModificarSaleOrder` en `backend/Pos.Tests/PosTests.cs`.
  * Demuestra que una política creada en tiempo de test (`CustomVipDiscountPolicy` de 25%) se inyecta en `SaleOrder` y calcula correctamente sin modificar la entidad.
* **Test de Happy Path con Descuento:** `P13_CreateSale_ValidTwoProductsCashOrder_ReturnsSyncedOrderWithVerifiedTotals` en `backend/Pos.Tests/PosTests.cs`.
  * Valida la política `PercentageDiscountPolicy(10m)` sobre una orden de 2 productos con desglose fiscal canónico.

**Comando de Verificación:**
```powershell
dotnet test "backend/Pos.Tests/Pos.Tests.csproj" --filter "FullyQualifiedName~P10|FullyQualifiedName~P13" --logger "console;verbosity=normal"
```
