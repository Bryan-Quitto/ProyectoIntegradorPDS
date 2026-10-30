# Proyecto Integrador POS — Integración Odoo 20 Community
**Asignatura:** Proceso de Desarrollo de Software (PDS) — Quinto Semestre  
**Tecnologías:** Backend .NET 10 (C#) | Frontend React 19 + TypeScript + Vite | ERP Odoo 20.0 Community (Docker)  
**Patrón Arquitectónico:** Clean Architecture (Hexagonal / Ports & Adapters)  

---

## 1. Índice de Decisiones de Diseño (Sección 7 y 17)

En cumplimiento estricto con las Secciones 7 y 17 de la rúbrica del proyecto, se distinguen explícitamente los **patrones de diseño (GoF / Empresariales)** del enfoque arquitectónico (**Clean Architecture**) y los principios (**SOLID**). 

| Ficha Técnica | Patrón de Diseño | Categoría | Responsabilidad en el POS | Requisitos Cubiertos |
| :--- | :--- | :--- | :--- | :--- |
| [**DD-01: Políticas de Descuento**](docs/decisiones-diseno/DD-01-strategy-politicas-descuento.md) | **Strategy** | GoF Comportamiento | Encapsula algoritmos de descuento intercambiables (Porcentual, Umbral Fijo, Sin Descuento) sin alterar la entidad `SaleOrder`. | RF-06, RF-07, RF-08, RN-04 |
| [**DD-02: Ciclo de Vida de la Orden**](docs/decisiones-diseno/DD-02-state-ciclo-vida-orden.md) | **State** | GoF Comportamiento | Controla las fases de la orden (`Draft` $\rightarrow$ `Confirmed` $\rightarrow$ `Synced`) congelando modificaciones antes del cobro. | RN-01, RN-06, RN-07 |
| [**DD-03: Adaptador y Builder Odoo con Idempotencia**](docs/decisiones-diseno/DD-03-adapter-builder-idempotencia-odoo.md) | **Adapter** + **Builder** + **Idempotent Receiver** | GoF Estructural + GoF Creacional + Patrón Empresarial | Traduce el puerto de aplicación a la API JSON-2 de Odoo, construye comandos ORM y evita cobros duplicados en reintentos. | RF-16, RN-03, RN-07, RN-08 |

---

## 2. Estructura Arquitectónica

```
ProyectoIntegradorPDS/
├── backend/
│   ├── Pos.Domain/             # Capa Núcleo: Entidades, Value Objects, Excepciones, Interfaces Strategy/State (0 dependencias)
│   ├── Pos.Application/        # Capa Casos de Uso: Orquestación, DTOs, Puertos/Interfaces (depende solo de Pos.Domain)
│   ├── Pos.Infrastructure/     # Capa Adaptadores: Odoo JSON-2, SQLite (EF Core), Seguridad JWT
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

## 3. Verificación Rápida y Pruebas Automatizadas

### Backend (.NET 10)
```powershell
Set-Location "backend"
dotnet build
dotnet test --logger "console;verbosity=normal"
```

### Frontend (React + TypeScript)
```powershell
Set-Location "frontend"
npm run build
npx vitest run
```

---

## 4. Reglas de Negocio Implementadas

- **RN-01:** Rechazo de confirmación de órdenes sin productos.
- **RN-02:** Cantidad de productos estrictamente mayor a 0.
- **RN-03:** Precios oficiales validados directamente contra Odoo 20 (fuente de verdad).
- **RN-04:** Restricción de porcentajes de descuento entre 0% y 100%.
- **RN-06:** Congelamiento de ítems y descuentos una vez confirmada la orden (patrón State).
- **RN-07:** Prohibido declarar falsos éxitos si Odoo rechaza o no responde la sincronización.
- **RN-08:** Idempotencia estricta en ventas mediante encabezado `Idempotency-Key` y persistencia SQLite local.
- **RNF-09:** Cero credenciales ni secretos en repositorio Git (`.env` estrictamente en `.gitignore`).
