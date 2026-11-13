# 📄 INFORME DE EVIDENCIAS — SEMANA 4 (TURNO 2: ESTEBAN)
## Módulo: Pruebas Finales Automatizadas, Cobertura E2E y Resiliencia
**Responsable:** Washington Esteban Villalba López (`Esteban-Vlz`)  
**Asignatura:** Patrones de Software — Quinto Semestre  
**Marco Oficial:** Especificación Académica Oficial (Semana 4 - Entrega Final)  
**Criterios y Requisitos Cubiertos:** CA-01, CA-07, RNF-04, RNF-05, RNF-06, RNF-08, RNF-09, RN-07, RN-08, RF-14, RF-16  

---

## 1. Resumen Ejecutivo del Turno

En estricto cumplimiento con el cronograma del [PLAN_TRABAJO_SEMANA_4.md](../../PLAN_TRABAJO_SEMANA_4.md), durante el **Turno 2 (Jueves)** se ejecutaron cuatro frentes de trabajo esenciales para la entrega final y la defensa técnica individual:

1. **Mejoras Colaborativas y Ajustes de Compatibilidad (Complementando el Turno 1):**
   - **Compatibilidad Multiplataforma en `build_cero_warnings.ps1` (CA-07):** Se adaptó la ruta del script a `$PSScriptRoot` dinámico y universal e incorporó verificación de códigos de salida (`$LASTEXITCODE`), garantizando que cualquier integrante del equipo pueda auditar la compilación limpia en cualquier entorno.
   - **Integridad y Trazabilidad de Auditoría en `SaleEndpoints.cs`:** Se reforzó la asignación de `UserId` y `OperatorName` para que provengan directamente de los claims firmados del token JWT para cajeros, asegurando la fidelidad inmutable de la autoría de ventas.
   - **Protección de Datos en `CustomerEndpoints.cs`:** Se alineó el endpoint `GET /api/customers` con `.RequireAuthorization()`, garantizando acceso controlado bajo el Principio de Menor Privilegio.
   - **Estandarización RFC 7807:** Se unificó el retorno en `GET /api/sales/{id}` con `Results.Problem` (ProblemDetails) para consistencia global.

2. **Blindaje de Aislamiento de Caja y Roles en Backend (xUnit - .NET 10):**
   - Se diseñaron e implementaron 3 nuevas pruebas oficiales (`P25`, `P26`, `P27`) en [`PosTests.cs`](../../backend/Pos.Tests/PosTests.cs), certificando el rechazo de ventas en cajas ajenas (HTTP 403), el bloqueo a operadores sin sesión activa y el aislamiento del historial por caja asignada.
   - **Resultado Backend:** **31 de 31 pruebas en verde (100% PASS)**.

3. **Automatización de la Suite de Integración E2E en PowerShell (`test_flujo_completo_e2e.ps1`):**
   - Se desarrolló en la raíz del repositorio el script maestro `test_flujo_completo_e2e.ps1`, el cual valida en un solo comando el ciclo completo del sistema contra el backend real y Odoo 20:
     * Paso 1: Health Check (`/health` -> UP).
     * Paso 2: Autenticación de Operador contra Odoo (Obtención de JWT Bearer con rol y caja asignada).
     * Paso 3: Consulta de Catálogo Odoo con existencias disponibles.
     * Paso 4: Verificación de Sesión Activa de POS en Odoo ERP (`pos.session` -> `opened`).
     * Paso 5: Emisión de Venta con Idempotencia (HTTP 201 Created y conciliación Odoo).
     * Paso 6: Verificación de Idempotent Receiver (RN-08 / Reintento con caché idéntica).
     * Paso 7: Consulta de Auditoría y Desglose Fiscal (`GET /api/sales/{id}`).
   - **Resultado E2E:** **7 de 7 pasos superados (100% PASS)**.

4. **Pruebas de Borde y Resiliencia en Frontend (Vitest - React 18):**
   - Se diseñaron e incorporaron 3 nuevas pruebas oficiales (`P32`, `P33`, `P34`) en [`pos.spec.ts`](../../frontend/src/__tests__/pos.spec.ts), validando la traducción de errores técnicos de red a mensajes amigables, la asociación del cliente `Acme Corporation` con cédula ecuatoriana y la propagación de excepciones sin declarar falsos éxitos (RN-07).
   - **Resultado Frontend:** **22 de 22 pruebas en verde (100% PASS)**.

5. **Línea Base Total del Proyecto:** **53 pruebas automatizadas operativas y superadas al 100%** (31 Backend + 22 Frontend).

