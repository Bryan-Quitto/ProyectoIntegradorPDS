# Ficha Técnica de Decisión de Diseño: DD-02
## Patrón de Diseño: State (GoF - Comportamiento)
### Módulo: Ciclo de Vida y Transiciones de la Orden de Venta (`Pos.Domain`)

---

### 1. Problema Concreto en el POS
Una orden de venta en el POS atraviesa etapas críticas con reglas de negocio estrictas (RN-01, RN-06, RN-07):
1. **Borrador (`Draft`):** La orden se está armando en la caja. Se pueden agregar, modificar o quitar líneas, así como alterar la política de descuento.
2. **Confirmada (`Confirmed`):** La orden fue cerrada por el cajero para proceder al cobro. **Regla de Negocio RN-06:** En este estado la orden queda congelada; está terminantemente prohibido alterar productos, cantidades o descuentos.
3. **Sincronizada (`Synced`):** La orden fue transmitida exitosamente a Odoo 20 y cuenta con número de recibo (`pos_reference`) e ID oficial del ERP. No admite re-confirmación ni re-sincronización.

El problema técnico consiste en evitar que el código dependa de variables booleanas dispersas (ej. `isConfirmed`, `isSynced`) y múltiples validaciones manuales `if (!isConfirmed)` en cada método de mutación (`AddLine`, `RemoveLine`, `SetDiscountPolicy`), lo cual suele desembocar en condiciones de carrera, estados inconsistentes y órdenes enviadas a Odoo modificadas a posteriori.

---

### 2. Alternativas Investigadas y Comparativa Técnica

