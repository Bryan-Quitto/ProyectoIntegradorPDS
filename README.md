# Proyecto Integrador POS — Integración Odoo 20 Community
**Asignatura:** Patrones de Software — Quinto Semestre  
**Tecnologías:** Backend ASP.NET Core (.NET 10 / C#) | Frontend React 18 + TypeScript + Vite | ERP Odoo 20.0 Community (Docker)  
**Patrón Arquitectónico:** Clean Architecture (Hexagonal / Ports & Adapters)  
**Estado de Entrega:** **Semana 3 Completada y Auditada al 100% (Lead QA: Bryan Lopez)**

---

## 1. Estado del Proyecto y Matriz de Requisitos Funcionales (RF)

En cumplimiento de la especificación académica oficial (*Documento de Requisitos, Plan de 4 Semanas y Criterios de Evaluación*), se presenta el estado de implementación y cobertura de todos los Requisitos Funcionales (RF-01 a RF-17):

| Código | Requisito Funcional | Estado | Componente / Implementación | Evidencia / Prueba Automatizada |
| :--- | :--- | :---: | :--- | :--- |
| **RF-01** | Consultar productos disponibles desde Odoo | ✅ **Completado** | `SearchProductsUseCase`, `ProductCatalogPanel.tsx` | `GET /api/products` (200 OK), `P18` |
| **RF-02** | Buscar productos por nombre o código | ✅ **Completado** | Filtrado reactivo en UI y backend con estados vacíos amigables | Vitest `P18`, prueba negativa con `PROD-999` |
| **RF-03** | Agregar productos al carrito con cantidades | ✅ **Completado** | `ManageCartUseCase.addItem`, `Cart.ts` | Botón cobro bloqueado si vacío (RN-01), xUnit `P01` |
| **RF-04** | Modificar cantidades y eliminar productos del carrito | ✅ **Completado** | Controles `+`/`-`, input protegido e interactivo, modal de confirmación | xUnit `P02` (rechazo $\le 0$), Vitest `P17` (límite 1000) |
| **RF-05** | Subtotales y total coherentes con reglas fiscales | ✅ **Completado** | Desglose fiscal con IVA 15% incluido (Ecuador): Base $30.43, IVA $4.57, Total $35.00 | Vitest `P07`, xUnit `P09` y `P13` |
| **RF-06** | Aplicar políticas de descuento habilitadas | ✅ **Completado** | Patrón Strategy (`IDiscountPolicy`): 10% Promo, Umbral Fijo | xUnit `P10`, UI reactiva en `CartSummaryPanel.tsx` |
| **RF-07** | Incorporar o cambiar políticas de descuento sin impacto | ✅ **Completado** | Principio Abierto/Cerrado (OCP): inyección de nuevas políticas sin modificar `SaleOrder` | xUnit `P10` (`CustomVipDiscountPolicy`) |
| **RF-08** | Rechazar descuentos no autorizados con mensajes claros | ✅ **Completado** | Validación de dominio en `PercentageDiscountPolicy` y `CreateSaleUseCase` | xUnit `RF08_PoliticaDescuentoNoAutorizada...` |
| **RF-09** | Confirmar una venta con líneas, precios y descuentos | ✅ **Completado** | `CreateSaleUseCase`, agregado `SaleOrder` (patrón State) | xUnit `P09`, `P13`, UI `PaymentModal.tsx` |
| **RF-10** | Seleccionar un método de pago configurado y extensible | ✅ **Completado** | Patrón Strategy (`IPaymentProcessor`, `CashPaymentProcessor`, `BankTransferPaymentProcessor`) y selector en React UI | xUnit `P10_ExtensibilidadMetodosPago...`, Vitest `P21` |
| **RF-11** | Registrar la venta y el pago en Odoo mediante integración | ✅ **Completado** | Sincronización atómica mediante método oficial `pos.order.sync_from_ui` (JSON-2) **sin parches manuales** | **Prueba P09 (Integración Real Anexo F)**, xUnit `P09`, HTTP 201 Created |
| **RF-12** | Mostrar identificador, estado y resultado real | ✅ **Completado** | Retorno de `odooOrderReference`, `odooOrderId` y estado `Synced`/`paid` | Modal post-pago en UI, comprobación en Odoo (Evidencia P09) |
| **RF-13** | Consultar el historial de ventas registradas | ✅ **Completado** | Vista `SalesHistoryPage.tsx`, filtros reactivos por referencia ERP, cliente y cédula | `GET /api/sales`, Vitest `P19`, xUnit `P14` |
| **RF-14** | Visualizar el detalle de una venta con trazabilidad | ✅ **Completado** | Modal `SaleDetailModal.tsx` con desglose fiscal completo, metadatos e impresión | `GET /api/sales/{id}`, xUnit `P14`, Vitest `P15` |
| **RF-15** | Impedir operaciones con datos inválidos o inconsistentes | ✅ **Completado** | Validación de sesión POS cerrada y producto inexistente antes de llamar a Odoo | xUnit `P03` (sesión cerrada), `P04` (producto inválido) |
| **RF-16** | Evitar registros duplicados por solicitudes repetidas | ✅ **Completado** | Idempotencia estricta mediante cabecera `Idempotency-Key` y persistencia SQLite | xUnit `P06`, Vitest `P20`, script `test_idempotencia_p06.ps1` |
| **RF-17** | Gestionar errores de comunicación sin afirmar falso éxito | ✅ **Completado** | Manejo de excepciones RFC 7807 (`ProblemDetails`), timeout y caída de Odoo sin falso éxito | xUnit `P05` (backend), Vitest `P08` (frontend) |

> 📌 **Alcance Temporal y Cierre de Semana 3:**  
> Se encuentran **100% cumplidos y verificados los hitos de las Semanas 1, 2 y 3**. La solución integra resiliencia estricta, inmutabilidad de órdenes, trazabilidad contable oficial y extensibilidad de pagos bajo principios SOLID (OCP).

---

## 2. Índice de Informes de Evidencias y Decisiones de Diseño

### A. Informes de Turnos de la Semana 3 (Protocolo de Relevos)
* 👤 **Eli (Turno 1):** [Informe de Resiliencia, Timeouts e Idempotencia Estricta (P03, P05, P06, P08)](docs/evidencias/semana3-eli-resiliencia-idempotencia.md)
* 👤 **Vero (Turno 2):** [Informe de Trazabilidad en Odoo, Inmutabilidad e Historial (RF-12, RF-13, RF-14, RN-06)](docs/evidencias/semana3-vero-historial-trazabilidad.md)
* 👤 **Esteban (Turno 3):** [Informe de Extensibilidad de Pagos, SOLID (OCP) y Selector UI (RF-10, P10)](docs/evidencias/semana3-esteban-extensibilidad-pagos.md)
* 👤 **Bryan (Turno 4):** [Informe de Auditoría Lead QA, Integración E2E Odoo Anexo F y Cierre Semana 3 (P09, CA-03, CA-05)](docs/evidencias/semana3-bryan-auditoria-p09.md)
* 📋 **Plan de Trabajo Consolidado:** [Plan de Trabajo Semana 3 Refactorizado](PLAN_TRABAJO_SEMANA_3.md)
* 🔬 **Auditoría de Integración Real Semanas 1 y 2:** [Trazas JSON-2 de Integración Real contra Odoo 20](docs/evidencias/semana1-2-integracion-real.md)

### B. Fichas Técnicas de Patrones de Diseño (Secciones 7 y 17 de la Rúbrica)

| Ficha Técnica | Patrón de Diseño | Categoría | Responsabilidad en el POS | Requisitos Cubiertos |
| :--- | :--- | :--- | :--- | :--- |
| [**DD-01: Políticas de Descuento**](docs/decisiones-diseno/DD-01-strategy-politicas-descuento.md) | **Strategy** | GoF Comportamiento | Encapsula algoritmos de descuento intercambiables (Porcentual, Umbral Fijo, Sin Descuento) sin alterar la entidad `SaleOrder`. | RF-06, RF-07, RF-08, RN-04 |
| [**DD-02: Ciclo de Vida de la Orden**](docs/decisiones-diseno/DD-02-state-ciclo-vida-orden.md) | **State** | GoF Comportamiento | Controla las fases de la orden (`Draft` $\rightarrow$ `Confirmed` $\rightarrow$ `Synced`) congelando modificaciones antes del cobro (**RN-06**). | RN-01, RN-06, RN-07 |
| [**DD-03: Adaptador y Builder Odoo con Idempotencia**](docs/decisiones-diseno/DD-03-adapter-builder-idempotencia-odoo.md) | **Adapter** + **Builder** + **Idempotent Receiver** | GoF Estructural + GoF Creacional + Patrón Empresarial | Traduce el puerto de aplicación a la API JSON-2 de Odoo, construye comandos ORM atómicos y evita cobros duplicados en reintentos. | RF-11, RF-16, RN-03, RN-07, RN-08 |
| **Extensibilidad de Pagos (Turno 3)** | **Strategy** | GoF Comportamiento | Desacopla la autorización contable de métodos de pago (`IPaymentProcessor`, `CashPaymentProcessor`, `BankTransferPaymentProcessor`) cumpliendo **OCP**. | RF-10, RNF-04, RNF-07, P10 |

---

## 3. Matriz de Reglas de Negocio (RN) y Requisitos No Funcionales (RNF)

### Matriz Oficial de Reglas de Negocio (RN-01 a RN-08)

En concordancia con la Sección 5 del documento de especificación académica, se detalla el cumplimiento exacto de cada regla de negocio:

| Código | Regla de Negocio (PDF) | Implementación y Mecanismo en Código | Prueba Automatizada / Evidencia Verificable |
| :---: | :--- | :--- | :--- |
| **RN-01** | La venta contiene al menos un producto. | `SaleOrder.Confirm()` valida `_lines.Count > 0`. En frontend, botón de cobro deshabilitado si `items.length === 0`. | xUnit **P01** (`DomainValidationException`) y Vitest en UI. |
| **RN-02** | Las cantidades son mayores que cero. | Value Object `Quantity` rechaza valores $\le 0$. En frontend `Cart.addItem` y `Cart.updateQuantity` restringen rango $[1, 1000]$. | xUnit **P02** (prueba con $0, -1, -10$) y Vitest **P17**. |
| **RN-03** | Los precios de referencia se obtienen y verifican con Odoo; el frontend no impone precios arbitrarios. | `CreateSaleUseCase` consulta Odoo vía `GetProductsByIdsAsync` para resolver precios oficiales y regla fiscal; ignora cualquier precio enviado por el cliente. | xUnit **P13** y trazas JSON-2 en `docs/evidencias/semana1-2-integracion-real.md`. |
| **RN-04** | Los descuentos cumplen las políticas autorizadas. | Patrón Strategy (`IDiscountPolicy`) valida porcentajes en rango $[0, 100]\%$ y umbrales mínimos. | xUnit **P10** (`PercentageDiscountPolicy` lanza `DomainValidationException`). |
| **RN-05** | No se confirma una venta inconsistente. | Bloqueo si sesión POS de Odoo está cerrada o producto no existe en el catálogo ERP. | xUnit **P03** (sesión cerrada) y xUnit **P04** (producto no habilitado). |
| **RN-06** | Una venta confirmada no se edita como venta en preparación. | Patrón State (`ISaleOrderState`): `ConfirmedState` y `SyncedState` lanzan `SaleOrderStateException` al intentar mutar líneas o descuentos. | Validado en agregado de dominio `SaleOrder` (Ficha **DD-02**). |
| **RN-07** | No se informa un pago exitoso sin confirmación del sistema responsable. | Excepciones RPC o timeout de Odoo abortan la operación local sin marcar la orden como pagada ni mostrar falso éxito. | xUnit **P05** (backend) y Vitest **P08** (traducción a `ProblemDetails` en frontend). |
| **RN-08** | Una petición repetida no debe generar una segunda venta accidental. | Tabla `idempotency_keys` en SQLite local y encabezado HTTP `Idempotency-Key` devuelven la respuesta en caché sin duplicar en Odoo. | xUnit **P06** y script de prueba de doble POST en consola (Capa 3 de Guía). |

### Requisitos No Funcionales Clave
- **RNF-01 / RNF-02:** Clean Architecture tanto en backend (.NET 10) como en frontend (React 18).
- **RNF-04:** Principios SOLID (SRP en casos de uso, OCP demostrado en descuentos, DIP con interfaces segregadas).
- **RNF-07:** Extensibilidad controlada demostrada con suite de pruebas automáticas.
- **RNF-09:** Cero credenciales ni secretos en repositorio Git (`.env` ignorado en `.gitignore`).
- **RNF-11:** Interoperabilidad demostrada: `Frontend React -> Backend ASP.NET Core -> Odoo 20 Community`.
- **RNF-12:** Reproducibilidad total mediante contenedor Docker y guía de pruebas paso a paso.

---

## 4. Estructura Arquitectónica

```
ProyectoIntegradorPDS/
├── docker-compose.yml          # Orquestación de Odoo 20 Community y PostgreSQL 16
├── GUIA_PRUEBAS_SEMANAS_1_Y_2_POS.md # Guía paso a paso para el equipo y evaluación docente
├── backend/
│   ├── Pos.Domain/             # Capa Núcleo: Entidades, Value Objects, Excepciones, Interfaces Strategy/State (0 dependencias)
│   ├── Pos.Application/        # Capa Casos de Uso: Orquestación, DTOs, Puertos/Interfaces (depende solo de Pos.Domain)
│   ├── Pos.Infrastructure/     # Capa Adaptadores: Odoo JSON-2 (Adapter/Builder), SQLite (EF Core), Seguridad JWT
│   ├── Pos.Api/                # Capa Presentación: Minimal APIs (Endpoints delgados), Composition Root
│   └── Pos.Tests/              # Suite de Pruebas Unitarias automatizadas (xUnit, FluentAssertions, NSubstitute)
├── frontend/
│   ├── src/
│   │   ├── domain/             # Entidades puras en TypeScript (Cart, CartItem, Product) sin React ni Axios
│   │   ├── application/        # Casos de uso y puertos de frontend (ManageCartUseCase, CheckoutSaleUseCase)
│   │   ├── infrastructure/     # Adaptador HTTP hacia la API de backend (PosApiHttpAdapter)
│   │   ├── presentation/       # Vistas (PosTerminalPage, SalesHistoryPage), componentes UI y custom hooks
│   │   └── shared/             # Utilidades compartidas (cn, validadores)
│   └── src/__tests__/          # Suite de Pruebas Frontend automatizadas (Vitest)
└── docs/
    ├── decisiones-diseno/      # Fichas técnicas formales de patrones DD-01, DD-02 y DD-03
    └── evidencias/             # Evidencias de ejecución e integración real contra Odoo 20
```

---

## 5. Preparación del Entorno y Ejecución

> 📖 **Para instrucciones detalladas paso a paso, consulta la [Guía de Pruebas y Reproducción](GUIA_PRUEBAS_SEMANAS_1_Y_2_POS.md).**

### 1. Levantar Odoo 20 en Docker
En la raíz del proyecto:
```powershell
docker compose up -d
```
Verifica que los contenedores `odoo20_web` (puerto `8069`) y `odoo20_db` (puerto `5432`) estén activos con `docker ps`.

### 2. Configurar la Instancia de Odoo (Web)
1. Ingresa a `http://localhost:8069`, crea la base de datos `pos_db` e instala la aplicación **Punto de Venta (Point of Sale)**.
2. Genera una **API Key** desde tu perfil de usuario (*Seguridad de la Cuenta -> Claves de API de Desarrollador*).
3. Configura el impuesto `VAT 15% G` con la opción **"Incluido en el precio"**.
4. Crea `Producto A` (`PROD-001`, $15.00) y `Producto B` (`PROD-002`, $20.00) marcados como **Disponibles en POS**.
5. Abre una sesión de POS en el tablero para que quede en estado `opened`.

### 3. Variables de Entorno del Backend
Copia `backend/.env.example` a `backend/.env` y completa tus credenciales locales:
```ini
ODOO__BASEURL=http://localhost:8069
ODOO__DATABASE=pos_db
ODOO__USERNAME=tu_usuario@ejemplo.com
ODOO__APIKEY=tu_api_key_de_odoo
ODOO__PASSWORD=tu_contraseña_o_api_key
JWT__SECURITYKEY=clave_secreta_jwt_minimo_32_caracteres_aqui
CONNECTIONSTRINGS__SQLITEDB=Data Source=pos_local.db
ASPNETCORE_ENVIRONMENT=Development
PORT=5000
```

### 4. Iniciar Servicios
* **Terminal Backend:**
  ```powershell
  cd backend/Pos.Api
  dotnet run
  ```
  *(Disponible en `http://localhost:5000`)*

* **Terminal Frontend:**
  ```powershell
  cd frontend
  npm install
  npm run dev
  ```
  *(Disponible en `http://localhost:5173`)*

---

## 6. Ejecución de Pruebas Automatizadas

El proyecto cuenta con **29 pruebas automatizadas (100% PASS)** distribuidas entre backend y frontend:

### Backend (.NET 10 — xUnit: 20/20 PASS)
Ejecutar desde la raíz del proyecto:
```powershell
dotnet test backend/Pos.Tests/Pos.Tests.csproj --logger "console;verbosity=normal"
```

| Prueba | Comando con Filtro | Cobertura |
| :--- | :--- | :--- |
| **P01** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P01"` | RN-01: Rechazo de venta sin productos (`DomainValidationException`). |
| **P02** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P02"` | RN-02: Rechazo de cantidades $\le 0$ (pruebas con 0, -1, -10). |
| **P03** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P03"` | RF-15 / RN-05: Rechazo si la sesión POS de Odoo está cerrada. |
| **P04** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P04"` | RF-15 / RN-03: Rechazo si el producto no existe o está deshabilitado en Odoo. |
| **P05** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P05"` | RN-07 / RF-17: Fallo RPC o timeout no declara éxito ficticio. |
| **P06** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P06"` | RN-08 / RF-16: Idempotencia estricta sin duplicar orden en Odoo ni SQLite. |
| **P09** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P09"` | **RF-11 / CA-03 / Anexo F:** Venta real de 2 productos ($35) en efectivo, IVA 15% ($4.57) y conciliación Odoo sin parches `write`. |
| **P10 (Desc.)** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P10_InyectarNuevaPolitica"` | RF-07 / OCP: Inyección de nueva política de descuento sin alterar `SaleOrder`. |
| **P10 (Pagos)** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P10_ExtensibilidadMetodosPago"` | RF-10 / OCP: Inyección de procesador de pago (`MockCustomCryptoPaymentProcessor`) sin alterar `CreateSaleUseCase`. |
| **RF-08** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~RF08"` | RN-04: Rechazo con mensaje claro de tipos de descuento no autorizados. |
| **RN-06** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~RN06"` | RN-06: Patrón State; inmutabilidad estricta de orden confirmada y sincronizada (lanza `SaleOrderStateException`). |
| **P11 / P12** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~Login"` | ARQ-01 / ARQ-02: Autenticación desacoplada y generación/validación de token JWT. |
| **P13** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P13"` | Happy Path: Venta en efectivo de 2 productos con política porcentual y desglose fiscal Odoo. |
| **P14** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P14"` | RF-14: Consulta de orden completa por ID con todas sus líneas fiscales y metadatos. |

### Frontend (React + TypeScript — Vitest: 9/9 PASS)
Ejecutar ubicándose en `frontend/`:
```powershell
cd frontend
npm run test
```

| Prueba | Comando con Filtro | Cobertura |
| :--- | :--- | :--- |
| **P07** | `npm run test -- -t "P07"` | RF-05: Cálculo puro y determinista de carrito, totales, descuento e IVA 15% en `Cart.ts`. |
| **P08** | `npm run test -- -t "P08"` | RN-07 / RF-17: Captura de `PosApiError` (ProblemDetails) y mensaje comprensible sin falso éxito. |
| **P15** | `npm run test -- -t "P15"` | RF-14: Caso de uso `GetSaleDetailUseCase` y recuperación de orden completa. |
| **P16** | `npm run test -- -t "P16"` | Formateo de timestamps UTC a hora local Ecuador (`UTC-5`). |
| **P17** | `npm run test -- -t "P17"` | RF-04 / RN-02: Validación de límites en carrito (1 a 1000 unidades, rechazo de $\le 0$). |
| **P18** | `npm run test -- -t "P18"` | RF-02: Filtrado reactivo de catálogo por Nombre, Código de referencia y Todos. |
| **P19** | `npm run test -- -t "P19"` | RF-13: Filtrado de historial por referencia Odoo acotada, cliente y cédula. |
| **P20** | `npm run test -- -t "P20"` | RN-08 / RF-16: Preservación de `Idempotency-Key` en reintentos para evitar cobros duplicados. |
| **P21** | `npm run test -- -t "P21"` | RF-10: Propagación del método de pago seleccionado al Gateway de Odoo. |

---

## 7. Dictamen de Cierre Semana 3 — Aprobación Lead QA

Con la ejecución de las suites de prueba al 100% verde (29/29 tests), la resolución de la prueba de integración oficial **P09 (Anexo F)** sin parches manuales en Odoo 20, la inmutabilidad garantizada por el patrón **State**, la extensibilidad con patrón **Strategy** (**OCP / P10**) y la resiliencia probada con patrón **Idempotent Receiver**, se certifica el cierre exitoso de la Semana 3.

**Lead QA Responsable:** Bryan Lopez  
**Fecha:** Domingo, 8 de Noviembre de 2026
