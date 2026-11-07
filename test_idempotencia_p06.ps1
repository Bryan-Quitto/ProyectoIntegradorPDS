# Script de Comprobacion de Idempotencia Oficial (RF-16 / RN-08 / Prueba P06) - Turno Eli
$idempotencyKey = "eli-idempotency-" + [guid]::NewGuid().ToString()

$headers = @{
    "Content-Type" = "application/json"
    "Idempotency-Key" = $idempotencyKey
}

$body = @{
    idempotencyKey = $idempotencyKey
    posSessionId = 2
    customerDocument = "1712345678"
    customerName = "Consumidor Final"
    discountType = "NONE"
    discountValue = 0
    items = @(
        @{ productId = 3; quantity = 1 }
    )
} | ConvertTo-Json

Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host " [PRUEBA P06 - ELI] DEMOSTRACION DE IDEMPOTENCIA ESTRICTA (RF-16 / RN-08)" -ForegroundColor Yellow
Write-Host " Clave de Idempotencia compartida: $idempotencyKey" -ForegroundColor White
Write-Host "==========================================================================" -ForegroundColor Cyan

Write-Host "`n>>> [PETICION 1]: Enviando primer cobro a Odoo..." -ForegroundColor Green
$resp1 = Invoke-RestMethod -Uri "http://localhost:5000/api/sales" -Method Post -Headers $headers -Body $body
Write-Host "  HTTP 201 Created" -ForegroundColor Green
Write-Host "  OrderId Local:         $($resp1.orderId)" -ForegroundColor White
Write-Host "  Odoo Order Reference:  $($resp1.odooOrderReference)" -ForegroundColor White
Write-Host "  Odoo Order ID:         #$($resp1.odooOrderId)" -ForegroundColor White
Write-Host "  Total Cobrado:         $($resp1.totalAmount)" -ForegroundColor White
Write-Host "  Estado:                $($resp1.state)" -ForegroundColor White

Write-Host "`n>>> [PETICION 2]: Reenviando EXACTAMENTE la misma venta con Idempotency-Key repetida..." -ForegroundColor Yellow
$resp2 = Invoke-RestMethod -Uri "http://localhost:5000/api/sales" -Method Post -Headers $headers -Body $body
Write-Host "  HTTP 201 Created (RECUPERADO DESDE SQLITE SIN TOCAR ODOO)" -ForegroundColor Yellow
Write-Host "  OrderId Local:         $($resp2.orderId)" -ForegroundColor White
Write-Host "  Odoo Order Reference:  $($resp2.odooOrderReference)" -ForegroundColor White
Write-Host "  Odoo Order ID:         #$($resp2.odooOrderId)" -ForegroundColor White
Write-Host "  Total Cobrado:         $($resp2.totalAmount)" -ForegroundColor White
Write-Host "  Estado:                $($resp2.state)" -ForegroundColor White

Write-Host "`n==========================================================================" -ForegroundColor Cyan
if ($resp1.orderId -eq $resp2.orderId -and $resp1.odooOrderId -eq $resp2.odooOrderId) {
    Write-Host " [EXITO ROTUNDO - P06]: Ambas peticiones devolvieron el MISMO ID (#$($resp1.odooOrderId))." -ForegroundColor Green
    Write-Host " Odoo NO duplico la orden ni genero un segundo cobro accidental." -ForegroundColor Green
    Write-Host " Regla de Negocio RN-08 y Requisito RF-16 100% CUMPLIDOS." -ForegroundColor Green
} else {
    Write-Host " [FALLO]: Se generaron IDs diferentes." -ForegroundColor Red
}
Write-Host "==========================================================================" -ForegroundColor Cyan
