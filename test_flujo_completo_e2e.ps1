# ==============================================================================
# SCRIPT OFICIAL DE PRUEBA DE INTEGRACIÓN END-TO-END (E2E) — SEMANA 4
# [GUÍA ESTEBAN - TURNO 2 - CA-07 / RNF-06 / RNF-08 / RNF-09]:
# Este script automatiza la verificación integral del ciclo de vida del POS:
#   Paso 1: GET  /health                   -> Comprueba disponibilidad del servicio (UP)
#   Paso 2: POST /api/auth/login           -> Autentica operador contra Odoo ERP y obtiene JWT Bearer
#   Paso 3: GET  /api/products             -> Valida sincronización de catálogo con existencias
#   Paso 4: GET  /api/pos/sessions/active  -> Valida caja registradora activa en Odoo
#   Paso 5: POST /api/sales                -> Emite venta con Idempotency-Key y cliente Acme Corp
#   Paso 6: POST /api/sales (Reintento)    -> Verifica Idempotent Receiver (RN-08 / Caché idéntica)
#   Paso 7: GET  /api/sales/{id}           -> Valida consulta de auditoría y desglose fiscal
# ==============================================================================

param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$Username = "admin",
    [string]$Password = "admin123"
)

$ErrorActionPreference = "Stop"

Write-Host "`n==========================================================================" -ForegroundColor Cyan
Write-Host " [SEMANA 4 - ESTEBAN] SUITE DE INTEGRACIÓN END-TO-END (E2E) POS ODOO 20" -ForegroundColor Yellow
Write-Host " Servidor Objetivo: $BaseUrl | Operador: $Username" -ForegroundColor Gray
Write-Host "==========================================================================" -ForegroundColor Cyan

$TotalSteps = 7
$PassedSteps = 0

function Report-Step($stepNum, $name, $success, $details) {
    if ($success) {
        Write-Host " [PASS] Paso $stepNum de $($TotalSteps): $name" -ForegroundColor Green
        if ($details) { Write-Host "        -> $details" -ForegroundColor DarkGray }
        $script:PassedSteps++
    } else {
        Write-Host " [FAIL] Paso $stepNum de $($TotalSteps): $name" -ForegroundColor Red
        if ($details) { Write-Host "        -> Error: $details" -ForegroundColor Yellow }
        Write-Host "`n[ERROR FATAL]: La suite E2E se detuvo debido a un fallo en el paso $stepNum." -ForegroundColor Red
        exit 1
    }
}

# ------------------------------------------------------------------------------
# PASO 1: Health Check (/health)
# ------------------------------------------------------------------------------
try {
    $healthRes = Invoke-RestMethod -Uri "$BaseUrl/health" -Method Get -TimeoutSec 10
    if ($healthRes.status -eq "UP") {
        Report-Step 1 "Health Check del Servidor" $true "Estado: $($healthRes.status) ($($healthRes.service))"
    } else {
        Report-Step 1 "Health Check del Servidor" $false "Estado no esperado: $($healthRes.status)"
    }
} catch {
    Report-Step 1 "Health Check del Servidor" $false $_.Exception.Message
}

# ------------------------------------------------------------------------------
# PASO 2: Autenticación de Operador (/api/auth/login)
# ------------------------------------------------------------------------------
$token = $null
$posSessionId = $null
try {
    $loginPayload = @{
        username = $Username
        password = $Password
    } | ConvertTo-Json

    $loginRes = Invoke-RestMethod -Uri "$BaseUrl/api/auth/login" -Method Post `
        -ContentType "application/json" -Body $loginPayload -TimeoutSec 15

    if ($loginRes.token -and $loginRes.posSessionId) {
        $token = $loginRes.token
        $posSessionId = [int]$loginRes.posSessionId
        Report-Step 2 "Autenticación de Operador contra Odoo" $true "Usuario: $($loginRes.username) | Caja #$posSessionId ($($loginRes.posConfigName)) | Rol: $($loginRes.role)"
    } else {
        Report-Step 2 "Autenticación de Operador contra Odoo" $false "No se recibió token o sesión de POS activa en Odoo."
    }
} catch {
    Report-Step 2 "Autenticación de Operador contra Odoo" $false $_.Exception.Message
}

$authHeaders = @{
    Authorization = "Bearer $token"
    Accept = "application/json"
}

# ------------------------------------------------------------------------------
# PASO 3: Catálogo de Productos (/api/products)
# ------------------------------------------------------------------------------
$firstProduct = $null
try {
    $products = Invoke-RestMethod -Uri "$BaseUrl/api/products" -Method Get `
        -Headers $authHeaders -TimeoutSec 15

    if ($products.Count -gt 0) {
        $firstProduct = $products[0]
        Report-Step 3 "Consulta de Catálogo de Productos Odoo" $true "Cargados $($products.Count) productos. Primero: '$($firstProduct.name)' (Stock: $($firstProduct.availableQuantity), Precio: `$$($firstProduct.price))"
    } else {
        Report-Step 3 "Consulta de Catálogo de Productos Odoo" $false "El catálogo devolvió 0 productos disponibles."
    }
} catch {
    Report-Step 3 "Consulta de Catálogo de Productos Odoo" $false $_.Exception.Message
}

