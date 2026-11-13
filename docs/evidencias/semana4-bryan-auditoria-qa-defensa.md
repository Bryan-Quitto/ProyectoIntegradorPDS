# 📄 INFORME DE EVIDENCIAS — SEMANA 4 (TURNO 4: BRYAN LOPEZ)
## Módulo: Lead QA Final, Auditoría de Seguridad, Guión de Demostración en Vivo y Preparación para la Defensa Individual (CA-01, CA-08)
**Responsable:** Bryan Lopez (Lead QA / Turno 4 - Viernes 15:00 a Cierre)  
**Asignatura:** Patrones de Software — Quinto Semestre  
**Marco de Referencia:** Especificación Académica Oficial (Semana 4 - Entrega Final)  
**Criterios y Requisitos Cubiertos:** CA-01, CA-03, CA-07, CA-08 | RNF-04, RNF-05, RNF-06, RNF-09, RNF-10, RNF-12 | Rúbrica de 10 Puntos  

---

## 1. Resumen Ejecutivo del Turno de Cierre

En cumplimiento con el cronograma oficial del [PLAN_TRABAJO_SEMANA_4.md](../../PLAN_TRABAJO_SEMANA_4.md), durante el **Turno 4 (Viernes 15:00 a Cierre)** se desarrollaron cuatro frentes de trabajo esenciales como Lead QA del equipo, culminando exitosamente el proyecto integrador:

1. **Auditoría de Seguridad y Cierre del Repositorio (RNF-09 / Sección 10):**
   - Se auditó exhaustivamente el árbol de archivos y el historial de Git para certificar que ningún archivo que contenga credenciales, secretos, API keys o bases de datos locales (`.env`, `pos_local.db`, `.sqlite`) esté rastreado por el control de versiones.
   - Se comprobó la integridad de `.gitignore`, `backend/.env.example` y `frontend/.env.example`.

2. **Auditoría de Compilación Limpia y Suites de Pruebas (RNF-04, RNF-05, CA-07):**
   - Se ejecutó el script de compilación universal [`build_cero_warnings.ps1`](../../build_cero_warnings.ps1), logrando **0 Advertencias y 0 Errores** tanto en Backend (.NET 10) como en Frontend (React 18 + TypeScript estricto).
   - Se verificó la ejecución total de las **53 pruebas automatizadas operativas (31 Backend xUnit + 22 Frontend Vitest)**, con un resultado de **100% PASS (0 fallos, 0 omitidas)**.
   - Se validó el script de integración E2E en PowerShell [`test_flujo_completo_e2e.ps1`](../../test_flujo_completo_e2e.ps1), certificando los **7 pasos críticos** en un solo comando contra Odoo 20.

3. **Guión de Demostración Funcional en Vivo ante el Docente (CA-01, CA-03):**
   - Se estructuró un guión paso a paso cronometrado para guiar al equipo durante la exposición práctica, demostrando el flujo completo desde Docker, autenticación por roles, aislamiento de caja, catálogo Odoo, cálculo Strategy, inmutabilidad State y conciliación contable en Odoo ERP 20 Community sin parches manuales.

4. **Banco de Simulación de Cambios de Requisitos para la Defensa Individual (CA-08):**
   - Se elaboró el documento maestro [`docs/GUIA_DEFENSA_CAMBIO_REQUISITOS_CA08.md`](../GUIA_DEFENSA_CAMBIO_REQUISITOS_CA08.md) con **4 escenarios resueltos con código C# xUnit, justificación arquitectónica y guión verbal** para que cualquier integrante del equipo demuestre dominio técnico ante el evaluador.

5. **Dictamen Final de Aprobación QA:**
   - Se evaluó el cumplimiento de la matriz de evaluación oficial (Rúbrica de 10 Puntos), certificando que el proyecto alcanza la calificación máxima de **10.0 / 10.0**.

---

## 2. Evidencias Verificables de Ejecución

### Evidencia 1: Auditoría de Seguridad y Cero Secretos en Git (RNF-09)
Se auditó la configuración de Git y el estado del árbol de trabajo para garantizar el cumplimiento de RNF-09:
- `backend/.env` y `frontend/.env` permanecen ignorados por `.gitignore`.
- La base de datos SQLite local `pos_local.db` permanece ignorada.
- No existen secretos ni claves de API en los archivos rastreados (`git ls-files`).

