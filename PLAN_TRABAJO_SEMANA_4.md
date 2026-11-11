# 📋 PLAN DE TRABAJO — SEMANA 4 (ENTREGA FINAL)
## Sistema Punto de Venta (POS) con Arquitectura Limpia e Integración Odoo 20 Community
**Asignatura:** Patrones de Software — Quinto Semestre  
**Marco de Referencia:** Especificación Académica Oficial (Plan de 4 Semanas, Rúbrica de 10 Puntos y Criterios de Aceptación CA-01 a CA-08)  
**Objetivo Central de la Semana 4:** Refactorización, Clean Code, pruebas finales de resiliencia y concurrencia, documentación reproducible, demostración funcional integrada en vivo y preparación del equipo para la defensa técnica individual ante cambio de requisitos.

---

## 🎯 1. REQUISITO PREVIO INNEGOCIABLE (COMPROBACIÓN DE SALUD LOCAL)

> ⚠️ **REGLA DE ORO DEL EQUIPO:**  
> Ningún integrante puede comenzar su turno sin antes haber ejecutado localmente las suites completas de pruebas automatizadas y comprobado que la rama `main` se encuentra en **100% PASS**.

Antes de modificar o agregar cualquier archivo, cada integrante debe verificar en su terminal de PowerShell:
1. Contenedores Docker de Odoo 20 y PostgreSQL activos (`docker compose up -d` en `http://localhost:8069`).
2. Archivo `backend/.env` configurado con su API Key local y sesión de POS en estado **`opened`**.
3. Suite de pruebas de Backend (.NET 10 xUnit) en verde:
   ```powershell
   dotnet test backend/Pos.Tests/Pos.Tests.csproj
   # Debe reportar: Superado: 28, Con error: 0 (100% PASS)
   ```
4. Suite de pruebas de Frontend (TypeScript Vitest) en verde:
   ```powershell
   cd frontend
   npm run test
   # Debe reportar: 19 passed (19) (100% PASS)
   ```
5. **Línea Base Total:** **47 pruebas automatizadas operativas y en verde**.

Si alguna prueba falla, el entorno local está desincronizado. Se debe corregir antes de iniciar el turno.

---

## 🔄 2. PROTOCOLO DE RELEVOS EN GIT (`main`) Y PREVENCIÓN DE CONFLICTOS

Para garantizar un trabajo secuencial, fluido y sin conflictos sobre la rama `main`:

### A. Al iniciar tu turno:
```powershell
git checkout main
git pull origin main
# Re-ejecutar dotnet test y npm run test para garantizar línea base sana
```

### B. Delimitación de Áreas de Trabajo por Turno:
Para evitar sobreescrituras accidentales entre compañeros, las responsabilidades están estrictamente separadas por capas:
* **Bryan Quitto (Base Previa):** Base de arranque con módulos de Fase 4 (autenticación JWT por roles, aislamiento por caja Odoo y clientes `res.partner`).
* **Eli (Turno 1 - Miércoles):** Refactorización arquitectónica de `Pos.Api` (modularización de endpoints y eliminación de warnings) y tipado estricto en frontend.
* **Esteban (Turno 2 - Jueves):** Nuevas pruebas automatizadas de cobertura en `Pos.Tests` y automatización del script E2E en PowerShell.
* **Vero (Turno 3 - Viernes mañana):** Documentación maestra en `README.md`, consolidación de fichas de diseño (`docs/decisiones-diseno/`) y capturas en `docs/evidencias/img/`.
* **Bryan Lopez (Turno 4 - Viernes tarde):** Auditoría final de repositorio, guión de demostración en vivo, simulador de cambio de requisitos para la defensa individual y dictamen de cierre.

### C. Al finalizar tu turno:
1. Ejecutar las suites completas (`dotnet test` y `npm run test`) y verificar compilación limpia (`dotnet build` y `npm run build`).
2. Realizar commit y push descriptivo:
   ```powershell
   git add .
   git commit -m "<tipo>(semana4): <Nombre Integrante> - <Descripción del entregable>"
   git push origin main
   ```
3. Notificar formalmente al compañero del siguiente turno:  
   *"Turno finalizado. 47+ tests pasando al 100%. `main` sincronizado en GitHub."*

