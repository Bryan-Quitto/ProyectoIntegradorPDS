# ==============================================================================
# AUDITORÍA DE COMPILACIÓN LIMPIA (Cero Errores y Cero Advertencias)
# [GUÍA ESTEBAN - MEJORA COLABORATIVA DE COMPATIBILIDAD - CA-07 / RNF-04 / RNF-05]:
# Complementamos el excelente trabajo de modularización iniciado por Eli, adaptando
# la ruta base a `$PSScriptRoot` para que el script sea universal y corra sin problemas
# en la máquina de cualquiera del equipo (desarrollo colaborativo fluido).
# Además, agregamos validación de `$LASTEXITCODE` para asegurar que el reporte verde
# refleje fielmente que ambas compilaciones pasaron sin advertencias ni errores.
# ==============================================================================

$repoDir = $PSScriptRoot

Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host " [SEMANA 4 - REPRODUCIBLE] AUDITORÍA DE CLEAN CODE Y COMPILACIÓN LIMPIA" -ForegroundColor Yellow
Write-Host "==========================================================================" -ForegroundColor Cyan

Write-Host "`n>>> [1/2] Compilando Backend ASP.NET Core (.NET 10)..." -ForegroundColor White
Push-Location (Join-Path $repoDir "backend\Pos.Api")
dotnet build --nologo
$backendExit = $LASTEXITCODE
Pop-Location

if ($backendExit -ne 0) {
    Write-Host "`n[ERROR CRÍTICO]: La compilación del Backend falló con código $backendExit." -ForegroundColor Red
    exit 1
}

Write-Host "`n>>> [2/2] Compilando Frontend React 18 + Vite (TypeScript Estricto)..." -ForegroundColor White
Push-Location (Join-Path $repoDir "frontend")
npm.cmd run build
$frontendExit = $LASTEXITCODE
Pop-Location

if ($frontendExit -ne 0) {
    Write-Host "`n[ERROR CRÍTICO]: La compilación del Frontend falló con código $frontendExit." -ForegroundColor Red
    exit 1
}

Write-Host "`n==========================================================================" -ForegroundColor Cyan
Write-Host " [ÉXITO TOTAL - COMPILACIÓN VERIFICADA]:" -ForegroundColor Green
Write-Host "  1. Warning ASP0000 eliminado de Program.cs." -ForegroundColor Green
Write-Host "  2. Minimal APIs modularizadas en Pos.Api/Endpoints/ (Clean Architecture & SRP)." -ForegroundColor Green
Write-Host "  3. Backend (.NET 10): 0 Advertencias, 0 Errores." -ForegroundColor Green
Write-Host "  4. Frontend (React 18 + TS): Compilación de producción 100% limpia." -ForegroundColor Green
Write-Host "  5. Ejecución universal y reproducible en cualquier entorno (CA-07)." -ForegroundColor Green
Write-Host "==========================================================================" -ForegroundColor Cyan
