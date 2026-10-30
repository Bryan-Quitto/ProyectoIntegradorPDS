# Ficha Técnica de Decisión de Diseño: DD-03
## Patrones de Diseño: Adapter (GoF Estructural) + Builder (GoF Creacional) + Idempotent Receiver (Patrón Empresarial)
### Módulo: Integración con Odoo 20 JSON-2 y Control de Idempotencia (`Pos.Application` / `Pos.Infrastructure`)

---

### 1. Problema Concreto en el POS
La integración del Punto de Venta local con el ERP Odoo 20 Community presenta desafíos críticos de interoperabilidad, confiabilidad y consistencia transaccional:
1. **Divergencia de Protocolos y Modelos de Datos:** El Dominio del POS habla en términos de agregados limpios (`SaleOrder`, `Product`, `TaxRule`), mientras que Odoo 20 expone una API propietaria JSON-2 (`/json/2/{model}/{method}`) basada en comandos ORM especiales (tuplas `[0, 0, {...}]` y `[6, 0, [...]]`) y autenticación Bearer por API Key.
2. **Construcción de Payloads Complejos y Acoplados:** El endpoint `pos.order.sync_from_ui` en Odoo 20 exige una estructura de diccionario profundamente anidada (`session_id`, `uuid`, `lines`, `payment_ids`, `date_order`, desglose de impuestos), que no debe contaminar las entidades del Dominio.
3. **Riesgo de Pagos Duplicados (RN-08 / RF-16):** En redes inestables de comercio minorista, un cajero o terminal puede reintentar una venta por timeout. Si el backend no implementa idempotencia estricta, se crearían múltiples órdenes y cobros duplicados en el ERP para un único pago en efectivo.
4. **Prohibición de Falsos Éxitos (RN-07):** El POS no puede confirmar localmente una venta si Odoo rechazó la transacción o no respondió.

---

### 2. Alternativas Investigadas y Comparativa Técnica

| Alternativa | Descripción Técnica | Ventajas | Desventajas / Razones de Descarte |
| :--- | :--- | :--- | :--- |
| **Alternativa 1: Sin Patrón (Llamadas HTTP directas desde Casos de Uso con diccionarios inline)** | Instanciar `HttpClient` dentro de `CreateSaleUseCase`, armar un `Dictionary<string, object>` anónimo y enviar la petición sin abstracción de puerto ni clave de idempotencia. | Ahorra interfaces y clases intermedias en prototipos iniciales. | **Violación destructiva de Clean Architecture.** Acopla la capa de Aplicación a detalles HTTP y al formato específico de Odoo 20. Impide realizar pruebas unitarias sin levantar Odoo real. No protege contra cobros duplicados por reintentos. |
| **Alternativa 2: Replicar Catálogo y Sincronización Asíncrona (Outbox Pattern con Tablas Propias)** | Duplicar el catálogo completo de productos en SQLite y sincronizar órdenes en segundo plano mediante un worker periódicamente. | Resiliencia temporal ante caídas de red. | **Violación de la Rúbrica de Persistencia:** Odoo 20 debe ser la **fuente de verdad empresarial**. Replicar catálogo viola el principio de no duplicidad de ERP. Además, no permite la validación instantánea de precios (RN-03) exigida para la venta en caja. |
| **Alternativa 3 (Elegida): Tríada Adapter + Builder + Idempotent Receiver** | **Adapter** (`OdooJson2PosAdapter`) implementa el puerto `IOdooPosGateway`. **Builder** (`OdooPosOrderPayloadBuilder`) ensambla el payload ORM de Odoo paso a paso a partir del Dominio. **Idempotent Receiver** (`SqliteIdempotencyStore`) registra el hash determinista y la respuesta en SQLite antes y después de Odoo. | **Desacoplamiento total.** Aplicación solo conoce interfaces; Domain gobierna los montos fiscales; Builder encapsula la sintaxis JSON-2 de Odoo; Idempotencia previene cualquier duplicación en SQLite y Odoo vía `uuid` determinista. | Requiere mantener el Builder actualizado si Odoo modifica sus campos internos entre versiones mayores. |

> **¿Cuándo NO aportaría valor este patrón?**  
> Si la integración se realizara contra una API REST estándar con modelos idénticos al dominio y sin riesgo de peticiones concurrentes o reintentos (ej. un servicio CRUD interno de solo lectura), un cliente genérico bastaría. La naturaleza propietaria de Odoo JSON-2 y el cobro de dinero real justifican plenamente la solución combinada.

---

### 3. Solución Elegida y su Categoría

* **Categorías de los Patrones Aplicados:**
  1. **Adapter:** **Patrón GoF Estructural**. Convierte la interfaz de la API JSON-2 de Odoo 20 a la interfaz `IOdooPosGateway` esperada por la capa Application.
  2. **Builder:** **Patrón GoF Creacional**. Separa la construcción de la compleja estructura de datos que requiere Odoo de su representación, permitiendo que el mismo proceso construya órdenes con distintos métodos de pago y líneas.
  3. **Idempotent Receiver:** **Patrón de Arquitectura Empresarial (Enterprise Integration Patterns - EIP)**. Garantiza que peticiones repetidas con la misma `Idempotency-Key` devuelvan la respuesta previa sin ejecutar una segunda venta en Odoo.