---

## 🗓️ 3. CRONOGRAMA DE TURNOS Y ALINEACIÓN CON LA RÚBRICA

| Turno | Horario | Responsable | Módulo Asignado | Requisitos y Criterios Oficiales |
| :---: | :---: | :---: | :--- | :--- |
| **0** | **Base Previa** | **Bryan Quitto** | **Base de Arranque y Módulos de Fase 4** | **Autenticación JWT por roles, asignación estricta de caja Odoo (`pos_session_id`), clientes `res.partner` y layout visual.** |
| **1** | **Miércoles<br>Todo el día** | **Eli** | **Refactorización, Clean Code y Cero Warnings** | **RNF-04, RNF-05 (Clean Code):**<br>• Modularizar Minimal APIs en `Pos.Api/Endpoints/`.<br>• Eliminar warning `ASP0000` en `Program.cs`.<br>• Tipado estricto y compilación limpia frontend/backend. |
| **2** | **Jueves<br>Todo el día** | **Esteban** | **Pruebas Finales Automatizadas y Script E2E** | **RNF-06, CA-07 (Pruebas Automatizadas):**<br>• Tests de aislamiento de caja y roles (HTTP 403 / Admin vs Cajero).<br>• Script PowerShell E2E (`test_flujo_completo_e2e.ps1`).<br>• Pruebas de resiliencia de red en frontend. |
| **3** | **Viernes<br>Hasta 15:00** | **Vero** | **Documentación Maestra, README y Fichas de Diseño** | **RNF-12, Secciones 7, 10 y 17 (Documentación):**<br>• Actualización total de `README.md` a Semana 4 (Entrega Final).<br>• Auditoría de Fichas Técnicas DD-01, DD-02 y DD-03.<br>• Actualización del banco de capturas en `docs/evidencias/img/`. |
| **4** | **Viernes<br>15:00 a Cierre** | **Bryan Lopez** | **Lead QA Final, Guión Demo y Banco de Defensa** | **CA-01, CA-08 (Demostración y Defensa Individual):**<br>• Auditoría de seguridad (cero secretos, verificación de `.gitignore`).<br>• Guión paso a paso de demostración en vivo.<br>• Banco de Simulación de Cambios de Requisitos (CA-08). |

---

## 🔨 4. DETALLE TÉCNICO DE RESPONSABILIDADES POR INTEGRANTE

---

### 👤 TRABAJO PREVIO: BRYAN QUITTO
#### Módulo: Autenticación con Roles, Aislamiento de Caja Odoo y Clientes `res.partner`
* **Justificación frente a la Rúbrica:**  
  Robustecimiento del sistema para operación multi-cajero real en retail, garantizando que ningún cajero opere en cajas ajenas y permitiendo facturar con clientes de Odoo.
* **Tareas Desarrolladas:**
  1. **Autenticación y Roles de Operador:** Implementación de endpoints y servicios JWT distinguiendo rol `Cashier` y `Administrator`.
  2. **Aislamiento por Caja Asignada:** Validación estricta en `POST /api/sales` para rechazar ventas si la sesión no coincide con la caja asignada en el token del cajero (retorno HTTP 403 Forbidden).
  3. **Integración con Clientes Odoo (`res.partner`):** Búsqueda y asociación de clientes reales en la orden de venta mediante `SearchCustomersUseCase`.
  4. **Mejoras de UI:** Modales interactivos tipo combobox, filtros dinámicos y layout horizontal optimizado para pantalla de caja.
* **Entregable:**
  * Commits base en `main`: `e605b60` y `f71e49d`.

---

### 👤 TURNO 1: ELI (Miércoles — Todo el día)
#### Módulo: Refactorización Arquitectónica, Clean Code y Cero Warnings (RNF-04, RNF-05)
* **Justificación frente a la Rúbrica:**  
  La Sección 9 exige *"Refactorización y Clean Code"*. La acumulación de endpoints en un solo archivo `Program.cs` degrada la mantenibilidad (RNF-10) y viola el Principio de Responsabilidad Única (SRP). Asimismo, las advertencias del compilador restan puntos en buenas prácticas.