---

## 2. Evidencias Verificables de Ejecución

### Evidencia 1: Ejecución del Script Oficial E2E (`test_flujo_completo_e2e.ps1`)
```text
==========================================================================
 [SEMANA 4 - ESTEBAN] SUITE DE INTEGRACIÓN END-TO-END (E2E) POS ODOO 20
 Servidor Objetivo: http://localhost:5000 | Operador: admin
==========================================================================
 [PASS] Paso 1 de 7: Health Check del Servidor
        -> Estado: UP (Pos.Api (.NET 10))
 [PASS] Paso 2 de 7: Autenticación de Operador contra Odoo
        -> Usuario: admin | Caja #5 (Furniture Shop) | Rol: Administrator
 [PASS] Paso 3 de 7: Consulta de Catálogo de Productos Odoo
        -> Cargados 100 productos. Primero: 'Whiteboard Pen' (Stock: 75.0, Precio: $1.2)
 [PASS] Paso 4 de 7: Verificación de Sesión Activa de POS
        -> Sesión #5 'Furniture Shop/00001' en estado abierto (Usuario ID: 2)
 [PASS] Paso 5 de 7: Emisión Exitosa de Venta (POST /api/sales)
        -> Orden ID: 5b884ace-67ba-46ab-226a-fe41593bd0ee | Total: $2.90 | Odoo Ref: 260-1-000008
 [PASS] Paso 6 de 7: Idempotent Receiver (Reintento RN-08)
        -> Retorno de orden idéntica desde caché sin duplicación de cobro ni orden en Odoo.
 [PASS] Paso 7 de 7: Consulta de Auditoría y Desglose Fiscal
        -> Subtotal: $2.4 | IVA: $0.5 | Total: $2.9 | Líneas: 1

==========================================================================
 [RESULTADO OFICIAL DE LA PRUEBA E2E - TURNO 2 ESTEBAN]:
  Paso 1: Health Check (UP)                                 -> [PASS]
  Paso 2: Login Operador Odoo (JWT Bearer)                 -> [PASS]
  Paso 3: Catálogo Productos Odoo con Stock                -> [PASS]
  Paso 4: Sesión Activa de POS en Odoo ERP                 -> [PASS]
  Paso 5: Emisión de Venta con Idempotencia (HTTP 201)     -> [PASS]
  Paso 6: Idempotent Receiver Caché Hit (RN-08)            -> [PASS]
  Paso 7: Auditoría y Desglose Fiscal (GET /api/sales/{id}) -> [PASS]
  Progreso de Suite: 7 de 7 pasos completados con éxito (100% PASS).
==========================================================================
```

### Evidencia 2: Suite Completa de Backend (.NET 10 xUnit)
```text
PS C:\Users\GHOST\Documents\Patrones\Patrones 2.0> dotnet test backend/Pos.Tests/Pos.Tests.csproj
  Determinando los proyectos que se van a restaurar...
  Todos los proyectos están actualizados para la restauración.
  Pos.Domain -> Pos.Domain.dll
  Pos.Application -> Pos.Application.dll
  Pos.Infrastructure -> Pos.Infrastructure.dll
  Pos.Tests -> Pos.Tests.dll
Serie de pruebas para Pos.Tests.dll (.NETCoreApp,Version=v10.0)
1 archivos de prueba en total coincidieron con el patrón especificado.

Correctas! - Con error: 0, Superado: 31, Omitido: 0, Total: 31, Duración: 1 s - Pos.Tests.dll (net10.0)
```

### Evidencia 3: Suite Completa de Frontend (TypeScript Vitest)
```text
PS C:\Users\GHOST\Documents\Patrones\Patrones 2.0\frontend> npm test
> pos-odoo-frontend@1.0.0 test
> vitest run

 ✓ src/__tests__/pos.spec.ts (22 tests) 38ms

 Test Files  1 passed (1)
      Tests  22 passed (22)
   Duration  1.32s
```

