# 🚀 GUÍA DE REPRODUCCIÓN Y PRUEBAS - SEMANAS 1 Y 2
## Sistema Punto de Venta (POS) con Arquitectura Limpia e Integración Odoo 20 Community

> ⚠️ **ADVERTENCIA FUNDAMENTAL PARA EL EQUIPO:**
> **NO compartimos la misma base de datos ni el mismo servidor de Odoo.**  
> Cada integrante corre su propia instancia local de Odoo 20 en Docker. Si no sigues los pasos de configuración inicial de Docker, `.env` y de la interfaz de Odoo, el backend no podrá autenticarse ni consultar productos, y las pruebas en vivo **FALLARÁN**.

---

## ÍNDICE
1. [Requisitos Previos de Software](#1-requisitos-previos-de-software)
2. [Instalación de Odoo 20 en Docker Local](#2-instalación-de-odoo-20-en-docker-local)
3. [Configuración Obligatoria dentro de la Interfaz Web de Odoo](#3-configuración-obligatoria-dentro-de-la-interfaz-web-de-odoo)
4. [Configuración del Backend (.env)](#4-configuración-del-backend-env)
5. [Levantamiento de los Servicios del Proyecto](#5-levantamiento-de-los-servicios-del-proyecto)
6. [Batería de Pruebas de Semanas 1 y 2 (Paso a Paso)](#6-batería-de-pruebas-de-semanas-1-y-2-paso-a-paso)
   - [Capa 1: Pruebas en Vivo en Interfaz React (UI)](#capa-1-pruebas-en-vivo-en-interfaz-react-ui)
   - [Capa 2: Verificación de Registros en Odoo ERP](#capa-2-verificación-de-registros-en-odoo-erp)
   - [Capa 3: Prueba de Idempotencia en Consola (RF-16)](#capa-3-prueba-de-idempotencia-en-consola-rf-16)
   - [Capa 4: Ejecución de Pruebas Automatizadas (Backend & Frontend)](#capa-4-ejecución-de-pruebas-automatizadas-backend--frontend)

---

## 1. REQUISITOS PREVIOS DE SOFTWARE

Asegúrate de tener instalado en tu máquina:
* **Docker Desktop** (con Docker Compose activo y corriendo).
* **.NET 10 SDK** (o versión correspondiente instalada para compilar C#).
* **Node.js v18+ o v20+** y `npm`.
* **PowerShell 7 (`pwsh`)** o PowerShell estándar de Windows.

---

## 2. INSTALACIÓN DE ODOO 20 EN DOCKER LOCAL

En la raíz del proyecto (`ProyectoIntegradorPDS/`), donde reside directamente el archivo `docker-compose.yml`, levanta los contenedores:

```powershell
docker compose up -d
```

Verifica que ambos contenedores estén en estado `running`:
```powershell
docker ps
```
Deberás ver `odoo20_web` escuchando en el puerto `8069` y `odoo20_db` en el puerto `5432`.

### Creación de la Base de Datos Inicial en Odoo:
1. Abre tu navegador e ingresa a: `http://localhost:8069`.
2. Odoo mostrará la pantalla de creación de base de datos (`database/manager`):
   * **Master Password**: Guarda la clave maestra que sugiere o define una segura.
   * **Database Name**: `pos_db` (¡Obligatorio usar este nombre o actualizarlo en tu `.env`!).
   * **Email**: Tu correo (ej. `tu_nombre@ejemplo.com`). Este será tu usuario de acceso.
   * **Password**: Tu contraseña personal (anótala bien).
   * **Language**: Spanish / Español.
   * **Country**: Ecuador (recomendado para la configuración fiscal de IVA 15%).
   * **Demo data**: Marca la casilla si deseas datos demo, o déjala desmarcada para crear los productos limpios.
3. Haz clic en **Create database** y espera a que finalice el aprovisionamiento.

### Generación de API Key en Odoo (Imprescindible para el Backend):
1. Inicia sesión en `http://localhost:8069` con tu Email y Contraseña.
2. Haz clic en tu avatar/nombre arriba a la derecha -> **Preferencias (Preferences)** o **Mi Perfil**.
3. Ve a la pestaña **Seguridad de la Cuenta (Account Security)**.
4. En la sección **Claves de API de Desarrollador (Developer API Keys)**, haz clic en **Nueva Clave de API (New API Key)**.
5. Asigna una descripción (ej. `POS Backend API`), confirma con tu contraseña y **COPIA LA CLAVE GENERADA**. Guárdala en un bloc de notas; Odoo no te la volverá a mostrar.

---

## 3. CONFIGURACIÓN OBLIGATORIA DENTRO DE LA INTERFAZ WEB DE ODOO

Para que la API .NET y el POS funcionen, debes realizar estas 4 configuraciones en Odoo:

### A. Instalar el Módulo de Punto de Venta (POS)
1. Ve a **Aplicaciones (Apps)** en el menú principal de Odoo.
2. Busca `Punto de Venta` (Point of Sale).
3. Haz clic en **Activar (Activate / Install)** y espera a que termine.

### B. Configurar el Impuesto IVA 15% (Ecuador)
1. Ve a **Contabilidad** o **Facturación** -> **Configuración** -> **Impuestos** (o desde Punto de Venta -> Configuración -> Ajustes).
2. Localiza o crea el impuesto sobre ventas:
   * **Nombre**: `VAT 15% G` o `IVA 15% Ventas`.
   * **Importe**: `15.00%`.
   * **Cálculo de Impuesto**: Porcentaje del precio.
   * ⚠️ **MUY IMPORTANTE**: En **Opciones Avanzadas**, activa la casilla **"Incluido en el precio" (Included in Price)**. El cálculo oficial del proyecto asume precios con IVA incluido.

### C. Crear los Productos Oficiales
Ve a **Punto de Venta** -> **Productos** -> **Productos** -> **Nuevo**:

* **Producto 1:**
  * **Nombre del Producto**: `Producto A`
  * **Tipo de Producto**: Almacenable o Consumible / Bien.
  * **Referencia Interna (Código)**: `PROD-001` (¡Exactamente este código!).
  * **Precio de Venta**: `$15.00`
  * **Impuestos del Cliente**: `VAT 15% G` (15% incluido).
  * **Pestaña Punto de Venta**: Marcar la casilla **"Disponible en Punto de Venta" (Available in POS)** = `Sí / Marcado`.

* **Producto 2:**
  * **Nombre del Producto**: `Producto B`
  * **Referencia Interna (Código)**: `PROD-002`
  * **Precio de Venta**: `$20.00`
  * **Impuestos del Cliente**: `VAT 15% G` (15% incluido).
  * **Pestaña Punto de Venta**: Marcar **"Disponible en Punto de Venta"** = `Sí / Marcado`.

* **Producto 3 (Opcional para pruebas adicionales):**
  * **Nombre del Producto**: `Producto C`
  * **Referencia Interna (Código)**: `PROD-003`
  * **Precio de Venta**: `$1.00`
  * **Disponible en Punto de Venta** = `Sí`.

### D. Abrir una Sesión de POS (¡Vital!)
El sistema bloquea ventas si no hay una sesión activa de Odoo:
1. Ve a **Punto de Venta** -> **Tablero (Dashboard)**.
2. En la caja principal (ej. *Shop* o *My Company*), haz clic en **Nueva Sesión (New Session)** o **Abrir Caja**.
3. Confirma el saldo de apertura y déjala en estado **"En proceso" / `opened`**.

---

## 4. CONFIGURACIÓN DEL BACKEND (.env)

En la carpeta `backend/` existe un archivo `.env`. Debes configurarlo con tus credenciales locales de Odoo:

Abre `backend/.env` y coloca:

```ini
# Odoo 20 Integration Settings
ODOO__BASEURL=http://localhost:8069
ODOO__DATABASE=pos_db
ODOO__USERNAME=tu_correo_de_odoo@ejemplo.com
ODOO__APIKEY=pega_aqui_tu_api_key_generada_en_odoo
ODOO__PASSWORD=tu_contraseña_o_api_key

# JWT Authentication Settings
JWT__SECURITYKEY=cL9+3oCqTiwNA2a+VLdMXM13ltpje3FtXseaY8NzKSE=
JWT__VALIDISSUER=PosApi
JWT__VALIDAUDIENCE=PosWebClient
JWT__EXPIRATIONMINUTES=120

# Local Persistence (SQLite - RN-08 Idempotency & Audit)
CONNECTIONSTRINGS__SQLITEDB=Data Source=pos_local.db

# ASP.NET Core Settings
ASPNETCORE_ENVIRONMENT=Development
PORT=5000
```

---

## 5. LEVANTAMIENTO DE LOS SERVICIOS DEL PROYECTO

Abre dos terminales de PowerShell:

### Terminal 1: Backend (.NET API)
```powershell
cd "ProyectoIntegradorPDS\backend\Pos.Api"
dotnet run
```
* Debe arrancar en `http://localhost:5000`.
* Prueba rápida en navegador: `http://localhost:5000/api/products` debe responder `200 OK` con el JSON de tus productos de Odoo.

### Terminal 2: Frontend (React + Vite)
```powershell
cd "ProyectoIntegradorPDS\frontend"
npm install
npm run dev
```
* Debe arrancar en `http://localhost:5173`.
* Abre `http://localhost:5173` en tu navegador.

---

## 6. BATERÍA DE PRUEBAS DE SEMANAS 1 Y 2 (PASO A PASO)

---

### CAPA 1: PRUEBAS EN VIVO EN INTERFAZ REACT (UI)

#### ✅ RF-01: Carga de Catálogo y Sesión Activa
1. Abre `http://localhost:5173`.
2. En la cabecera superior debe aparecer:
   * **Sesión Odoo #X: Nombre (opened)** con indicador en verde pulsante.
3. En el catálogo deben listarse `Producto A` ($15.00) y `Producto B` ($20.00).
4. Abre DevTools (F12) -> pestaña **Network**: la petición `GET /api/products` tiene estado `200 OK`.

#### ✅ RF-02: Búsqueda Reactiva y Estado Vacío
1. **Búsqueda exitosa:** Escribe `PROD-001` en la barra de búsqueda.
   * *Resultado esperado:* Filtra de inmediato y muestra únicamente `Producto A`.
2. **Búsqueda negativa:** Escribe `PROD-999`.
   * *Resultado esperado:* Muestra el mensaje informativo:
     > *"No se encontraron productos en Odoo. Verifica que existan productos con 'available_in_pos' en el ERP."*
     Sin errores en la consola.
3. Haz clic en **Limpiar búsqueda**.

#### ✅ RF-03: Agregar al Carrito y Regla de Carrito Vacío (RN-01)
1. Con el carrito en 0 ítems, verifica que el botón verde **"Cobrar e Imprimir"** se encuentra **deshabilitado**.
2. Haz clic en `Agregar Producto A al carrito`.
3. Haz clic en `Agregar Producto B al carrito`.
   * *Resultado esperado:* Ambos se agregan al panel derecho con cantidad `1` cada uno.

#### ✅ RF-04: Modificar Cantidades y Quitar Productos
1. En la línea de `Producto A`, haz clic en el botón `+` hasta subirlo a `3` ($45.00).
2. Haz clic en `-` para devolverlo a `1` ($15.00).
3. En `Producto B`, haz clic en el botón de la papelera (**Quitar ítem del carrito**).
   * *Resultado esperado:* Se despliega el modal interactivo de confirmación:
     > *"¿Quitar producto del carrito? Confirma si deseas remover este producto de la venta actual."*
4. Haz clic en **Quitar Producto**. El ítem desaparece y el subtotal se recalcula en tiempo real.
5. Vuelve a hacer clic en el catálogo para agregar `Producto B` nuevamente.

#### ✅ RF-05: Regla Fiscal y Desglose de Totales (RN-03)
Con 1 `Producto A` ($15.00) y 1 `Producto B` ($20.00) en el carrito:
* **Subtotal Bruto:** `$35.00`
* **Base Imponible (Neto sin IVA):** `$30.43`
* **IVA Ecuador (15%):** `$4.57`
* **Total a Cobrar:** `$35.00`
*(El cálculo demuestra que el IVA 15% está incluido según la regla fiscal ecuatoriana de Odoo).*

#### ✅ RF-06: Aplicar Política de Descuento (Patrón Strategy)
1. En la sección **Política de Descuento (Strategy)** del carrito, haz clic en **10% Promo**.
2. Verifica el recálculo instantáneo:
   * **Descuento aplicado:** `-$3.50`
   * **Base imponible:** `$27.39`
   * **IVA (15%):** `$4.11`
   * **Total a Liquidar:** `$31.50`
3. Vuelve a hacer clic en **Sin desc.** para dejar la orden en `$35.00` listos para el pago en efectivo.

#### ✅ RF-09 a RF-12: Venta en Efectivo y Sincronización en Odoo
1. Con los 2 productos en el carrito (Total $35.00), haz clic en **Cobrar e Imprimir**.
2. Se abre el modal **Finalizar Cobro POS**:
   * Cédula/RUC: `9999999999999` (Consumidor Final).
   * Nombre: `Consumidor Final`.
3. Haz clic en **Confirmar Pago**.
4. La interfaz muestra el spinner `Sincronizando...` y tras comunicarse con Odoo despliega el diálogo de éxito:
   * **¡Venta Sincronizada con Odoo!**
   * **Referencia POS:** ej. `260-1-000005`
   * **ID en Odoo ERP:** ej. `#8`
   * **Monto Cobrado:** `$35.00`
5. Haz clic en **Completar y Nueva Venta**. El carrito se vacía automáticamente.

---

### CAPA 2: VERIFICACIÓN DE REGISTROS EN ODOO ERP

1. Abre tu navegador en `http://localhost:8069`.
2. Ve a **Punto de Venta** -> **Órdenes** -> **Órdenes**.
3. Abre la orden recién generada (coincide con la referencia, ej. `260-1-000005`):
   * **Estado:** `Pagado` / `Paid`.
   * **Líneas:** 1x Producto A ($15.00) y 1x Producto B ($20.00).
   * **Impuestos:** $4.57 (VAT 15% G).
   * **Pagos:** Efectivo por $35.00.
4. En el Frontend, haz clic en el botón superior **Historial de Ventas**:
   * Aparecerá la tabla de auditoría local de SQLite sincronizada.
   * Haz clic en **Ver Detalle** para ver el desglose fiscal idéntico al de Odoo.

---

### CAPA 3: PRUEBA DE IDEMPOTENCIA EN CONSOLA (RF-16 / RN-08)

Ejecuta este script en PowerShell para demostrar que ante envíos repetidos con la misma clave de idempotencia, el backend **NO** duplica la orden en Odoo:

```powershell
$headers = @{
    "Content-Type" = "application/json"
    "Idempotency-Key" = "test-idem-demo-001"
}
$body = @{
    idempotencyKey = "test-idem-demo-001"
    posSessionId = 4
    customerDocument = "9999999999999"
    customerName = "Consumidor Final"
    discountType = "NONE"
    discountValue = 0
    items = @( @{ productId = 3; quantity = 1 } )
} | ConvertTo-Json

# Llamada 1 (Crea y sincroniza en Odoo)
$resp1 = Invoke-RestMethod -Uri "http://localhost:5000/api/sales" -Method POST -Headers $headers -Body $body
Write-Output "Venta 1 ID: $($resp1.orderId) | Odoo Ref: $($resp1.odooOrderReference)"

# Llamada 2 (Idéntica con la misma clave)
$resp2 = Invoke-RestMethod -Uri "http://localhost:5000/api/sales" -Method POST -Headers $headers -Body $body
Write-Output "Venta 2 ID: $($resp2.orderId) | Odoo Ref: $($resp2.odooOrderReference)"

Write-Output "Idempotencia Exitosa: $(($resp1.orderId -eq $resp2.orderId) -and ($resp1.odooOrderReference -eq $resp2.odooOrderReference))"
```

---

### CAPA 4: EJECUCIÓN DE PRUEBAS AUTOMATIZADAS (BACKEND & FRONTEND)

> 💡 **Nota sobre los nombres de los tests:**  
> En el código fuente, las pruebas están bautizadas en español e inglés. Para ejecutarlas sin errores, usa los filtros `FullyQualifiedName` indicados a continuación.

#### Pruebas del Backend (C# .NET xUnit):
Ejecuta desde la carpeta raíz del proyecto:

| Requisito / Regla | Comando Exacto en PowerShell | Qué Valida |
| :--- | :--- | :--- |
| **RN-01 / P01** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P01"` | Rechaza venta sin productos lanzando `DomainValidationException`. |
| **RN-02 / P02** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P02"` | Rechaza cantidades $\le 0$ (valores `0`, `-1`, `-10`). |
| **RF-15 / P03** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P03"` | Rechaza venta si la sesión de POS está cerrada o no existe. |
| **RF-15 / P04** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P04"` | Rechaza venta si el producto no existe o está deshabilitado en Odoo. |
| **RN-07 / P05** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P05"` | Error RPC o timeout de Odoo aborta sin declarar falso éxito. |
| **RN-08 / P06** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P06"` | Misma clave de idempotencia retorna orden previa sin llamar a Odoo. |
| **RF-07 / P10** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P10"` | Principio Open/Closed (SOLID): Inyecta nueva política VIP sin tocar `SaleOrder`. |
| **Happy Path / P13** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P13"` | Venta completa de 2 productos con desglose fiscal Odoo (IVA 15% incluido). |
| **Detalle / P14** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj --filter "FullyQualifiedName~P14"` | Consulta de orden completa por ID con todas sus líneas fiscales. |
| **SUITE COMPLETA** | `dotnet test backend/Pos.Tests/Pos.Tests.csproj` | **13 de 13 pruebas superadas (100% PASS)**. |

---

#### Pruebas del Frontend (TypeScript Vitest):
Ejecuta ubicándote en la carpeta `frontend/`:

```powershell
cd "ProyectoIntegradorPDS\frontend"
```

| Prueba | Comando Exacto en PowerShell | Qué Valida |
| :--- | :--- | :--- |
| **P07** | `npm run test -- -t "P07"` | Cálculo puro y determinista de carrito, totales, descuento e IVA 15% en `Cart.ts`. |
| **P08** | `npm run test -- -t "P08"` | Traducción de fallos de red/Odoo (`ProblemDetails`) sin declarar falso éxito en UI. |
| **P15** | `npm run test -- -t "P15"` | Caso de uso `GetSaleDetailUseCase` recupera orden y líneas completas. |
| **P16** | `npm run test -- -t "P16"` | Conversión de fechas UTC del backend a la zona horaria Ecuador (`UTC-5`). |
| **P17** | `npm run test -- -t "P17"` | Límites de cantidad en Carrito (permite 1 a 1000, bloquea $\le 0$ y $> 1000$). |
| **P18** | `npm run test -- -t "P18"` | Filtrado reactivo de catálogo por Nombre, Código de referencia y Todos. |
| **P19** | `npm run test -- -t "P19"` | Filtrado de historial por referencia Odoo, nombre y cédula. |
| **SUITE COMPLETA** | `npm run test` | **7 de 7 pruebas superadas (100% PASS)**. |

---

## 7. RESUMEN DE COMPROBACIÓN RÁPIDA ANTE EL DOCENTE

1. **Terminal Backend:** Corre `dotnet test backend/Pos.Tests/Pos.Tests.csproj` -> Muestra **13 superados**.
2. **Terminal Frontend:** Corre `npm run test` -> Muestra **7 superados**.
3. **Navegador:**
   * Muestra el catálogo cargando desde Odoo (`http://localhost:5173`).
   * Busca `PROD-001`.
   * Agrega Producto A y B ($35.00 con IVA 15% incluido: Base $30.43, IVA $4.57).
   * Muestra la estrategia de descuento (10% Promo -> Total $31.50).
   * Paga en efectivo -> Recibe `201 Created` y referencia oficial.
4. **Odoo:** Muestra en `http://localhost:8069` la orden en estado `Pagado` con su asiento de caja conciliado.