* **Tareas a Desarrollar:**
  1. **Eliminación del Warning `ASP0000` en `Program.cs`:**
     - Refactorizar la configuración de JWT en `backend/Pos.Api/Program.cs` para obtener `jwtSecurityKey`, `jwtIssuer` y `jwtAudience` directamente desde `builder.Configuration` sin invocar `builder.Services.BuildServiceProvider()`.
     - Lograr que `dotnet build` reporte: **0 Errores y 0 Advertencias**.
  2. **Modularización de Minimal APIs (Clean Architecture & SRP):**
     - Crear el directorio `backend/Pos.Api/Endpoints/` y desacoplar los endpoints en métodos de extensión limpios:
       * `AuthEndpoints.cs`: Mapeo de `POST /api/auth/login`.
       * `ProductEndpoints.cs`: Mapeo de `GET /api/products`.
       * `CustomerEndpoints.cs`: Mapeo de `GET /api/customers`.
       * `PosSessionEndpoints.cs`: Mapeo de `GET /api/pos/sessions/active`.
       * `SaleEndpoints.cs`: Mapeo de `POST /api/sales`, `GET /api/sales` y `GET /api/sales/{id}`.
     - Dejar `Program.cs` como un *Composition Root* conciso, legible y elegante.
  3. **Clean Code y Tipado Estricto en Frontend:**
     - Auditar `frontend/src/` para eliminar cualquier uso residual de tipos `any`, verificando que `npm run build` ejecute `tsc -b && vite build` sin advertencias.
* **Entregables y Evidencia Documental:**
  - Crear el informe: `docs/evidencias/semana4-eli-refactorizacion-clean-code.md`.
  - Captura en consola de `dotnet build` y `npm run build` limpios en `docs/evidencias/img/s4-eli-build-cero-warnings.png`.
  - Commit: `refactor(semana4): Eli - modularizar endpoints minimal api y eliminar warning ASP0000`.

---

### 👤 TURNO 2: ESTEBAN (Jueves — Todo el día)
#### Módulo: Pruebas Finales Automatizadas, Cobertura E2E y Resiliencia (RNF-06, CA-07)
* **Justificación frente a la Rúbrica:**  
  El Criterio de Aceptación CA-07 exige que *"las pruebas se ejecuten mediante instrucciones reproducibles y se reporten sus resultados"*. Las nuevas funcionalidades de roles y caja deben contar con blindaje automático en xUnit y Vitest.
* **Tareas a Desarrollar:**
  1. **Pruebas de Aislamiento de Caja y Roles en Backend (xUnit):**
     - En `backend/Pos.Tests/PosTests.cs`, asegurar pruebas unitarias para:
       - Rechazo de venta cuando el cajero intenta registrar en una caja no autorizada (HTTP 403 Forbidden).
       - Comprobación de que el historial devuelve únicamente las ventas de la sesión asignada para cajeros, mientras que para administradores devuelve el consolidado total.
  2. **Script de Prueba de Integración E2E en PowerShell:**
     - Desarrollar en la raíz el script `test_flujo_completo_e2e.ps1` que valide en un solo comando el flujo HTTP completo contra el backend activo:
       1. `GET /health` $\rightarrow$ Verificar estado UP.
       2. `POST /api/auth/login` $\rightarrow$ Obtener JWT Bearer de operador.
       3. `GET /api/products?search=PROD` $\rightarrow$ Verificar catálogo Odoo.
       4. `GET /api/pos/sessions/active` $\rightarrow$ Verificar sesión abierta.
       5. `POST /api/sales` $\rightarrow$ Emitir venta con `Idempotency-Key` y verificar HTTP 201.
       6. `POST /api/sales` (Reintento con misma clave) $\rightarrow$ Verificar respuesta idéntica en caché (RN-08 / P06).
       7. `GET /api/sales/{id}` $\rightarrow$ Comprobar detalle con desglose fiscal.
  3. **Pruebas de Borde en Frontend (Vitest):**
     - Añadir en `src/__tests__/pos.spec.ts` pruebas que certifiquen el manejo de errores ante caídas de servidor y validación de cliente seleccionado.
