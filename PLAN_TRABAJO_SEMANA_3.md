# 📋 PLAN DE TRABAJO REFACTORIZADO — SEMANA 3
## Sistema Punto de Venta (POS) con Arquitectura Limpia e Integración Odoo 20 Community
**Asignatura:** Patrones de Software — Quinto Semestre  
**Marco de Referencia:** Especificación Académica Oficial (Plan de 4 Semanas) + Informe de Validación Técnica JSON-2 del Docente  
**Objetivo Central de la Semana 3:** Ventas, pagos, trazabilidad en Odoo ERP, historial transaccional, resiliencia contra fallos, idempotencia estricta y extensibilidad con principios SOLID (OCP / P10).

---

## 🎯 1. REQUISITO PREVIO INNEGOCIABLE (COMPROBACIÓN DE SALUD LOCAL)

> ⚠️ **REGLA DE ORO DEL EQUIPO:**  
> Ningún integrante puede comenzar a escribir código en su turno sin antes haber ejecutado y validado localmente la **`GUIA_PRUEBAS_SEMANAS_1_Y_2_POS.md`** en su propia máquina.

Antes de tocar una sola línea de código, cada integrante debe verificar en su terminal:
1. Contenedores Docker de Odoo 20 y PostgreSQL activos (`docker compose up -d` en `http://localhost:8069`).
2. Archivo `backend/.env` configurado con su API Key local y sesión de POS en estado **`opened`**.
3. Suite de pruebas de Backend (.NET 10 xUnit) en verde:
   ```powershell
   dotnet test backend/Pos.Tests/Pos.Tests.csproj
   # Debe mostrar: Superado: 13, Con error: 0 (100% PASS)
   ```
4. Suite de pruebas de Frontend (TypeScript Vitest) en verde:
   ```powershell
   cd frontend
   npm run test
   # Debe mostrar: 7 passed (7) (100% PASS)
   ```

Si alguna prueba falla, el entorno local está desincronizado. Se debe corregir antes de iniciar el turno.

---

## 🔄 2. PROTOCOLO DE RELEVOS EN GIT (`main`) Y PREVENCIÓN DE CONFLICTOS

Para trabajar de forma secuencial y coordinada sobre la rama `main` sin generar *merge conflicts* ni romper pruebas previas:

### A. Al iniciar tu turno:
```powershell
git checkout main
git pull origin main
# Re-ejecutar dotnet test y npm run test para garantizar línea base sana
```

### B. Delimitación Estricta sobre `PaymentModal.tsx`:
Dado que `PaymentModal.tsx` es un componente central del cobro, para evitar que un turno rompa los tests de Vitest del turno anterior, se asignan zonas exclusivas de edición:
* **Eli (Turno 1):** Trabaja en el **estado de procesamiento y bloqueo de botones** (prevención de doble clic para idempotencia) y en el **banner de alerta de errores de comunicación/sesión cerrada**.
* **Vero (Turno 2):** Trabaja en la **vista post-pago exitoso** (diálogo con ID de Odoo, referencia, líneas y desglose fiscal inmutable de la orden confirmada).
* **Esteban (Turno 3):** Trabaja en el **selector de método de pago** dentro del formulario de cobro y su paso al payload del caso de uso.

### C. Al finalizar tu turno:
1. Ejecuta nuevamente las suites de pruebas completas (backend y frontend). ¡Deben continuar en 100% PASS!
2. Realiza el commit y push descriptivo:
   ```powershell
   git add .
   git commit -m "feat(semana3): [Nombre Integrante] - [Módulo / Pruebas Pxx]"
   git push origin main
   ```
3. Notifica formalmente al siguiente compañero:  
   *"Turno finalizado. Tests pasando al 100%. `main` sincronizado en GitHub."*

---

## 🗓️ 3. CRONOGRAMA DE TURNOS Y ALINEACIÓN CON LA MATRIZ OFICIAL