* **Distinción Conceptual Crítica:**
  * **Clean Architecture:** Ubica el puerto `IOdooPosGateway` en `Pos.Application/Ports/` y las implementaciones concretas en `Pos.Infrastructure/Odoo/`. Application jamás conoce a `HttpClient`, ni el protocolo JSON-RPC, ni la API Key.
  * **SOLID:** Cumple con **DIP (Dependency Inversion Principle)** (los módulos de alto nivel no dependen de los de bajo nivel; ambos dependen de abstracciones) e **ISP (Interface Segregation Principle)** (`IOdooPosGateway` solo expone operaciones que el POS necesita).
  * **Patrones GoF y EIP:** Proveen la técnica específica para construir objetos complejos (Builder), adaptar interfaces dispares (Adapter) y filtrar mensajes duplicados (Idempotent Receiver).

#### Diagrama de Secuencia e Interacción de Componentes
```
[Cliente HTTP / POS UI]
       |
       | POST /api/sales (Header: Idempotency-Key)
       v
[CreateSaleUseCase]
       |
       |-- 1. HasKeyAsync(key)? --------> [SqliteIdempotencyStore]
       |      (Si existe: retorna respuesta previa sin tocar Odoo)
       |
       |-- 2. GetProductsByIdsAsync() --> [OdooJson2PosAdapter] (RN-03)
       |                                           |
       |                                           | POST /json/2/product.product/search_read
       |                                           v
       |                                      [Odoo 20 ERP]
       |
       |-- 3. Crea y Confirma SaleOrder (Domain)
       |
       |-- 4. SyncSaleOrderAsync(order) -> [OdooJson2PosAdapter]
       |                                           |
       |                                           |-- FromSaleOrder(order) -> [PayloadBuilder]
       |                                           |-- Build() -> Payload JSON-2
       |                                           |
       |                                           | POST /json/2/pos.order/sync_from_ui
       |                                           v
       |                                      [Odoo 20 ERP]
       |
       |-- 5. SaveKeyAsync() + SaveSale() -> [SqlitePersistence]
       |
       v
 Retorna 201 Created (SaleOrderResponseDto)
```

---

### 4. Consecuencias y Compromisos (Trade-offs)

* **Ventajas Obtenidas:**
  1. **Tolerancia a Fallos y Cero Duplicidad:** La `Idempotency-Key` deriva deterministamente el `uuid` del pedido en Odoo. Si Odoo recibe dos veces la misma llamada por timeout de red, su restricción de unicidad (`_unique_uuid`) y el manejador `sync_from_ui` reconocen el mismo pedido sin duplicar inventario ni caja.
  2. **Independencia Tecnológica:** Si el día de mañana se migra de Odoo a SAP o un ERP interno, solo se crea un nuevo adaptador (`SapPosAdapter`) implementando `IOdooPosGateway`. Dominio y Aplicación permanecen intactos.
  3. **Seguridad Rigurosa (RNF-09):** La API Key se inyecta en el adaptador desde variables de entorno; el Dominio y los controladores nunca tienen acceso a secretos de infraestructura.
* **Costo de Complejidad Asumido:**
  1. Necesidad de mapear entre tres modelos de datos: DTOs de Aplicación, Entidades de Dominio y diccionarios de comandos ORM de Odoo.
  2. Almacenamiento local SQLite para la tabla `IdempotencyRecords` con serialización JSON de respuestas.

---

### 5. Ubicación Exacta en el Código

| Componente | Tipo | Capa Arquitectónica | Ruta en el Repositorio |
| :--- | :--- | :--- | :--- |
| `IOdooPosGateway` | Puerto (Interfaz) | `Pos.Application` | `backend/Pos.Application/Ports/IPorts.cs` |
| `IIdempotencyStore` | Puerto (Interfaz) | `Pos.Application` | `backend/Pos.Application/Ports/IPorts.cs` |
| `OdooJson2PosAdapter` | Adaptador (Adapter GoF) | `Pos.Infrastructure` | `backend/Pos.Infrastructure/Odoo/OdooJson2PosAdapter.cs` |
| `OdooPosOrderPayloadBuilder` | Constructor (Builder GoF) | `Pos.Infrastructure` | `backend/Pos.Infrastructure/Odoo/OdooPosOrderPayloadBuilder.cs` |
| `SqliteIdempotencyStore` | Receptor Idempotente (EIP) | `Pos.Infrastructure` | `backend/Pos.Infrastructure/Persistence/Repositories.cs` |
| `CreateSaleUseCase` | Caso de Uso Orquestador | `Pos.Application` | `backend/Pos.Application/UseCases/UseCases.cs` |

---

### 6. Prueba y Demostración Verificable

El comportamiento del adaptador, el constructor y el receptor idempotente se verifica mediante:
* **Test de Idempotencia (RN-08):** `P06_LlamadaConMismaIdempotencyKey_RetornaRespuestaPreviaSinDuplicar` en `backend/Pos.Tests/PosTests.cs`.
  * Simula una segunda llamada con la misma clave y asevera que se retorna la respuesta previa y que `SyncSaleOrderAsync` **no** es invocado.
* **Test de Rechazo a Falsos Éxitos (RN-07):** `P05_ErrorComunicacionConOdoo_LanzaOdooCommunicationExceptionSinFalsoExito` en `backend/Pos.Tests/PosTests.cs`.
  * Valida que una caída de Odoo lanza excepción y no registra la venta en la persistencia local.
* **Test de Integración Completa con Totales:** `P13_CreateSale_ValidTwoProductsCashOrder_ReturnsSyncedOrderWithVerifiedTotals` en `backend/Pos.Tests/PosTests.cs`.
  * Valida la construcción correcta de la orden con dos productos y su derivación determinista de UUID.

**Comando de Verificación:**
```powershell
dotnet test "backend/Pos.Tests/Pos.Tests.csproj" --filter "FullyQualifiedName~P05|FullyQualifiedName~P06|FullyQualifiedName~P13" --logger "console;verbosity=normal"
```
