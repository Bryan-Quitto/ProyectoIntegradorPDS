# 🧠 SKILLS & CONSTRAINTS SPECIFICATION — POS ODOO 20 COMMUNITY
## Proyecto Integrador: Patrones de Software (Quinto Semestre)

> 📌 **PROPÓSITO DE ESTE DOCUMENTO:**
> Este archivo consolida **toda la especificación académica, bloqueos técnicos, reglas de negocio, decisiones de diseño y limitaciones oficiales** extraídas de los documentos y directrices docentes (PDF de Especificación Académica, PDF de Validación Técnica JSON-2 y Directivas de Cátedra).
> 
> **Cualquier agente de IA o desarrollador que continúe el trabajo en este repositorio DEBE acatar estrictamente las restricciones y directrices aquí descritas.**

---

## 1. REGLAS ARQUITECTÓNICAS Y ESTILO OBLIGATORIO

### 1.1. Clean Architecture (Hexagonal / Ports & Adapters)
* **Backend (.NET 10 / C#):**
  * `Pos.Domain`: Núcleo puro. Entidades (`SaleOrder`, `Product`), Value Objects inmutables (`Money`, `Quantity`, `TaxRule`), Excepciones de Dominio, e Interfaces de Patrones (`IDiscountPolicy`, `IOrderState`). **Cero dependencias externas ni de infraestructura**.
  * `Pos.Application`: Orquestación de negocio. DTOs, Puertos (`IOdooPosGateway`, `IIdempotencyStore`, `ISaleHistoryRepository`), y **Casos de Uso**.
  * `Pos.Infrastructure`: Adaptadores tecnológicos. Implementación JSON-2 para Odoo (`OdooJson2PosAdapter`), SQLite con EF Core para auditoría e idempotencia local, y seguridad JWT.
  * `Pos.Api`: Capa de presentación HTTP. Minimal APIs delegando directamente a los Casos de Uso. Manejo uniforme de errores con `ProblemDetails`.
* **Frontend (React 18 + TypeScript + Vite):**
  * `src/domain/`: Entidades y reglas puras (`Cart`, `CartItem`, `Product`) en TypeScript vainilla sin dependencias de React, hooks ni Axios.
  * `src/application/`: Casos de uso (`ManageCartUseCase`, `CheckoutSaleUseCase`, `GetSaleDetailUseCase`) y puertos (`IPosApiGateway`).
  * `src/infrastructure/`: Adaptador HTTP (`PosApiHttpAdapter`) que consume la API REST de .NET.
  * `src/presentation/`: Componentes UI (Tailwind CSS, Zustand, React Router DOM, Lucide Icons).

### 1.2. ⚠️ REGLA ESTRICTA DE CÁTEDRA: ESTILO DE LA CAPA APPLICATION
* **PROHIBIDO:** Usar **Services** (Opción *a*). No crear `PosService`, `OrderService`, etc. Se prohíben por tender a convertirse en "God Objects" que violan el principio de Responsabilidad Única (SRP).
* **OBLIGATORIO:** Usar **Use Cases (Casos de Uso puros)** (Opción *b*). Cada caso de uso es una clase con un único método de ejecución (`ExecuteAsync` o `execute`) que orquesta una única intención de usuario o sistema. (Opcionalmente CQRS con MediatR / Opción *c*).

---

## 2. BLOQUEOS TÉCNICOS Y CONDICIONES CRÍTICAS (LECCIONES DE ODOO 20 JSON-2)

Del informe docente de validación técnica de Odoo 20 Community mediante API JSON-2, se extraen las siguientes limitaciones y soluciones requeridas:

| Limitación Docente Identificada | Solución Obligatoria en este Proyecto |
| :--- | :--- |
| **Escrituras manuales (`write`) prohibidas:** En Postman el docente tuvo que forzar importes y `amount_paid` manualmente mediante llamadas diagnósticas `write`. El docente advirtió que esto **NO es un flujo de producción válido**. | **Uso estricto de `pos.order.sync_from_ui`:** Se utiliza el método oficial transaccional de Odoo invocado en `/json/2/pos.order/sync_from_ui`. En una sola llamada atómica se crean la orden, las líneas fiscales y el pago, dejando la orden en `paid` automáticamente. |
| **Cálculo de impuestos e importes:** Odoo no calcula automáticamente los subtotales si se envían llamadas de bajo nivel fragmentadas. | **Patrón Builder (`OdooPosOrderPayloadBuilder`):** El agregado `SaleOrder` calcula previamente de forma pura la base imponible y el IVA (con precisión centesimal), y el Builder inyecta los comandos ORM `[0, 0, {...}]` con `price_subtotal`, `price_subtotal_incl`, y `tax_ids`. |
| **Configuración Fiscal Ecuador:** Precios con IVA 15% incluido. | En Odoo el impuesto `VAT 15% G` debe configurarse como **"Incluido en el precio"**. Para $35.00 ($15 de Producto A + $20 de Producto B): Base = $30.43, IVA 15% = $4.57. El sistema no permite discrepancias de centavos. |
| **No acceso directo a Base de Datos:** | Está **terminantemente prohibido** que .NET se conecte directamente a la base PostgreSQL de Odoo para insertar o consultar ventas. Todo flujo comercial debe pasar por la API JSON-2 (`POST /json/2/<modelo>/<metodo>`). |
| **Manejo de credenciales y secretos:** | La API Key y credenciales de Odoo **nunca** deben viajar al navegador frontend ni comitearse en Git. Se gestionan exclusivamente en el `backend/.env` (ignorado en `.gitignore`). |

---

## 3. REGLAS DE NEGOCIO OBLIGATORIAS (RN)

* **RN-01 (Venta no vacía):** La orden debe contener al menos un producto. Si se intenta confirmar vacía, lanza `DomainValidationException` con código `RN-01`.
* **RN-02 (Cantidades positivas):** La cantidad de cada producto debe ser estrictamente mayor a 0 ($qty > 0$). Modelado en el Value Object `Quantity` (rechaza `0`, `-1`, `-10`). Límite superior de seguridad: 1000 unidades.
* **RN-03 (Fuente de verdad de precios):** Los precios e impuestos oficiales se consultan y validan exclusivamente contra Odoo 20. El cliente frontend **no puede imponer precios arbitrarios**.
* **RN-04 (Políticas de descuento autorizadas):** Descuentos restringidos entre 0% y 100%. Porcentajes negativos o superiores al 100% son rechazados en el constructor de la política.
* **RN-05 (Consistencia de venta):** No se confirma una venta si la sesión de POS de Odoo no está en estado `opened` o si algún producto no existe o está deshabilitado para POS.
* **RN-06 (Congelamiento de orden):** Una vez confirmada (`ConfirmedState`), la orden no admite modificaciones de ítems ni cambio de políticas de descuento (patrón State).
* **RN-07 (Prohibido falso éxito):** Si Odoo responde con error RPC, sesión cerrada o timeout, el backend aborta la transacción y lanza `OdooCommunicationException`. Jamás se devuelve éxito ficticio ni se registra la venta como pagada si Odoo no la confirmó.
* **RN-08 (Idempotencia estricta):** Cada solicitud de cobro envía un encabezado `Idempotency-Key` (GUID). Si la llamada se repite, el backend responde inmediatamente la orden almacenada en SQLite sin crear una segunda venta en Odoo.

---

## 4. PATRONES DE DISEÑO GOF & EMPRESARIALES IMPLEMENTADOS

| ID | Patrón | Categoría | Ubicación en el Código | Justificación y Problema que Resuelve |
| :--- | :--- | :--- | :--- | :--- |
| **DD-01** | **Strategy** | GoF Comportamiento | `Pos.Domain/Strategies/DiscountPolicies.cs` | **Políticas de Descuento variables:** Permite intercambiar `PercentageDiscountPolicy`, `ThresholdFixedDiscountPolicy` y `NoDiscountPolicy` sin modificar la entidad `SaleOrder`, cumpliendo el Principio Open/Closed (OCP). |
| **DD-02** | **State** | GoF Comportamiento | `Pos.Domain/Entities/SaleOrder.cs` | **Ciclo de Vida de la Orden:** Modela los estados `DraftState` $\to$ `ConfirmedState` $\to$ `SyncedState`. Bloquea la mutación de líneas una vez que la orden pasa a confirmación y cobro. |
| **DD-03** | **Adapter** + **Builder** + **Idempotent Receiver** | GoF Estructural + Creacional + Patrón Empresarial | `Pos.Infrastructure/Odoo/` y `Pos.Infrastructure/Persistence/` | **Integración Odoo & Idempotencia:** `OdooJson2PosAdapter` desacopla el puerto `IOdooPosGateway` de la API JSON-2; `OdooPosOrderPayloadBuilder` ensambla el JSON ORM para `sync_from_ui`; e `IdempotencyStore` en SQLite evita duplicados en reintentos de red. |

---

## 5. MATRIZ DE REQUISITOS FUNCIONALES (RF-01 A RF-17)

| Código | Requisito Funcional | Estado | Componente Clave | Prueba Automatizada |
| :--- | :--- | :---: | :--- | :--- |
| **RF-01** | Consultar catálogo desde Odoo | ✅ Activo | `SearchProductsUseCase`, `ProductCatalogPanel.tsx` | `GET /api/products` (200 OK), `P18` |
| **RF-02** | Búsqueda por nombre/código y estado vacío | ✅ Activo | Filtrado reactivo en UI con fallback informativo | Vitest `P18`, prueba negativa con `PROD-999` |
| **RF-03** | Agregar al carrito con cantidades | ✅ Activo | `ManageCartUseCase.addItem`, `Cart.ts` | Botón cobro deshabilitado si vacío, xUnit `P01` |
| **RF-04** | Modificar cantidades y eliminar productos | ✅ Activo | Controles `+`/`-`, modal de confirmación de borrado | xUnit `P02`, Vitest `P17` |
| **RF-05** | Coherencia de cálculo fiscal (IVA 15%) | ✅ Activo | Desglose centesimal Odoo: Base $30.43 + IVA $4.57 = $35.00 | Vitest `P07`, xUnit `P13` |
| **RF-06** | Aplicación de descuentos (Strategy) | ✅ Activo | `IDiscountPolicy`: 10% Promo (-$3.50), Base $27.39, Total $31.50 | xUnit `P10`, UI reactiva |
| **RF-07** | Extensibilidad OCP en descuentos | ✅ Activo | Admite `CustomVipDiscountPolicy` sin tocar `SaleOrder` | xUnit `P10` |
| **RF-08** | Rechazar descuentos no autorizados | ✅ Activo | Lanza `DomainValidationException` si descuento $< 0\%$ o $> 100\%$ | Validación en constructor de política |
| **RF-09** | Confirmar venta con líneas y descuentos | ✅ Activo | `CreateSaleUseCase`, congelamiento por `SaleOrder.Confirm()` | xUnit `P13`, UI `PaymentModal.tsx` |
| **RF-10** | Selección de método de pago configurado | ✅ Activo | Resolución dinámica de Efectivo en la sesión POS activa | `ResolveCashPaymentMethodIdAsync` |
| **RF-11** | Registro de venta y pago en Odoo | ✅ Activo | Transacción atómica vía `pos.order.sync_from_ui` | xUnit `P13`, HTTP 201 Created |
| **RF-12** | Identificador, estado y resultado real | ✅ Activo | Devuelve `odooOrderReference`, `odooOrderId` y estado `Synced` | Modal de confirmación en UI |
| **RF-13** | Consultar historial de ventas registradas | ✅ Activo | Vista `SalesHistoryPage.tsx`, auditoría local SQLite | `GET /api/sales`, Vitest `P19` |
| **RF-14** | Visualizar el detalle de una venta | ✅ Activo | Modal `SaleDetailModal.tsx` con desglose fiscal por línea | `GET /api/sales/{id}`, xUnit `P14`, Vitest `P15` |
| **RF-15** | Impedir operaciones inválidas | ✅ Activo | Rechazo si sesión cerrada o producto no existe en Odoo | xUnit `P03`, xUnit `P04` |
| **RF-16** | Idempotencia ante solicitudes repetidas | ✅ Activo | Caché SQLite por `Idempotency-Key` sin re-llamar a Odoo | xUnit `P06`, script de doble POST |
| **RF-17** | Manejo de fallos sin falso éxito | ✅ Activo | Mapeo de fallos RPC a `OdooCommunicationException` | xUnit `P05`, Vitest `P08` |

---

## 6. SUITE DE PRUEBAS AUTOMATIZADAS Y COMANDOS DE EJECUCIÓN

### 6.1. Backend (.NET 10 xUnit)
Ejecutar desde la raíz del proyecto:

```powershell
# Ejecutar todas las 13 pruebas unitarias de backend
dotnet test backend/Pos.Tests/Pos.Tests.csproj

# Pruebas individuales con filtro FullyQualifiedName:
dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P01" # RN-01: Rechazo venta vacía
dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P02" # RN-02: Rechazo cantidades <= 0
dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P03" # RF-15: Rechazo sesión POS cerrada
dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P04" # RF-15: Rechazo producto inválido
dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P05" # RN-07: Error Odoo sin falso éxito
dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P06" # RN-08: Idempotencia en cobro
dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P10" # RF-07: Extensibilidad OCP
dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P13" # Happy Path: Venta 2 productos Odoo
dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P14" # RF-14: Consulta de orden por ID
dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~Login" # P11/P12: Autenticación JWT
```

### 6.2. Frontend (React + TypeScript Vitest)
Ejecutar desde la carpeta `frontend/`:

```powershell
cd frontend
# Ejecutar todas las 7 pruebas unitarias de frontend
npm run test

# Pruebas individuales por tag:
npm run test -- -t "P07" # RF-05: Cálculo puro de carrito e IVA 15%
npm run test -- -t "P08" # RF-17: Traducción de errores sin falso éxito
npm run test -- -t "P15" # RF-14: Caso de uso GetSaleDetailUseCase
npm run test -- -t "P16" # Formateo y conversión UTC a UTC-5 Ecuador
npm run test -- -t "P17" # RF-04: Validación de límites de cantidad (1 a 1000)
npm run test -- -t "P18" # RF-02: Filtrado reactivo de catálogo
npm run test -- -t "P19" # RF-13: Filtrado de historial por referencia y cliente
```

---

## 7. RUTAS DECLARATIVAS Y URL QUERY PARAMS (FRONTEND)

El frontend está instrumentado con `react-router-dom` (v6.28) y sincroniza filtros con la URL:

* `/`: Terminal POS principal. Soporta `?buscar=...&filtro=ALL|NAME|CODE&pagina=X`.
* `/historial`: Historial de ventas de auditoría. Soporta `?buscar=...&filtro=ALL|REFERENCE|CUSTOMER&pagina=X`.
* `/ventas` y `/pos`: Redirecciones declarativas.

---

## 8. DIRECTIVAS PARA CUALQUIER AGENTE DE IA O DESARROLLADOR

1. **NO convertir Casos de Uso en Servicios.** Mantener siempre clases `UseCase` individuales bajo `Pos.Application/UseCases/`.
2. **NO modificar importes de Odoo mediante llamadas `write`.** Cualquier interacción con órdenes POS debe efectuarse a través de `pos.order.sync_from_ui` mediante el builder correspondiente.
3. **NO acceder a PostgreSQL de Odoo directamente.**
4. **NO deshabilitar la Idempotencia (RN-08).** Toda orden generada debe ir acompañada de su `Idempotency-Key` único.
5. **NO eliminar las pruebas automatizadas.** Toda nueva regla de negocio debe acompañarse de su correspondiente prueba en `PosTests.cs` o `pos.spec.ts`.
