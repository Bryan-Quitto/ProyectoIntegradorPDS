# Evidencia de Integración Real: Pos.Api (.NET 10) $\rightarrow$ Odoo 20 Community (JSON-2)
**Asignatura:** Proceso de Desarrollo de Software (PDS) — Quinto Semestre  
**Fecha de Ejecución:** 29 de Octubre de 2026  
**Ambiente de Ejecución:**
- **ERP:** Odoo 20.0 Community en contenedor Docker `odoo20_web` (`http://localhost:8069`)
- **Base de Datos Odoo:** `pos_db` (PostgreSQL 16 en contenedor `odoo20_db`)
- **Backend POS:** ASP.NET Core (.NET 10.0) en `http://localhost:5000`
- **Protocolo de Integración:** Odoo JSON-2 API (`POST /json/2/{model}/{method}`) con autenticación Bearer por API Key (RNF-09)
- **Persistencia Local:** SQLite (`pos_local.db`) para control de idempotencia (RN-08) y auditoría de ventas

---

## 1. Verificación Previa del Entorno Odoo 20

### 1.1 Sesión Activa de POS en Odoo
- **Modelo:** `pos.session`
- **Filtro:** `[("state", "=", "opened")]`
- **Resultado:**
  - **ID de Sesión:** `4`
  - **Nombre:** `My Company/00002`
  - **Estado:** `opened`
  - **Usuario:** `Administrator` (ID: `2`)
  - **Configuración POS:** `My Company` (ID: `1`)

### 1.2 Catálogo Oficial de Productos Configurados para POS
- **Modelo:** `product.product`
- **Filtro:** `[("default_code", "in", ["PROD-001", "PROD-002"])]`
- **Resultado:**
  - **Producto A:** ID `3`, Código `PROD-001`, Precio de Lista `$15.00`, IVA 15% incluido (Impuesto ID `1`).
  - **Producto B:** ID `4`, Código `PROD-002`, Precio de Lista `$20.00`, IVA 15% incluido (Impuesto ID `1`).

### 1.3 Método de Pago en Efectivo Resuelto Dinámicamente
- **Modelo:** `pos.payment.method`
- **Filtro:** `[("type", "=", "cash"), ("config_ids", "in", [1])]`
- **Resultado:** ID `1` (`Efectivo`, diario asociado: `Efectivo My Company`).

---

## 2. Petición 1: Catálogo de Productos (`GET /api/products?search=`)

### Request HTTP
```http
GET /api/products?search= HTTP/1.1
Host: localhost:5000
Accept: application/json
```

### Response HTTP (200 OK)
```json
[
  {
    "id": 2,
    "name": "Service Fee",
    "price": 0.0,
    "barcode": "",
    "availableQuantity": 0.0,
    "defaultCode": "FEE",
    "taxRate": 0,
    "priceIncludesTax": false,
    "taxIds": []
  },
  {
    "id": 3,
    "name": "Producto A",
    "price": 15.0,
    "barcode": "",
    "availableQuantity": 0.0,
    "defaultCode": "PROD-001",
    "taxRate": 0.15,
    "priceIncludesTax": true,
    "taxIds": [1]
  },
  {
    "id": 4,
    "name": "Producto B",
    "price": 20.0,
    "barcode": "",
    "availableQuantity": 0.0,
    "defaultCode": "PROD-002",
    "taxRate": 0.15,
    "priceIncludesTax": true,
    "taxIds": [1]
  }
]
```

---

## 3. Petición 2: Sesión Activa de POS (`GET /api/pos/sessions/active`)

### Request HTTP
```http
GET /api/pos/sessions/active HTTP/1.1
Host: localhost:5000
Accept: application/json
```

### Response HTTP (200 OK)
```json
{
  "id": 4,
  "name": "My Company/00002",
  "state": "opened",
  "userId": 2,
  "isOpen": true
}
```

---

## 4. Petición 3: Venta Temprana de 2 Productos en Efectivo (`POST /api/sales`) — Sección 13

### Request HTTP
```http
POST /api/sales HTTP/1.1
Host: localhost:5000
Content-Type: application/json
Idempotency-Key: 1254aa45-03f5-425a-ace6-320ff6fe712b

{
  "posSessionId": 4,
  "customerDocument": "1712345678",
  "customerName": "Juan Perez",
  "discountType": "NONE",
  "discountValue": 0,
  "items": [
    {
      "productId": 3,
      "quantity": 1
    },
    {
      "productId": 4,
      "quantity": 1
    }
  ]
}
```