* **Entregables y Evidencia Documental:**
  - Crear el script ejecutable: `test_flujo_completo_e2e.ps1`.
  - Crear el informe: `docs/evidencias/semana4-esteban-pruebas-finales-resiliencia.md`.
  - Capturas de terminal ejecutando las suites en verde y el script E2E en `docs/evidencias/img/s4-esteban-e2e-verdes.png`.
  - Commit: `test(semana4): Esteban - suites finales xunit vitest y script de ejecucion E2E`.

---

### 👤 TURNO 3: VERO (Viernes — Hasta las 15:00)
#### Módulo: Documentación Maestra, Actualización de README y Decisiones de Diseño (RNF-12, Secciones 7, 10 y 17)
* **Justificación frente a la Rúbrica:**  
  La Sección 10 y 17 exige un README reproducible con toda la configuración y el registro formal de al menos 3 decisiones de diseño justificadas con alternativas y código. Toda la documentación debe reflejar el cierre de la Semana 4.
* **Tareas a Desarrollar:**
  1. **Actualización Consolidada del `README.md`:**
     - Actualizar el estado de entrega a: **Semana 4 — Entrega Final Consolidada**.
     - Actualizar la tabla de pruebas automatizadas: reflejar el total consolidado (47+ tests passing al 100%).
     - Documentar los nuevos endpoints (`/api/customers`, `/api/auth/login`) y el uso del nuevo script de prueba `test_flujo_completo_e2e.ps1`.
     - Verificar que las instrucciones de levantamiento con Docker y configuración de Odoo sean 100% reproducibles por un evaluador externo.
  2. **Auditoría de Fichas de Decisión de Diseño (`docs/decisiones-diseno/`):**
     - Revisar que `DD-01` (Strategy Descuentos), `DD-02` (State Orden) y `DD-03` (Adapter + Builder + Idempotent Odoo) cumplan rigurosamente los 6 apartados exigidos en la Sección 17:
       1. Problema concreto.
       2. Alternativas investigadas (comparativa técnica).
       3. Solución elegida y categoría (GoF, Empresarial).
       4. Consecuencias y compromisos (*trade-offs*).
       5. Ubicación en código.
       6. Prueba o demostración verificable.
     - Garantizar que cada ficha justifique claramente **cuándo el patrón NO aportaría valor** y distinga entre Clean Architecture, SOLID y GoF.
  3. **Consolidación del Banco de Evidencias Gráficas:**
     - Organizar en `docs/evidencias/img/` capturas limpias y actualizadas de la interfaz de usuario con clientes, desglose de impuestos e historial.
* **Entregables y Evidencia Documental:**
  - Actualización de `README.md`.
  - Crear el informe: `docs/evidencias/semana4-vero-documentacion-evidencias.md`.
  - Commit: `docs(semana4): Vero - actualizar README entrega final y consolidar decisiones de diseno`.

---

### 👤 TURNO 4: BRYAN LOPEZ (Viernes — 15:00 a Cierre)
#### Módulo: Lead QA Final, Guión de Demostración y Preparación para la Defensa Individual (CA-01, CA-08)
* **Justificación frente a la Rúbrica:**  
  El Criterio de Aceptación CA-08 establece que *"la defensa demuestra comprensión individual y respuesta a un cambio de requisito"*. Bryan Lopez tiene la responsabilidad de asegurar que ningún secreto quede en el repositorio, redactar el guión de la demo y entrenar al equipo con un simulador de cambios de requisitos.