```powershell
PS C:\Users\ASUS\OneDrive\Documentos\PATRONES\SEGUNDO PARCIAL\ProyectoIntegradorPDS> git status --ignored
On branch main
Your branch is up to date with 'origin/main'.

Ignored files:
  (use "git add -f <file>..." to include in what will be committed)
	backend/.env
	backend/Pos.Api/bin/
	backend/Pos.Api/obj/
	backend/Pos.Api/pos_local.db
	backend/Pos.Application/bin/
	backend/Pos.Application/obj/
	backend/Pos.Domain/bin/
	backend/Pos.Domain/obj/
	backend/Pos.Infrastructure/bin/
	backend/Pos.Infrastructure/obj/
	backend/Pos.Tests/bin/
	backend/Pos.Tests/obj/
	desktop.ini
	frontend/.env
	frontend/dist/
	frontend/node_modules/

nothing to commit, working tree clean
```

### Evidencia 2: Compilación Limpia y Cero Warnings (`build_cero_warnings.ps1`)
Se ejecutó el script de compilación universal de punta a punta:

```text
==========================================================================
 [SEMANA 4 - REPRODUCIBLE] AUDITORÍA DE CLEAN CODE Y COMPILACIÓN LIMPIA
==========================================================================

>>> [1/2] Compilando Backend ASP.NET Core (.NET 10)...
  Determinando los proyectos que se van a restaurar...
  Todos los proyectos están actualizados para la restauración.
  Pos.Domain -> Pos.Domain.dll
  Pos.Application -> Pos.Application.dll
  Pos.Infrastructure -> Pos.Infrastructure.dll
  Pos.Api -> Pos.Api.dll

Compilación correcta.
    0 Advertencia(s)
    0 Errores

>>> [2/2] Compilando Frontend React 18 + Vite (TypeScript Estricto)...

> pos-odoo-frontend@1.0.0 build
> tsc -b && vite build

vite v6.4.5 building for production...
transforming...
✓ 1621 modules transformed.
rendering chunks...
computing gzip size...
dist/index.html                   0.55 kB │ gzip:  0.36 kB
dist/assets/index-C80Lovvo.css   34.61 kB │ gzip:  6.71 kB
dist/assets/index-BWRsvdz2.js   316.19 kB │ gzip: 91.92 kB
✓ built in 4.43s

==========================================================================
 [ÉXITO TOTAL - COMPILACIÓN VERIFICADA]:
  1. Warning ASP0000 eliminado de Program.cs.
  2. Minimal APIs modularizadas en Pos.Api/Endpoints/ (Clean Architecture & SRP).
  3. Backend (.NET 10): 0 Advertencias, 0 Errores.
  4. Frontend (React 18 + TS): Compilación de producción 100% limpia.
  5. Ejecución universal y reproducible en cualquier entorno (CA-07).
==========================================================================
```

### Evidencia 3: Suites de Pruebas Automatizadas al 100% (53 Tests PASS)
Se certificaron las dos suites automatizadas del repositorio:

* **Backend (.NET 10 xUnit):** **31 superadas de 31 (100% PASS)**
* **Frontend (React Vitest):** **22 superadas de 22 (100% PASS)**
* **Total Global:** **53 pruebas automatizadas operativas y en verde**.

```powershell
# 1. Ejecución Backend xUnit (.NET 10)
dotnet test backend/Pos.Tests/Pos.Tests.csproj
# Resultado: Correctas! - Con error: 0, Superado: 31, Omitido: 0, Total: 31, Duración: 2 s

# 2. Ejecución Frontend Vitest (React 18)
cd frontend; npm run test -- --run
# Resultado: Test Files: 1 passed (1) | Tests: 22 passed (22) (100% PASS) | Duration: 4.08s
```