### Payload JSON-2 transmitido internamente hacia Odoo 20 (`pos.order.sync_from_ui`)
```json
{
  "orders": [
    {
      "uuid": "1254aa45-03f5-425a-ace6-320ff6fe712b",
      "session_id": 4,
      "state": "paid",
      "date_order": "2026-10-29 20:40:07",
      "amount_tax": 4.57,
      "amount_total": 35.0,
      "amount_paid": 35.0,
      "amount_return": 0.0,
      "to_invoice": false,
      "lines": [
        [
          0,
          0,
          {
            "uuid": "d1a8e940-5e36-47b1-948f-3617e9488a01",
            "product_id": 3,
            "full_product_name": "Producto A",
            "qty": 1,
            "price_unit": 15.0,
            "discount": 0.0,
            "price_subtotal": 13.04,
            "price_subtotal_incl": 15.0,
            "tax_ids": [[6, 0, [1]]]
          }
        ],
        [
          0,
          0,
          {
            "uuid": "f2b9fa51-6f47-48c2-a59e-4728f9599b02",
            "product_id": 4,
            "full_product_name": "Producto B",
            "qty": 1,
            "price_unit": 20.0,
            "discount": 0.0,
            "price_subtotal": 17.39,
            "price_subtotal_incl": 20.0,
            "tax_ids": [[6, 0, [1]]]
          }
        ]
      ],
      "payment_ids": [
        [
          0,
          0,
          {
            "amount": 35.0,
            "payment_method_id": 1,
            "payment_date": "2026-10-29 20:40:07"
          }
        ]
      ]
    }
  ]
}
```

### Response HTTP del Backend POS (201 Created)
```json
{
  "orderId": "1254aa45-03f5-425a-ace6-320ff6fe712b",
  "posSessionId": 4,
  "customerDocument": "1712345678",
  "customerName": "Juan Perez",
  "subTotal": 35.0,
  "discountAmount": 0.0,
  "taxAmount": 4.57,
  "totalAmount": 35.0,
  "state": "Synced",
  "odooOrderReference": "260-1-000003",
  "odooOrderId": 5,
  "createdAtUtc": "2026-10-29T20:40:07.0762264Z",
  "lines": [
    {
      "productId": 3,
      "productName": "Producto A",
      "quantity": 1,
      "unitPrice": 15.0,
      "subTotal": 15.0
    },
    {
      "productId": 4,
      "productName": "Producto B",
      "quantity": 1,
      "unitPrice": 20.0,
      "subTotal": 20.0
    }
  ]
}
```

---

## 5. Verificación de Idempotencia (RN-08 / RF-16)

Al re-enviar inmediatamente la misma solicitud con el encabezado `Idempotency-Key: 1254aa45-03f5-425a-ace6-320ff6fe712b`:
1. El backend consultó `SqliteIdempotencyStore`.
2. Identificó que la clave ya existía.
3. Retornó de forma inmediata el DTO cacheado con código `200 OK`, **sin disparar una segunda petición a Odoo 20 ni crear registros duplicados**.

---

## 6. Verificación Directa en la Base de Datos de Odoo 20

Consulta directa realizada vía API JSON-2 al modelo `pos.order` y `pos.order.line`:

### 6.1 Cabecera de la Orden (`pos.order` ID: 5)
```json
[
  {
    "id": 5,
    "name": "My Company - 000003",
    "pos_reference": "260-1-000003",
    "state": "paid",
    "amount_total": 35.0,
    "amount_tax": 4.57,
    "amount_paid": 35.0,
    "lines": [9, 10],
    "payment_ids": [3]
  }
]
```

### 6.2 Líneas de la Orden (`pos.order.line` IDs: 9 y 10)
```json
[
  {
    "id": 9,
    "product_id": [3, "[PROD-001] Producto A"],
    "qty": 1.0,
    "price_unit": 15.0,
    "price_subtotal": 13.04,
    "price_subtotal_incl": 15.0
  },
  {
    "id": 10,
    "product_id": [4, "[PROD-002] Producto B"],
    "qty": 1.0,
    "price_unit": 20.0,
    "price_subtotal": 17.39,
    "price_subtotal_incl": 20.0
  }
]
```

### 6.3 Desglose Contable
- **Base Imponible Total (Neto sin IVA):** $\$13.04 + \$17.39 = \$30.43$
- **IVA 15% Ecuador (Incluido en precio):** $\$1.96 + \$2.61 = \$4.57$
- **Total Pagado en Efectivo:** $\$35.00$
- **Estado en Odoo:** `paid` (Liquidado y conciliado con sesión activa `My Company/00002`).

---

## 7. Conclusión de la Auditoría

Se confirma que:
1. La comunicación bidireccional entre .NET 10 y Odoo 20 Community a través de la API JSON-2 es plenamente funcional.
2. Los precios se obtienen y validan de la fuente de verdad (RN-03).
3. La prueba temprana de venta de 2 productos pagados en efectivo (Sección 13) se completó con éxito técnico verificable.
4. Las reglas de no duplicidad (RN-08) y no falsos éxitos (RN-07) se cumplen de forma irrevocable.