### Evidencia 4: Compilación Limpia Universal (`build_cero_warnings.ps1`)
```text
==========================================================================
 [SEMANA 4 - REPRODUCIBLE] AUDITORÍA DE CLEAN CODE Y COMPILACIÓN LIMPIA
==========================================================================

>>> [1/2] Compilando Backend ASP.NET Core (.NET 10)...
Compilación correcta.
    0 Advertencia(s)
    0 Errores

>>> [2/2] Compilando Frontend React 18 + Vite (TypeScript Estricto)...
✓ 1621 modules transformed.
dist/index.html                   0.55 kB │ gzip:  0.36 kB
dist/assets/index-C80Lovvo.css   34.61 kB │ gzip:  6.71 kB
dist/assets/index-CQUvZe2v.js   316.19 kB │ gzip: 91.92 kB
✓ built in 3.88s

==========================================================================
 [ÉXITO TOTAL - COMPILACIÓN VERIFICADA]:
  1. Warning ASP0000 eliminado de Program.cs.
  2. Minimal APIs modularizadas en Pos.Api/Endpoints/ (Clean Architecture & SRP).
  3. Backend (.NET 10): 0 Advertencias, 0 Errores.
  4. Frontend (React 18 + TS): Compilación de producción 100% limpia.
  5. Ejecución universal y reproducible en cualquier entorno (CA-07).
==========================================================================
```

---

## 3. Matriz de Trazabilidad de Requisitos y Pruebas

| ID Prueba | Capa / Framework | Componente Evaluado | Requisito / Criterio | Resultado |
| :---: | :---: | :--- | :---: | :---: |
| **P25** | Backend (xUnit) | `CreateSaleUseCase` / Aislamiento de Caja | RN-07, CA-07 | **100% PASS** |
| **P26** | Backend (xUnit) | Operador sin sesión activa / Menor Privilegio | RNF-06, RN-07 | **100% PASS** |
| **P27** | Backend (xUnit) | `GetSalesHistoryUseCase` / Sesión sin ventas | RNF-06, CA-07 | **100% PASS** |
| **P32** | Frontend (Vitest) | `errorHandler.ts` / Resiliencia de Red | RNF-08, CA-01 | **100% PASS** |
| **P33** | Frontend (Vitest) | `CheckoutSaleUseCase` / Acme Corp & PartnerId | RF-14, RNF-06 | **100% PASS** |
| **P34** | Frontend (Vitest) | `CheckoutSaleUseCase` / Manejo estricto de errores | RN-07, RNF-08 | **100% PASS** |
| **E2E 1-7**| Integración (PowerShell)| Flujo Completo HTTP contra Backend y Odoo | CA-07, RN-08 | **100% PASS** |

---

## 4. Guía de Defensa Individual para Esteban (Turno 2)

### Pregunta de Examen 1: ¿Por qué fue necesario crear el script E2E en PowerShell en lugar de confiar únicamente en las pruebas unitarias?
> **Respuesta Técnica:**  
> Las pruebas unitarias con mocks (`Substitute.For<T>`) verifican la lógica pura de la aplicación en memoria (Dominio y Casos de Uso), pero no validan los límites físicos de integración: la serialización JSON, los headers HTTP (`Authorization: Bearer`, `Idempotency-Key`), la persistencia real en la base de datos SQLite ni la comunicación RPC con el contenedor Docker de Odoo 20. El script `test_flujo_completo_e2e.ps1` cumple con el Criterio de Aceptación **CA-07** al certificar que todo el pipeline funciona de extremo a extremo sin intervención manual.

### Pregunta de Examen 2: ¿Cómo se garantiza que un cajero no pueda registrar ventas en una caja registradora ajena?
> **Respuesta Técnica:**  
> Se implementó un control en dos capas:
> 1. **Capa API (`SaleEndpoints.cs`):** Se extrae el claim `pos_session_id` del token JWT firmado criptográficamente. Si el comando contiene un `PosSessionId` que difiere del claim autorizado, el endpoint rechaza la petición con **HTTP 403 Forbidden**.
> 2. **Capa de Aplicación (`CreateSaleUseCase.cs`):** Valida contra el Gateway de Odoo que la sesión esté en estado `'opened'`. Si la sesión está cerrada o no coincide, se arroja una excepción de validación de negocio (`ApplicationValidationException`), bloqueando cualquier intento de bypass.

---

## 5. Dictamen del Turno 2

* **Estado de la Rama `main`:** **ESTABLE Y OPERATIVA**.
* **Pruebas Automatizadas Totales:** **53 / 53 (100% PASS)**.
* **Compilación:** **0 Errores, 0 Advertencias** en Backend y Frontend.
* **Entregables:**
  - Script oficial: [`test_flujo_completo_e2e.ps1`](../../test_flujo_completo_e2e.ps1).
  - Informe técnico: `docs/evidencias/semana4-esteban-pruebas-finales-resiliencia.md`.
  - Fixes de auditoría en endpoints y scripts de compilación.
* **Pase de Relevo:** Listo para que **Vero (Turno 3)** continúe con la actualización documental del `README.md` y fichas de diseño.