* **Tareas a Desarrollar:**
  1. **Auditoría de Seguridad y Cierre de Repositorio (RNF-09 / Sección 10):**
     - Comprobar con `git status` y revisión de commits que ningún archivo con contraseñas o API keys (`.env`, `pos_local.db`) esté rastreado por Git. Verificar la integridad de `.gitignore` y `.env.example`.
  2. **Guión de Demostración Funcional en Vivo (CA-01, CA-03):**
     - Redactar un guión conciso paso a paso para la exposición ante el docente:
       * Paso 1: Mostrar contenedores en Docker y sesión de Odoo abierta.
       * Paso 2: Login con credenciales de operador y explicación de caja asignada.
       * Paso 3: Selección de productos y aplicación de descuento demostrando Strategy.
       * Paso 4: Selección de cliente y método de pago (Efectivo/Transferencia).
       * Paso 5: Cobro exitoso y demostración de orden inmutable en React.
       * Paso 6: Verificación en Odoo ERP: orden en estado `paid` con número de recibo oficial y conciliación exacta de IVA 15%.
  3. **Banco de Simulación de Cambios de Requisitos para la Defensa Individual (CA-08):**
     - Crear un documento técnico con **4 ejercicios resueltos paso a paso** para que cualquier integrante del equipo pueda defender en vivo ante el docente sin titubeos:
       * **Escenario 1 (Nuevo Descuento):** Implementar `VipGoldDiscountPolicy : IDiscountPolicy` (descuento del 20% si supera $40) en `Pos.Domain/Strategies/` sin tocar `SaleOrder`.
       * **Escenario 2 (Nuevo Método de Pago):** Implementar `DeunaPaymentProcessor : IPaymentProcessor` en `Pos.Application/Strategies/` cumpliendo OCP.
       * **Escenario 3 (Nueva Regla de Validación):** Agregar invariante en `SaleOrder` (ej. "Monto máximo por orden $1000") demostrando que las reglas de negocio viven en el Dominio y no en los controladores.
       * **Escenario 4 (Nuevo Campo Fiscal):** Agregar código de retención de impuestos en la orden sin afectar el adaptador de Odoo.
  4. **Dictamen de Cierre y Aprobación QA:**
     - Revisión de la rúbrica completa de 10 puntos y emisión del dictamen formal de aprobación de la Semana 4.
* **Entregables y Evidencia Documental:**
  - Crear el documento: `docs/evidencias/semana4-bryan-auditoria-qa-defensa.md`.
  - Crear el simulador: `docs/GUIA_DEFENSA_CAMBIO_REQUISITOS_CA08.md`.
  - Commit: `chore(release): Bryan Lopez - dictamen final QA de semana 4 y banco de simulacion de defensa`.

---

## 📊 5. MATRIZ DE CRITERIOS DE EVALUACIÓN (RÚBRICA DE 10 PUNTOS)

| Criterio de la Rúbrica | Evidencia Concreta en el Proyecto | Responsables Principales |
| :--- | :--- | :---: |
| **1. Investigación, selección, implementación y justificación de patrones** | Fichas DD-01 (*Strategy*), DD-02 (*State*), DD-03 (*Adapter + Builder + Idempotent Receiver*) y *Strategy* en Métodos de Pago. Justificación de cuándo no aportan valor. | Todo el equipo / Vero |
| **2. Clean Architecture en frontend y backend** | Separación estricta de capas (`Domain`, `Application`, `Infrastructure`, `Presentation`). Dependencias apuntando hacia adentro. Cero acoplamiento a HTTP en el dominio. | Eli / Bryan Quitto |
| **3. SOLID, Clean Code y buenas prácticas** | OCP demostrado en descuentos y pagos (P10). SRP en casos de uso y endpoints modularizados. Cero warnings en compilación. Nombres expresivos y manejo RFC 7807 (`ProblemDetails`). | Eli / Esteban |
| **4. Pruebas automatizadas** | 47+ pruebas automatizadas pasando (28 xUnit + 19 Vitest). Cobertura completa de P01 a P10, RN-01 a RN-08 y script E2E en PowerShell. | Esteban |
| **5. Funcionamiento integral del POS e integración con Odoo** | Flujo completo verificado contra Odoo 20 Community sin parches manuales (`pos.order.sync_from_ui` vía JSON-2). Órdenes pagadas y conciliadas en el ERP. | Bryan Quitto / Vero |
| **6. Defensa técnica individual** | Guión de demo en vivo y Banco de Simulación con 4 cambios de requisitos resueltos para dominar cualquier pregunta del docente en vivo (CA-08). | Bryan Lopez / Todo el equipo |

---

## 🚀 6. INSTRUCCIONES RÁPIDAS PARA EMPEZAR EL TURNO

```powershell
# 1. Asegurar sincronización con la rama principal
git checkout main
git pull origin main

# 2. Levantar el ERP en segundo plano
docker compose up -d

# 3. Comprobar que los tests estén al 100%
dotnet test backend/Pos.Tests/Pos.Tests.csproj
cd frontend; npm run test; cd ..

# 4. Trabajar en las tareas asignadas a tu turno
# 5. Compilar sin warnings, hacer commit y push a main
```