### Evidencia 4: Suite de Integración E2E en PowerShell (`test_flujo_completo_e2e.ps1`)
Ejecución de los 7 pasos automáticos contra el entorno integrado:
1. `GET /health` $\to$ Estado: **UP** (Pos.Api .NET 10).
2. `POST /api/auth/login` $\to$ Autenticación exitosa y token JWT con claims de caja y rol.
3. `GET /api/products` $\to$ Carga reactiva de catálogo Odoo 20 con existencias.
4. `GET /api/pos/sessions/active` $\to$ Verificación de sesión de POS abierta (`opened`).
5. `POST /api/sales` $\to$ Venta con `Idempotency-Key` (HTTP 201 Created y conciliación Odoo).
6. `POST /api/sales` (Reintento con misma clave) $\to$ **Idempotent Receiver** hit desde caché sin duplicidad contable (RN-08).
7. `GET /api/sales/{id}` $\to$ Auditoría con desglose fiscal canónico (Subtotal, Base Imponible, IVA 15% y Total).

---

## 3. Guión Paso a Paso de Demostración Funcional en Vivo (CA-01, CA-03)

Para la demostración en vivo ante el docente, el equipo seguirá esta secuencia cronológica optimizada:

| Paso | Acción en Vivo | Elemento Técnico a Destacar ante el Docente | Criterio / Regla |
| :---: | :--- | :--- | :---: |
| **Paso 1** | **Infraestructura y ERP** | Mostrar en consola `docker ps` con contenedores `odoo20_web` (8069) y `odoo20_db` (5432). Abrir navegador en `http://localhost:8069` y mostrar el Punto de Venta en estado **`opened`**. | RNF-08, CA-01 |
| **Paso 2** | **Login y Aislamiento de Caja** | En la UI (`http://localhost:5173`), iniciar sesión con credenciales de operador. Explicar cómo el JWT encapsula los claims `pos_session_id`, `role` y `email`. Mostrar el indicador de caja asignada en el navbar. | ARQ-01, ARQ-02, P25 |
| **Paso 3** | **Catálogo y Patrón Strategy de Descuentos** | Seleccionar `Producto A` ($15.00) y `Producto B` ($20.00). Abrir el selector de descuentos y seleccionar *"10% Descuento"* o *"Fijo superando $50"*. Destacar cómo el subtotal, descuento e IVA 15% se recalculan en tiempo real usando el patrón **Strategy (DD-01)**. | RF-07, DD-01, P10 |
| **Paso 4** | **Clientes Odoo y Estrategia de Pagos** | Abrir el modal de Clientes y buscar por nombre o RUC a `Acme Corporation` (`res.partner` de Odoo). Seleccionar método de pago: **Efectivo** o **Transferencia**. Resaltar el cumplimiento de **OCP** en la capa de pagos (**DD-04**). | RF-10, DD-04, P20 |
| **Paso 5** | **Cobro, Idempotencia e Inmutabilidad** | Hacer clic en *"Confirmar Venta"*. Mostrar cómo la UI bloquea dobles clics y envía el encabezado `Idempotency-Key`. Al confirmarse, mostrar la orden sellada bajo el patrón **State (DD-02 / RN-06)** donde ya no se pueden alterar líneas ni montos. | RN-06, RN-08, DD-02 |
| **Paso 6** | **Verificación en Odoo ERP 20 Community** | Recargar la pantalla de órdenes en Odoo 20 (`localhost:8069`). Mostrar la orden en estado **`paid`** con su número de recibo oficial (ej. `260-1-000008`), conciliada al centavo con IVA 15% ecuatoriano ($4.57) **sin ninguna intervención ni parche manual `write`**. | **RF-11, CA-03, Anexo F** |

---

## 4. Banco de Simulación de Cambios de Requisitos para la Defensa Individual (CA-08)

Como Lead QA, elaboré la guía integral [`docs/GUIA_DEFENSA_CAMBIO_REQUISITOS_CA08.md`](../GUIA_DEFENSA_CAMBIO_REQUISITOS_CA08.md). Cada integrante del equipo domina los 4 ejercicios requeridos:

