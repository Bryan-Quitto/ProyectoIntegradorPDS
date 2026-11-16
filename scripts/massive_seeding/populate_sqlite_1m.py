#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
=============================================================================
SCRIPT DE POBLACIÓN MASIVA DE ALTO RENDIMIENTO (1 MILLÓN DE REGISTROS)
MOTOR: SQLite 3 (Base local de auditoría: backend/Pos.Api/pos_local.db)
TABLA: SaleAuditRecords
PROYECTO: Sistema POS - PDS Quinto Semestre UTA
=============================================================================
"""

import os
import sys
import time
import uuid
import sqlite3
from datetime import datetime, timedelta

def get_db_path() -> str:
    # Ruta relativa al directorio de scripts o ruta absoluta por defecto
    script_dir = os.path.dirname(os.path.abspath(__file__))
    candidates = [
        os.path.abspath(os.path.join(script_dir, "..", "..", "backend", "Pos.Api", "pos_local.db")),
        r"C:\Users\THINKPAD\Desktop\UTA\QUINTO SEMESTRE\PDS\ProyectoIntegradorPDS\backend\Pos.Api\pos_local.db"
    ]
    for path in candidates:
        if os.path.exists(path):
            return path
    # Si no existe en las rutas anteriores, usar la primera ruta candidata (se creará)
    return candidates[0]

def generate_ecuadorian_id(rel_idx: int) -> str:
    """
    Generador Módulo 10 SRI Ecuador (Persona Natural / RUC Natural):
    - Provincia (dígitos 1-2): 01 a 24
    - Tercer dígito: 0 a 5
    - Dígitos centrales 4-9: 000000 a 999999
    - Coeficientes SRI: [2, 1, 2, 1, 2, 1, 2, 1, 2]
    - Verificador: (10 - (suma % 10)) % 10
    - Si rel_idx % 3 == 0 genera RUC terminación 001, sino Cédula de 10 dígitos.
    """
    prov = (rel_idx % 24) + 1
    d3 = (rel_idx // 24) % 6
    mid6 = (rel_idx // 144) % 1000000
    d9_str = f"{prov:02d}{d3}{mid6:06d}"
    
    coefs = (2, 1, 2, 1, 2, 1, 2, 1, 2)
    s = sum(
        (int(c) * coefs[idx] - 9) if int(c) * coefs[idx] >= 10 else int(c) * coefs[idx]
        for idx, c in enumerate(d9_str)
    )
    verifier = (10 - (s % 10)) % 10
    cedula = f"{d9_str}{verifier}"
    
    if rel_idx % 3 == 0:
        return f"{cedula}001"
    return cedula

def main():
    db_path = get_db_path()
    print("=" * 70)
    print("INICIANDO INYECCIÓN MASIVA EN SQLITE LOCAL (SaleAuditRecords)")
    print(f"Ruta de base de datos: {db_path}")
    print("Objetivo: 1,000,000 de registros de auditoría de ventas")
    print("=" * 70)

    if not os.path.exists(db_path):
        print(f"ERROR: No se encontró la base de datos en {db_path}.")
        sys.exit(1)

    conn = sqlite3.connect(db_path)
    cur = conn.cursor()

    # Optimizaciones de motor SQLite para inserción ultra rápida
    cur.execute("PRAGMA synchronous = OFF;")
    cur.execute("PRAGMA journal_mode = WAL;")
    cur.execute("PRAGMA cache_size = 100000;")

    # Obtener el número base actual de registros
    cur.execute("SELECT count(*) FROM SaleAuditRecords")
    initial_count = cur.fetchone()[0]
    print(f"Registros iniciales en SaleAuditRecords: {initial_count:,}")

    total_records = 1_000_000
    batch_size = 100_000
    payment_methods = ["Efectivo", "Tarjeta", "Transferencia"]
    base_date = datetime.utcnow()

    start_time = time.time()
    inserted_so_far = 0

    insert_query = """
    INSERT INTO SaleAuditRecords (
        OrderId, PosSessionId, CustomerDocument, CustomerName, SubTotal,
        DiscountAmount, TaxAmount, TotalAmount, State, OdooOrderReference,
        OdooOrderId, OrderLinesJson, SyncedAtUtc, SessionName, OperatorName,
        DiscountPolicyName, PaymentMethod, AmountPaid
    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    """

    for b in range(0, total_records, batch_size):
        chunk = []
        batch_start_idx = b + 1
        batch_end_idx = b + batch_size
        
        for i in range(batch_start_idx, batch_end_idx + 1):
            order_guid = str(uuid.uuid4()).upper()
            doc = generate_ecuadorian_id(i)
            
            # Cálculo fiscal coherente: IVA 15%
            total_val = round(0.50 + ((i * 7 + 13) % 24950) / 100.0, 2)
            subtotal_val = round(total_val / 1.15, 2)
            tax_val = round(total_val - subtotal_val, 2)
            
            pay_method = payment_methods[i % 3]
            dt_str = (base_date - timedelta(days=i % 365, seconds=i % 86400)).strftime("%Y-%m-%d %H:%M:%S")
            lines_json = f'[{{"ProductId":{i},"ProductName":"Producto POS {i:07d}","Quantity":1,"UnitPrice":{total_val},"SubTotal":{total_val}}}]'
            
            chunk.append((
                order_guid,
                4,                     # PosSessionId
                doc,                   # CustomerDocument (Cédula o RUC válido)
                f"Cliente Ec {i:07d}",  # CustomerName
                f"{subtotal_val:.2f}", # SubTotal
                "0.00",                # DiscountAmount
                f"{tax_val:.2f}",      # TaxAmount
                f"{total_val:.2f}",    # TotalAmount
                "Synced",              # State
                f"Order {i:08d}",      # OdooOrderReference
                i,                     # OdooOrderId
                lines_json,            # OrderLinesJson
                dt_str,                # SyncedAtUtc
                "POS/0001",            # SessionName
                "Cajero POS",          # OperatorName
                "Sin Descuento",       # DiscountPolicyName
                pay_method,            # PaymentMethod
                f"{total_val:.2f}"     # AmountPaid
            ))
            
        cur.executemany(insert_query, chunk)
        conn.commit()
        inserted_so_far += len(chunk)
        elapsed_chunk = time.time() - start_time
        print(f"[SQLite Seeder] Lote {inserted_so_far:,} / {total_records:,} insertado ({elapsed_chunk:.2f}s transcurridos)")

    # Creación y verificación de índices de alto rendimiento en SQLite
    print("-" * 70)
    print("Creando índices para optimizar consultas de paginación e historial...")
    cur.execute('CREATE INDEX IF NOT EXISTS "IX_SaleAuditRecords_PosSessionId_SyncedAtUtc" ON "SaleAuditRecords" ("PosSessionId", "SyncedAtUtc" DESC);')
    cur.execute('CREATE INDEX IF NOT EXISTS "IX_SaleAuditRecords_SyncedAtUtc" ON "SaleAuditRecords" ("SyncedAtUtc" DESC);')
    cur.execute('CREATE INDEX IF NOT EXISTS "IX_SaleAuditRecords_CustomerDocument" ON "SaleAuditRecords" ("CustomerDocument");')
    conn.commit()

    total_time = time.time() - start_time
    cur.execute("SELECT count(*) FROM SaleAuditRecords")
    final_count = cur.fetchone()[0]

    print("=" * 70)
    print(f"¡INYECCIÓN EN SQLITE FINALIZADA CON ÉXITO EN {total_time:.2f} SEGUNDOS!")
    print(f"Total final de registros en SaleAuditRecords: {final_count:,}")
    print("=" * 70)

    conn.close()

if __name__ == "__main__":
    main()