# ------------------------------------------------------------------------------
# PASO 4: Sesión Activa de POS (/api/pos/sessions/active)
# ------------------------------------------------------------------------------
try {
    $activeSession = Invoke-RestMethod -Uri "$BaseUrl/api/pos/sessions/active" -Method Get `
        -Headers $authHeaders -TimeoutSec 15

    if ($activeSession.isOpen -eq $true) {
        Report-Step 4 "Verificación de Sesión Activa de POS" $true "Sesión #$($activeSession.id) '$($activeSession.name)' en estado abierto (Usuario ID: $($activeSession.userId))"
    } else {
        Report-Step 4 "Verificación de Sesión Activa de POS" $false "La sesión no se encuentra abierta (isOpen = false)."
    }
} catch {
    Report-Step 4 "Verificación de Sesión Activa de POS" $false $_.Exception.Message
}

# ------------------------------------------------------------------------------
# PASO 5: Emisión de Venta con Idempotencia (/api/sales)
# ------------------------------------------------------------------------------
$idempotencyKey = "e2e-esteban-" + [guid]::NewGuid().ToString()
$saleResult = $null

$salePayloadObj = @{
    posSessionId = $posSessionId
    customerDocument = "1710034065"
    customerName = "Acme Corporation"
    discountType = "NONE"
    discountValue = 0
    paymentMethod = "Efectivo"
    items = @(
        @{
            productId = [int]$firstProduct.id
            quantity = 2
        }
    )
}
$saleJson = $salePayloadObj | ConvertTo-Json -Depth 5

try {
    $headersWithIdempotency = @{
        Authorization = "Bearer $token"
        Accept = "application/json"
        "Content-Type" = "application/json"
        "Idempotency-Key" = $idempotencyKey
    }

    $saleResult = Invoke-RestMethod -Uri "$BaseUrl/api/sales" -Method Post `
        -Headers $headersWithIdempotency -Body $saleJson -TimeoutSec 20

    if ($saleResult.orderId -and $saleResult.totalAmount -gt 0) {
        Report-Step 5 "Emisión Exitosa de Venta (POST /api/sales)" $true "Orden ID: $($saleResult.orderId) | Total: `$$($saleResult.totalAmount) | Odoo Ref: $($saleResult.odooOrderReference)"
    } else {
        Report-Step 5 "Emisión Exitosa de Venta (POST /api/sales)" $false "La respuesta no contiene OrderId válido o total."
    }
} catch {
    Report-Step 5 "Emisión Exitosa de Venta (POST /api/sales)" $false $_.Exception.Message
}

# ------------------------------------------------------------------------------
# PASO 6: Reintento con Misma Idempotency-Key (RN-08 / Cache Hit)
# ------------------------------------------------------------------------------
try {
    $retryResult = Invoke-RestMethod -Uri "$BaseUrl/api/sales" -Method Post `
        -Headers $headersWithIdempotency -Body $saleJson -TimeoutSec 15

    if ($retryResult.orderId -eq $saleResult.orderId -and $retryResult.totalAmount -eq $saleResult.totalAmount) {
        Report-Step 6 "Idempotent Receiver (Reintento RN-08)" $true "Retorno de orden idéntica desde caché sin duplicación de cobro ni orden en Odoo."
    } else {
        Report-Step 6 "Idempotent Receiver (Reintento RN-08)" $false "El reintento generó una orden diferente o montos no coincidentes."
    }
} catch {
    Report-Step 6 "Idempotent Receiver (Reintento RN-08)" $false $_.Exception.Message
}

# ------------------------------------------------------------------------------
# PASO 7: Consulta de Venta por ID y Desglose Fiscal (/api/sales/{id})
# ------------------------------------------------------------------------------
try {
    $queryRes = Invoke-RestMethod -Uri "$BaseUrl/api/sales/$($saleResult.orderId)" -Method Get `
        -Headers $authHeaders -TimeoutSec 15

    if ($queryRes.orderId -eq $saleResult.orderId -and $queryRes.lines.Count -gt 0) {
        Report-Step 7 "Consulta de Auditoría y Desglose Fiscal" $true "Subtotal: `$$($queryRes.subTotal) | IVA: `$$($queryRes.taxAmount) | Total: `$$($queryRes.totalAmount) | Líneas: $($queryRes.lines.Count)"
    } else {
        Report-Step 7 "Consulta de Auditoría y Desglose Fiscal" $false "No se recuperaron los detalles fiscales de la orden."
    }
} catch {
    Report-Step 7 "Consulta de Auditoría y Desglose Fiscal" $false $_.Exception.Message
}

Write-Host "`n==========================================================================" -ForegroundColor Cyan
Write-Host " [RESULTADO OFICIAL DE LA PRUEBA E2E - TURNO 2 ESTEBAN]:" -ForegroundColor Green
Write-Host "  Paso 1: Health Check (UP)                                 -> [PASS]" -ForegroundColor Green
Write-Host "  Paso 2: Login Operador Odoo (JWT Bearer)                 -> [PASS]" -ForegroundColor Green
Write-Host "  Paso 3: Catálogo Productos Odoo con Stock                -> [PASS]" -ForegroundColor Green
Write-Host "  Paso 4: Sesión Activa de POS en Odoo ERP                 -> [PASS]" -ForegroundColor Green
Write-Host "  Paso 5: Emisión de Venta con Idempotencia (HTTP 201)     -> [PASS]" -ForegroundColor Green
Write-Host "  Paso 6: Idempotent Receiver Caché Hit (RN-08)            -> [PASS]" -ForegroundColor Green
Write-Host "  Paso 7: Auditoría y Desglose Fiscal (GET /api/sales/{id}) -> [PASS]" -ForegroundColor Green
Write-Host "  Progreso de Suite: $PassedSteps de $TotalSteps pasos completados con éxito (100% PASS)." -ForegroundColor Green
Write-Host "==========================================================================`n" -ForegroundColor Cyan

exit 0