| Turno | Horario | Responsable | Módulo Asignado | Requisitos y Pruebas Oficiales |
| :---: | :---: | :---: | :--- | :--- |
| **1** | **Sábado<br>08:00 a 15:00** | **Eli** | **Resiliencia, Timeouts, Sesión POS e Idempotencia Estricta** | **RF-15, RF-16, RF-17<br>RN-05, RN-07, RN-08<br>Pruebas: P03, P05, P06, P08** |
| **2** | **Sábado<br>15:00 a 21:00** | **Vero** | **Confirmación Real, Trazabilidad ERP, Inmutabilidad e Historial** | **RF-12, RF-13, RF-14<br>RN-06<br>Pruebas: P07 / Verificación RF-13 y RF-14** |
| **3** | **Domingo<br>08:00 a 15:00** | **Esteban** | **Extensibilidad de Pagos, SOLID (OCP) y Verificación de Precios** | **RF-09, RF-10, RN-03<br>RNF-04, RNF-07<br>Pruebas: P04, P10** |
| **4** | **Domingo<br>15:00 a 21:00** | **Bryan** | **Auditoría QA, Evidencia E2E Odoo (Anexo F), README y Cierre** | **RF-11, CA-03, CA-05<br>Prueba Oficial: P09 (Integración Real)** |

---

## 🔨 4. DETALLE TÉCNICO DE RESPONSABILIDADES POR INTEGRANTE

---

### 👤 TURNO 1: ELI (Sábado 08:00 a 15:00)
#### Módulo: Resiliencia, Manejo de Fallos, Sesión Activa y Control de Idempotencia (RF-15, RF-16, RF-17 / RN-05, RN-07, RN-08)
#### Pruebas Oficiales a Cargo: P03, P05, P06 y P08

* **Justificación frente a la Rúbrica:**  
  En el documento docente, la prevención de cobros duplicados figura explícitamente como **"No probada / No validada"**. Además, la regla RN-07 prohíbe terminantemente declarar falsos éxitos si Odoo no confirma la transacción.
* **Tareas a Desarrollar:**
  1. **Control de Idempotencia en Frontend y Backend (RF-16 / RN-08 / P06):**
     - En `PaymentModal.tsx`, asegurar que durante el cobro el botón "Confirmar Pago" se deshabilite inmediatamente (`disabled={processing}`) mostrando un spinner para impedir clics repetidos del cajero.
     - Garantizar que el frontend genere una cabecera `Idempotency-Key` (UUIDv4) única por intento de cobro y que el backend (`IIdempotencyStore` en SQLite) retorne la respuesta cacheada sin reenviar la orden a Odoo ante reintentos.
  2. **Manejo de Errores y Timeouts de Odoo (RF-17 / RN-07 / P05 y P08):**
     - Probar en vivo qué sucede cuando Odoo se cae (`docker stop odoo20_web`) mientras se intenta registrar una venta.
     - Verificar que `CheckoutSaleUseCase` en React traduzca las excepciones `ProblemDetails` (HTTP 502/504) a un mensaje amigable: *"Error de comunicación con Odoo ERP: servicio no disponible o timeout. No se debitó ningún valor."* Prohibido declarar éxito ficticio.
  3. **Validación de Sesión POS Cerrada (RF-15 / RN-05 / P03):**
     - Comprobar que si la sesión de Odoo está cerrada (`state != 'opened'`), el backend lance `ApplicationValidationException` y el frontend bloquee el cobro.
* **Entregables y Evidencia Documental:**
  - Crear el archivo: `docs/evidencias/semana3-eli-resiliencia-idempotencia.md`.
  - Capturar 2 evidencias en `docs/evidencias/img/`:
    - `s3-eli-p08-error-comunicacion-odoo.png` (Modal en React mostrando el error controlado sin falso éxito).
    - `s3-eli-p06-idempotencia-consola.png` (Captura de ejecución del script PowerShell demostrando que dos POST con la misma clave devuelven el mismo ID sin duplicar en Odoo).

---

### 👤 TURNO 2: VERO (Sábado 15:00 a 21:00)
#### Módulo: Trazabilidad en Odoo, Inmutabilidad de la Venta e Historial Transaccional (RF-12, RF-13, RF-14 / RN-06)
#### Pruebas Oficiales a Cargo: P07 / Verificación de Consulta de Orden (RF-13 y RF-14 en Matriz de Aceptación)

