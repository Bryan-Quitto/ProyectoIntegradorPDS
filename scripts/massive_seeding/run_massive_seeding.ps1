# ============================================================================
# ORQUESTADOR MAESTRO DE INYECCION MASIVA (1 MILLON DE REGISTROS POR TABLA)
# ENTORNO: PowerShell 7 (Windows)
# PROYECTO: Sistema POS - PDS Quinto Semestre UTA
# ============================================================================

[CmdletBinding()]
param(
    [switch]$SkipOdoo,
    [switch]$SkipSqlite
)

$ErrorActionPreference = "Stop"

Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "  PDS QUINTO SEMESTRE - INYECCION MASIVA DE RENDIMIENTO (1,000,000)" -ForegroundColor Cyan
Write-Host "========================================================================" -ForegroundColor Cyan

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$OdooSqlScript = Join-Path $ScriptDir "populate_odoo_1m.sql"
$SqlitePythonScript = Join-Path $ScriptDir "populate_sqlite_1m.py"
$SqliteDbPath = Join-Path $ScriptDir "..\..\backend\Pos.Api\pos_local.db"

# 1. Verificacion del Contenedor Docker de Odoo DB
if (-not $SkipOdoo) {
    Write-Host "`n[1/4] Verificando estado del contenedor Docker 'odoo20_db'..." -ForegroundColor Yellow
    $container = docker ps --filter "name=odoo20_db" --filter "status=running" --format "{{.Names}}"
    if (-not $container) {
        Write-Error "El contenedor Docker 'odoo20_db' no esta en ejecucion. Inicie los contenedores con 'docker-compose up -d'."
        exit 1
    }
    Write-Host "  -> Contenedor 'odoo20_db' activo y listo." -ForegroundColor Green
}

# 2. Inyeccion Masiva en Odoo ERP (PostgreSQL 16)
if (-not $SkipOdoo) {
    Write-Host "`n[2/4] Ejecutando inyeccion masiva en Odoo 20 (PostgreSQL 16)..." -ForegroundColor Yellow
    Write-Host "  -> Archivo SQL: $OdooSqlScript" -ForegroundColor DarkGray
    Write-Host "  -> Tablas: res_partner, product_template, product_product, pos_order, pos_order_line, pos_payment" -ForegroundColor DarkGray

    $swOdoo = [System.Diagnostics.Stopwatch]::StartNew()
    Get-Content -Path $OdooSqlScript -Raw | docker exec -i odoo20_db psql -U odoo -d pos_db
    $swOdoo.Stop()

    Write-Host "  -> Inyeccion en Odoo PostgreSQL completada en $($swOdoo.Elapsed.TotalSeconds.ToString('F2')) segundos." -ForegroundColor Green
} else {
    Write-Host "`n[2/4] Inyeccion en Odoo OMITIDA por parametro -SkipOdoo." -ForegroundColor DarkGray
}

# 3. Inyeccion Masiva en SQLite Local (SaleAuditRecords)
if (-not $SkipSqlite) {
    Write-Host "`n[3/4] Ejecutando inyeccion masiva en SQLite (.NET 10 Auditoria)..." -ForegroundColor Yellow
    Write-Host "  -> Script Python: $SqlitePythonScript" -ForegroundColor DarkGray
    Write-Host "  -> Tabla: SaleAuditRecords en $SqliteDbPath" -ForegroundColor DarkGray

    $swSqlite = [System.Diagnostics.Stopwatch]::StartNew()
    python $SqlitePythonScript
    $swSqlite.Stop()

    Write-Host "  -> Inyeccion en SQLite completada en $($swSqlite.Elapsed.TotalSeconds.ToString('F2')) segundos." -ForegroundColor Green
} else {
    Write-Host "`n[3/4] Inyeccion en SQLite OMITIDA por parametro -SkipSqlite." -ForegroundColor DarkGray
}

# 4. Auditoria y Verificacion de Conteos Finales
Write-Host "`n[4/4] Verificando conteos en ambas bases de datos..." -ForegroundColor Yellow

if (-not $SkipOdoo) {
    Write-Host "`n--- CONTEO EN ODOO 20 (pos_db en Docker) ---" -ForegroundColor Cyan
    docker exec -i odoo20_db psql -U odoo -d pos_db -c "
    SELECT 
      (SELECT count(*) FROM res_partner) AS clientes_res_partner,
      (SELECT count(*) FROM product_template) AS plantillas_producto,
      (SELECT count(*) FROM product_product) AS variantes_producto,
      (SELECT count(*) FROM pos_order) AS ordenes_pos,
      (SELECT count(*) FROM pos_order_line) AS lineas_detalle_pos,
      (SELECT count(*) FROM pos_payment) AS pagos_pos;
    "
}

if (-not $SkipSqlite -and (Test-Path $SqliteDbPath)) {
    Write-Host "--- CONTEO EN SQLITE (.NET 10 pos_local.db) ---" -ForegroundColor Cyan
    python -c "
import sqlite3
conn = sqlite3.connect(r'$SqliteDbPath')
cur = conn.cursor()
cur.execute('SELECT count(*) FROM SaleAuditRecords')
print(f'Auditoria Local (SaleAuditRecords): {cur.fetchone()[0]:,} registros')
conn.close()
"
}

Write-Host "`n========================================================================" -ForegroundColor Green
Write-Host "  POBLACION MASIVA DE 1 MILLON DE REGISTROS COMPLETADA EXITOSAMENTE!" -ForegroundColor Green
Write-Host "========================================================================" -ForegroundColor Green