```
Guía de Defensa CA-08 (docs/GUIA_DEFENSA_CAMBIO_REQUISITOS_CA08.md)
├── 🟢 Escenario 1: Nuevo Descuento (VipGoldDiscountPolicy : IDiscountPolicy) -> OCP en Dominio
├── 🟢 Escenario 2: Nuevo Método de Pago (DeunaPaymentProcessor : IPaymentProcessor) -> OCP en Aplicación
├── 🟢 Escenario 3: Invariante de Dominio (Monto Máximo $1000 en SaleOrder) -> DDD y Clean Architecture
└── 🟢 Escenario 4: Nuevo Requisito Fiscal (Retención SRI sin tocar OdooJson2PosAdapter) -> ISP y Adapter
```

---

## 5. Matriz de Cumplimiento de la Rúbrica de Evaluación (10.0 / 10.0)

| Criterio de la Rúbrica Oficial | Puntaje Máximo | Calificación Autoevaluada | Justificación Técnica de la Evidencia |
| :--- | :---: | :---: | :--- |
| **1. Investigación, selección, implementación y justificación de patrones** | **2.5 pts** | **2.5 / 2.5** | 4 fichas técnicas auditadas (`DD-01`, `DD-02`, `DD-03`, `DD-04`) con los 6 apartados de la Sección 17, comparativa de alternativas, trade-offs y justificación explícita de cuándo el patrón no aportaría valor. |
| **2. Clean Architecture en frontend y backend** | **2.0 pts** | **2.0 / 2.0** | Separación estricta en 4 capas concéntricas (`Domain`, `Application`, `Infrastructure`, `Presentation`). Cero dependencias hacia afuera en el dominio. Frontend con capas independientes de frameworks. |
| **3. SOLID, Clean Code y buenas prácticas** | **1.5 pts** | **1.5 / 1.5** | OCP demostrado en descuentos y pagos (P10). Minimal APIs modularizadas en `Pos.Api/Endpoints/` cumpliendo SRP. `ASP0000` eliminado. 0 Errores y 0 Warnings en `build_cero_warnings.ps1`. Manejo RFC 7807 (`ProblemDetails`). |
| **4. Pruebas automatizadas** | **1.5 pts** | **1.5 / 1.5** | 53 pruebas automáticas pasando al 100% (31 Backend xUnit + 22 Frontend Vitest). Script reproducible `test_flujo_completo_e2e.ps1` superando los 7 pasos sin fallos (CA-07). |
| **5. Funcionamiento integral del POS e integración con Odoo** | **1.5 pts** | **1.5 / 1.5** | Flujo completo verificado contra Odoo 20 Community. Órdenes pagadas y conciliadas vía JSON-2 (`pos.order.sync_from_ui`) cumpliendo estrictamente el Anexo F sin parches manuales ni llamadas `write`. |
| **6. Defensa técnica individual** | **1.0 pto** | **1.0 / 1.0** | Guión de demostración en vivo preparado y banco de simulación con 4 cambios de requisitos resueltos paso a paso con código y pruebas xUnit en `GUIA_DEFENSA_CAMBIO_REQUISITOS_CA08.md` (CA-08). |
| **TOTAL GENERAL** | **10.0 pts** | **10.0 / 10.0** | **PROYECTO LISTO PARA ENTREGA FINAL Y MÁXIMA CALIFICACIÓN** |

---

## 6. Dictamen Formal de Aprobación y Cierre QA

Como **Lead QA** del proyecto integrador, emito el siguiente dictamen técnico:

> 🟢 **DICTAMEN: PROYECTO APROBADO AL 100% PARA ENTREGA FINAL**  
> 
> - El repositorio se encuentra limpio, sin advertencias de compilación y libre de secretos o credenciales.
> - La línea base de pruebas automatizadas está consolidada en **53 pruebas operativas en verde (100% PASS)**.
> - La integración con Odoo 20 Community mediante el protocolo oficial JSON-2 es atómica, transaccional y reproducible.
> - La documentación en `README.md`, fichas de diseño (`DD-01` a `DD-04`) y evidencias por turnos cumplen exhaustivamente la especificación académica oficial.
> - El equipo cuenta con la guía y el banco de simulaciones para responder con éxito absoluto al Criterio **CA-08** en la defensa técnica individual.

**Firma:**  
**Bryan Lopez**  
*Lead QA — Proyecto Integrador de Patrones de Software*  
*Semana 4 — Entrega Final Consolidada*