* **Justificación frente a la Rúbrica:**  
  La rúbrica exige que la interfaz refleje los identificadores y estados reales devueltos por Odoo (RF-12), permita auditar las operaciones previas (RF-13, RF-14) y garantice que una venta confirmada jamás pueda ser modificada como venta en borrador (RN-06). Maquetar impresiones es cosmético; la prioridad técnica es la **trazabilidad fiscal de datos reales**.
* **Tareas a Desarrollar:**
  1. **Inmutabilidad de la Orden Confirmada (RN-06):**
     - Verificar que el agregado `SaleOrder` (patrón State: `ConfirmedState` y `SyncedState`) lance `SaleOrderStateException` ante cualquier intento de invocar `AddLine`, `RemoveLine` o `SetDiscountPolicy` una vez confirmada la orden.
     - En el frontend, garantizar que tras el cobro exitoso el carrito se congele o limpie, sin permitir alterar líneas de la orden ya pagada.
  2. **Trazabilidad de Datos Reales de Odoo en Detalle (RF-12, RF-14 / Matriz de Aceptación):**
     - En `SaleDetailModal.tsx`, comprobar que se muestren todos los datos oficiales retornados por Odoo y almacenados en SQLite:
       - `name` / Referencia oficial de Odoo (ej. `260-1-000005`).
       - `odooOrderId` (ID numérico del ERP).
       - `state`: `paid` / Sincronizada.
       - Desglose contable: Base Imponible ($30.43), IVA 15% ($4.57), Total ($35.00), Nombre del Cajero y Nombre de Sesión.
  3. **Filtros Reactivos de Historial (RF-13):**
     - En `SalesHistoryPage.tsx`, asegurar que la búsqueda en tiempo real permita localizar ventas instantáneamente por número de recibo de Odoo, cédula o nombre del cliente.
  4. **Extra Visual Rápido (Opcional si sobra tiempo):**
     - Botón simple de impresión (`window.print()`) con CSS `@media print` en el modal de detalle para formato comprobante.
* **Entregables y Evidencia Documental:**
  - Crear el archivo: `docs/evidencias/semana3-vero-historial-trazabilidad.md`.
  - Capturar 2 evidencias en `docs/evidencias/img/`:
    - `s3-vero-rf14-detalle-venta-odoo.png` (Modal de detalle con todos los campos fiscales de Odoo).
    - `s3-vero-rf13-historial-busqueda.png` (Tabla de historial filtrada por referencia oficial de Odoo).

---

### 👤 TURNO 3: ESTEBAN (Domingo 08:00 a 15:00)
#### Módulo: Extensibilidad de Métodos de Pago, Verificación de Precios y Principio Abierto/Cerrado (RF-09, RF-10, RN-03 / RNF-04, RNF-07)
#### Pruebas Oficiales a Cargo: P04, P10 (Prueba Oficial de Extensibilidad SOLID)

* **Justificación frente a la Rúbrica:**  
  **¡ALERTA TÉCNICA!** Resolver RF-10 con un condicional `if (method != "Efectivo") throw Exception` **viola el Principio Abierto/Cerrado (OCP)** y **reprueba la prueba P10**. La especificación exige en P10: *"Demostrar con prueba un cambio controlado en una política o mecanismo sin alterar componentes no relacionados"*.
* **Tareas a Desarrollar:**
  1. **Arquitectura Extensible de Métodos de Pago (Patrón Strategy / Abstracción):**
     - Crear en el backend una abstracción extensible (ej. `IPaymentProcessor` o registro polimórfico de métodos de pago en `Pos.Application` o `Pos.Domain`).
     - Registrar la implementación por defecto: `CashPaymentProcessor` (que resuelve el diario de efectivo de Odoo).
     - Permitir registrar nuevos procesadores (ej. `BankTransferPaymentProcessor` o `CardPaymentProcessor`) sin modificar la lógica interna de `CreateSaleUseCase`.
  2. **Implementación de la Prueba P10 en xUnit:**
     - En `backend/Pos.Tests/PosTests.cs`, crear o enriquecer la prueba oficial **`P10_ExtensibilidadMetodosPago_PermiteNuevaEstrategiaSinModificarCasoDeUso`**, demostrando que se puede inyectar un método de pago simulado alternativo cumpliendo OCP.
  3. **Validación de Productos y Precios Oficiales (RF-09 / RN-03 / P04):**
     - Verificar que `CreateSaleUseCase` continúe consultando los precios e impuestos exclusivamente a Odoo (`GetProductsByIdsAsync`), rechazando productos no habilitados en POS (**P04**).
  4. **Selector en la Interfaz (RF-10):**
     - En `PaymentModal.tsx`, agregar un selector visual elegante donde aparezca seleccionado por defecto **Efectivo** (activo), junto a opciones secundarias informativas o extensibles.