| Alternativa | Descripción Técnica | Ventajas | Desventajas / Razones de Descarte |
| :--- | :--- | :--- | :--- |
| **Alternativa 1: Sin Patrón (Flags booleanos o Enum con validación `if`)** | Propiedades `bool IsConfirmed`, `bool IsSynced` en `SaleOrder`, con cláusulas de guarda manuales en cada método público. | Implementación rápida; fácil de entender en ejemplos de juguete de una sola clase. | **Propenso a fugas y errores humanos.** Conforme crecen los métodos (`AddLine`, `UpdateQuantity`, `ApplyDiscount`, `Cancel`), el desarrollador debe recordar colocar la guarda condicional en cada uno. No hay garantía formal de que el comportamiento cambie dinámicamente según el estado. |
| **Alternativa 2: Máquina de Estados Finita basada en librería externa (ej. Stateless)** | Configurar una máquina de estados mediante DSL fluido (`order.Configure(State.Draft).Permit(Trigger.Confirm, State.Confirmed)`). | Permite definir jerarquías de estados, triggers asíncronos y gráficas de transición. | **Violación de Clean Architecture en Domain.** Introducir dependencias de paquetes NuGet de terceros dentro de `Pos.Domain` viola la regla de pureza de la arquitectura (Domain debe tener 0 dependencias externas). |
| **Alternativa 3 (Elegida): Patrón State (GoF Comportamiento puro en C#)** | Interfaz `ISaleOrderState` y clases polimórficas de estado (`DraftState`, `ConfirmedState`, `SyncedState`) que encapsulan el comportamiento y controlan las transiciones válidas. | **100% puro en C#** (cero dependencias); comportamiento encapsulado; imposibilidad de mutar líneas fuera de `Draft`; transiciones atómicas y seguras. | Requiere exponer un método interno de transición (`TransitionToState`) accesible únicamente por el agregado. |

> **¿Cuándo NO aportaría valor este patrón?**  
> Si una orden de venta se crease y liquidase en un único paso atómico e instantáneo sin fases intermedias (ej. un registro de log o evento de solo lectura), un patrón State sería redundante. Sin embargo, en un POS real la orden se construye interactivamente, se congela para cobrar y se sincroniza asíncronamente con un ERP, lo cual hace al patrón indispensable.

---

### 3. Solución Elegida y su Categoría

* **Categoría del Patrón:** **Patrón GoF de Comportamiento**.
* **Distinción Conceptual Crítica:**
  * **Clean Architecture:** Ubica el patrón en el corazón de `Pos.Domain/States/`, garantizando que las reglas de transición sean invariantes empresariales puras y ajenas a controladores o persistencia.
  * **SOLID:** Cumple con **SRP** (cada clase de estado solo responde a las operaciones permitidas en su fase) y **LSP (Liskov Substitution Principle)** (cualquier implementación de `ISaleOrderState` puede sustituir a otra en el contexto de `SaleOrder` sin romper el contrato).
  * **Patrón de Diseño (GoF):** Provee una solución orientada a objetos para que `SaleOrder` modifique su comportamiento cuando su estado interno cambia, pareciendo cambiar de clase.

#### Diagrama de Transición de Estados
```
              Confirm()
  +---------+ ---------> +-----------+ MarkAsSynced() +----------+
  |  Draft  |            | Confirmed | -------------> |  Synced  |
  +---------+            +-----------+                +----------+
    |                      |                            |
    | CanModifyLines: true | CanModifyLines: false      | CanModifyLines: false
    | Mutaciones permitidas| Mutaciones lanzan          | Mutaciones lanzan
    |                      | SaleOrderStateException    | SaleOrderStateException
```

#### Diagrama de Clases
```
+--------------------------------------------------------------------------+
|                              <<interface>>                               |
|                            ISaleOrderState                               |
+--------------------------------------------------------------------------+
| + StateName: string                                                      |
| + CanModifyLines: bool                                                   |
| + Confirm(order: SaleOrder): void                                        |
| + MarkSynced(order: SaleOrder, odooRef: string, odooId: int): void       |
+--------------------------------------------------------------------------+
          ^                              ^                         ^
          |                              |                         |
+-------------------+          +--------------------+    +-------------------+
|    DraftState     |          |   ConfirmedState   |    |    SyncedState    |
+-------------------+          +--------------------+    +-------------------+
| CanModify: true   |          | CanModify: false   |    | CanModify: false  |
| Confirm() -> OK   |          | Confirm() -> ERROR |    | Confirm() -> ERROR|
| MarkSync()-> ERROR|          | MarkSync() -> OK   |    | MarkSync()-> ERROR|
+-------------------+          +--------------------+    +-------------------+
```

---

### 4. Consecuencias y Compromisos (Trade-offs)

* **Ventajas Obtenidas:**
  1. **Blindaje de la Invariante RN-06:** La propiedad `CanModifyLines` del estado actual gobierna el método de guarda `EnsureCanModify()` de `SaleOrder`. Intentar alterar líneas en `Confirmed` o `Synced` produce de inmediato una `SaleOrderStateException`.
  2. **Transiciones Seguras y Deterministas:** Es imposible saltar directamente de `Draft` a `Synced` sin pasar por `Confirmed`.
  3. **Trazabilidad en Persistencia:** El nombre del estado (`StateName`) se almacena en `SaleAuditRecord` en SQLite local y viaja a Odoo (`state = "paid"`).
* **Costo de Complejidad Asumido:**
  1. La entidad `SaleOrder` debe colaborar bidireccionalmente con los estados mediante métodos con alcance `internal` (`TransitionToState`, `SetOdooDetails`), restringidos al ensamblado de Dominio.

---

### 5. Ubicación Exacta en el Código

| Componente | Tipo | Ruta en el Repositorio |
| :--- | :--- | :--- |
| `ISaleOrderState` | Interfaz (Contrato de Estado) | `backend/Pos.Domain/States/SaleOrderStates.cs` |
| `DraftState` | Estado Concreto | `backend/Pos.Domain/States/SaleOrderStates.cs` |
| `ConfirmedState` | Estado Concreto | `backend/Pos.Domain/States/SaleOrderStates.cs` |
| `SyncedState` | Estado Concreto | `backend/Pos.Domain/States/SaleOrderStates.cs` |
| `SaleOrderStateException` | Excepción de Dominio | `backend/Pos.Domain/Exceptions/DomainExceptions.cs` |
| `SaleOrder` | Contexto / Entidad Agregado | `backend/Pos.Domain/Entities/SaleOrder.cs` |

---

### 6. Prueba y Demostración Verificable

El ciclo de vida del patrón State se valida de forma exhaustiva en la suite de pruebas unitarias:
* **Test de Transición Exitosa (Draft $\rightarrow$ Confirmed $\rightarrow$ Synced):** `P13_CreateSale_ValidTwoProductsCashOrder_ReturnsSyncedOrderWithVerifiedTotals` en `backend/Pos.Tests/PosTests.cs`.
  * Valida que la orden inicia en `Draft`, pasa a `Confirmed` antes de llamar a Odoo y se consolida en `Synced` al recibir el ID del ERP.
* **Test de Invariante RN-01 (Rechazo de Confirmación Vacía):** `P01_RechazarVentaSinProductos_LanzaDomainValidationException` en `backend/Pos.Tests/PosTests.cs`.
  * Demuestra que `DraftState.Confirm()` bloquea la confirmación si la orden no contiene productos.

**Comando de Verificación:**
```powershell
dotnet test "backend/Pos.Tests/Pos.Tests.csproj" --filter "FullyQualifiedName~P01|FullyQualifiedName~P13" --logger "console;verbosity=normal"
```
