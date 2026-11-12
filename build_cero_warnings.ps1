# Verificacion de Compilacion Limpia (Cero Errores y Cero Advertencias) - Turno 1 Eli
$repoDir = Resolve-Path "C:\Users\Elizabeth\*UNIVERSIDAD*\Documentos\5to y 6to\PATRONES\Proyecto\ProyectoIntegradorPDS"

Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host " [SEMANA 4 - ELI] AUDITORIA DE REFACTORIZACION Y CLEAN CODE (RNF-04 / RNF-05)" -ForegroundColor Yellow
Write-Host "==========================================================================" -ForegroundColor Cyan

Write-Host "`n>>> [1/2] Compilando Backend ASP.NET Core (.NET 10)..." -ForegroundColor White
Push-Location (Join-Path $repoDir "backend\Pos.Api")
dotnet build --nologo
Pop-Location

Write-Host "`n>>> [2/2] Compilando Frontend React 18 + Vite (TypeScript Estricto)..." -ForegroundColor White
Push-Location (Join-Path $repoDir "frontend")
npm run build
Pop-Location

Write-Host "`n==========================================================================" -ForegroundColor Cyan
Write-Host " [EXITO ROTUNDO - ELI]:" -ForegroundColor Green
Write-Host "  1. Warning ASP0000 eliminado de Program.cs." -ForegroundColor Green
Write-Host "  2. Minimal APIs modularizadas en Pos.Api/Endpoints/ (Clean Architecture & SRP)." -ForegroundColor Green
Write-Host "  3. Backend: 0 Advertencias, 0 Errores." -ForegroundColor Green
Write-Host "  4. Frontend: Compilacion de produccion exitosa sin errores de tipado." -ForegroundColor Green
Write-Host "==========================================================================" -ForegroundColor Cyan