* **Entregables y Evidencia Documental:**
  - Crear el archivo: `docs/evidencias/semana3-esteban-extensibilidad-pagos.md`.
  - Capturar 2 evidencias en `docs/evidencias/img/`:
    - `s3-esteban-rf10-selector-pagos-ui.png` (Selector de método de pago en el modal de React).
    - `s3-esteban-p10-test-extensibilidad-verde.png` (Terminal de PowerShell ejecutando `dotnet test --filter "P10"` en verde).

---

### 👤 TURNO 4: BRYAN (Domingo 15:00 a 21:00)
#### Módulo: Lead QA, Integración E2E en Odoo 20 (Anexo F), Consolidación de README y Cierre Semana 3 (RF-11, CA-03, CA-05)
#### Prueba Oficial a Cargo: P09 (Integración Real E2E contra Odoo ERP)

* **Justificación frente a la Rúbrica:**  
  En el informe docente de Postman, el método `sync_from_ui` figuraba como **"No ejecutado / No validado"**, y el docente tuvo que hacer parches manuales (`write` forzando `amount_paid`) para poder cerrar su orden. Bryan tiene la misión de consolidar la prueba irrefutable de que nuestra solución en .NET ejecuta la venta atómica y transaccional **sin parches manuales**, resolviendo el reto planteado en el **Anexo F** del documento docente.
* **Tareas a Desarrollar:**
  1. **Auditoría Global de Integración y Tests (QA):**
     - Ejecutar `git pull origin main` y compilar la solución completa.
     - Correr todas las suites automatizadas:
       - `dotnet test backend/Pos.Tests/Pos.Tests.csproj` (todas las pruebas P01 a P10 en verde).
       - `npm run test` en `frontend/` (todas las pruebas de Vitest en verde).
  2. **Ejecución y Evidencia E2E de la Prueba P09 (Anexo F Docente):**
     - Levantar Odoo 20 en Docker, abrir la sesión de caja e ingresar a la aplicación React.
     - Realizar una venta real de 2 productos (Producto A $15 y Producto B $20) pagada en efectivo ($35.00).
     - Ir a Odoo (`localhost:8069`) y consultar la orden con `pos.order/search_read` o en la vista del ERP.
     - Demostrar que Odoo registró:
       - `state`: `paid`
       - `amount_total`: `35.00`
       - `amount_tax`: `4.57` (IVA 15%)
       - `amount_paid`: `35.00`
       - **¡Cero escrituras manuales (`write`) diagnósticas! Transacción atómica nativa.**
  3. **Consolidación Oficial del `README.md`:**
     - Actualizar la tabla de Requisitos Funcionales (RF-01 a RF-17) y la Matriz de Reglas de Negocio (RN-01 a RN-08) del `README.md`.
     - Enlazar los reportes de evidencias generados por Eli, Vero y Esteban en el índice documental.
     - Verificar que no existan secretos ni archivos `.env` en el stage de Git.
  4. **Commit de Cierre y Etiquetado:**
     - Realizar el commit final de entrega de Semana 3 en `main`:
       ```powershell
       git add .
       git commit -m "chore(release): consolidacion final semana 3 - QA aprobado por Bryan (P01-P10 100% PASS)"
       git push origin main
       ```

---

## 📸 5. POLÍTICA DE DOCUMENTACIÓN, NOMENCLATURA Y PLANTILLA DE PATRONES (SECCIÓN 17)

1. **Edición exclusiva del `README.md`:**  
   👉 **SOLO BRYAN** edita el `README.md` el domingo por la noche para evitar conflictos de merge en `main`.

2. **Archivos de evidencia individuales y nomenclatura unificada de capturas:**  
   Cada integrante debe crear su archivo en `docs/evidencias/` usando estrictamente estos nombres de imagen en `docs/evidencias/img/`:
   * **Eli:** `docs/evidencias/semana3-eli-resiliencia-idempotencia.md`
     - `s3-eli-p08-error-comunicacion-odoo.png`
     - `s3-eli-p06-idempotencia-consola.png`
   * **Vero:** `docs/evidencias/semana3-vero-historial-trazabilidad.md`
     - `s3-vero-rf14-detalle-venta-odoo.png`
     - `s3-vero-rf13-historial-busqueda.png`
   * **Esteban:** `docs/evidencias/semana3-esteban-extensibilidad-pagos.md`
     - `s3-esteban-rf10-selector-pagos-ui.png`
     - `s3-esteban-p10-test-extensibilidad-verde.png`
   * **Bryan:** `docs/evidencias/semana3-bryan-auditoria-p09.md`
     - `s3-bryan-p09-orden-pagada-odoo.png`
     - `s3-bryan-qa-suites-verdes.png`

3. **Plantilla Obligatoria de Decisión de Diseño (Sección 17 del Sílabo / CA-05):**  
   Al final de su respectivo archivo `.md`, **Eli** (Idempotent Receiver), **Vero** (Patrón State en `SaleOrder`) y **Esteban** (Patrón Strategy en `IPaymentProcessor`) DEBEN completar esta tabla obligatoria para la defensa técnica:

   | Punto Exigido (Sección 17) | Descripción Técnica del Módulo |
   | :--- | :--- |
   | **1. Problema concreto** | ¿Qué fallo o rigidez ocurría si no aplicábamos este diseño? |
   | **2. Alternativas investigadas** | ¿Qué otra opción evaluamos (ej. `if/else`, validación simple en UI) y por qué se descartó? |
   | **3. Patrón / Solución y categoría** | Nombre del patrón (GoF, Empresarial o Práctica) y capa de Clean Architecture donde vive. |
   | **4. Consecuencias y compromisos** | Ventajas obtenidas (ej. OCP, testabilidad) vs. costo asumido (ej. más clases/interfaces). |
   | **5. Ubicación en el código** | Rutas exactas de las interfaces, clases y casos de uso implicados. |
   | **6. Prueba o demostración** | Nombre exacto del test en xUnit / Vitest (P01–P10) que lo verifica. |

4. **Nota sobre Persistencia (Sección 3) e Impuestos (RN-03):**
   * Los montos de subtotal, IVA 15% y total en la UI nunca se recalculan con fórmulas fijas en el navegador; se renderizan directamente desde los campos oficiales retornados por Odoo/Backend.
   * El uso de SQLite se documenta exclusivamente como almacén técnico de control de idempotencia (`IIdempotencyStore`) y bitácora de trazabilidad local vinculada al `odooOrderId`, sin sustituir a Odoo como fuente de verdad contable.

---

## 🏆 6. MATRIZ DE CRITERIOS DE ACEPTACIÓN PARA APROBACIÓN DE BRYAN

Antes de que Bryan haga el push de cierre el domingo a las 21:00, debe verificar este checklist:

- [x] **P01:** Venta sin productos rechazada con `DomainValidationException` (RN-01).
- [x] **P02:** Cantidad $\le 0$ rechazada por Value Object `Quantity` (RN-02).
- [x] **P03:** Sesión POS cerrada rechazada antes de llamar a Odoo (RN-05).
- [x] **P04:** Producto no existente en Odoo rechazado limpiamente (RN-03).
- [x] **P05 / P08:** Error de timeout/RPC de Odoo tratado sin declarar falsos éxitos (RN-07).
- [x] **P06:** Clave de idempotencia evita cobros dobles en Odoo y SQLite (RN-08 / RF-16).
- [x] **P07:** Carrito actualiza cantidades, subtotales e impuestos en tiempo real.
- [x] **P09:** Venta real de 2 productos y pago en efectivo conciliada en Odoo sin parches manuales.
- [x] **P10:** Extensibilidad de políticas o métodos de pago demostrada con prueba unitaria y principio OCP.
- [x] **RN-06:** Orden confirmada/sincronizada inmutable (bloqueada para edición).
- [x] **Docs:** 4 informes de evidencias creados con sus fotos (Eli, Vero, Esteban y Bryan con trazas P09/Anexo F) y `README.md` consolidado.
