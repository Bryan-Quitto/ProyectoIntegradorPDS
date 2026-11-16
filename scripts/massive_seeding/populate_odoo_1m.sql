-- ============================================================================
-- SCRIPT DE POBLACIÓN MASIVA DE ALTO RENDIMIENTO (1 MILLÓN DE DATOS POR TABLA)
-- MOTOR: PostgreSQL 16 (Contenedor Docker: odoo20_db, BD: pos_db, Usuario: odoo)
-- PROYECTO: Sistema POS - PDS Quinto Semestre UTA
-- ============================================================================

\timing on

BEGIN;

-- Ajustes de sesión para maximizar el throughput de escritura en disco
SET LOCAL synchronous_commit = off;
SET LOCAL work_mem = '64MB';

DO $$
DECLARE
  v_base_partner_id int;
  v_base_prod_id int;
  v_base_order_id int;
  v_base_line_id int;
  v_base_payment_id int;
  
  v_session_id int;
  v_config_id int;
  v_user_id int;
  v_journal_id int;
  
  -- Configuración de volumen: 1 Millón de datos por tabla en 10 bloques de 100k
  v_total_records int := 1000000;
  v_chunk_size int := 100000;
  v_start int;
  v_end int;
  v_step int := 0;
  v_total_chunks int;
BEGIN
  v_total_chunks := v_total_records / v_chunk_size;

  -- 1. Obtener los IDs base actuales para no sobreescribir ni colisionar
  SELECT COALESCE(MAX(id), 0) INTO v_base_partner_id FROM res_partner;
  SELECT COALESCE(MAX(id), 0) INTO v_base_prod_id FROM product_template;
  SELECT COALESCE(MAX(id), 0) INTO v_base_order_id FROM pos_order;
  SELECT COALESCE(MAX(id), 0) INTO v_base_line_id FROM pos_order_line;
  SELECT COALESCE(MAX(id), 0) INTO v_base_payment_id FROM pos_payment;

  -- 2. Obtener sesión activa de POS y referencias operativas
  SELECT id, config_id, user_id INTO v_session_id, v_config_id, v_user_id 
  FROM pos_session WHERE state = 'opened' ORDER BY id DESC LIMIT 1;
  
  IF v_session_id IS NULL THEN
    SELECT id, config_id, user_id INTO v_session_id, v_config_id, v_user_id 
    FROM pos_session ORDER BY id DESC LIMIT 1;
  END IF;

  SELECT id INTO v_journal_id FROM account_journal WHERE type = 'sale' LIMIT 1;

  RAISE NOTICE '===============================================================';
  RAISE NOTICE 'INICIANDO INYECCIÓN MASIVA EN ODOO 20 (POSTGRESQL 16)';
  RAISE NOTICE 'Objetivo: % registros por tabla en % chunks de %', v_total_records, v_total_chunks, v_chunk_size;
  RAISE NOTICE 'Sesión POS: #%, Config POS: #%, Usuario: #%, Diario: #%', v_session_id, v_config_id, v_user_id, v_journal_id;
  RAISE NOTICE '===============================================================';

  -- 3. Bucle de Inyección por Lotes (Chunks)
  FOR chunk IN 0..(v_total_chunks - 1) LOOP
    v_step := v_step + 1;
    v_start := chunk * v_chunk_size + 1;
    v_end := (chunk + 1) * v_chunk_size;

    -- A. Clientes (res_partner) con Validación Algoritmo Módulo 10 SRI Ecuador
    WITH chunk_series AS (
      SELECT (v_base_partner_id + i) AS id, i AS rel_idx
      FROM generate_series(v_start, v_end) i
    ),
    test_calc AS (
      SELECT 
        id, rel_idx,
        LPAD(((rel_idx % 24) + 1)::text, 2, '0') AS prov,
        ((rel_idx / 24) % 6)::text AS d3,
        LPAD(((rel_idx / 144) % 1000000)::text, 6, '0') AS mid6
      FROM chunk_series
    ),
    digits AS (
      SELECT 
        id, rel_idx,
        prov || d3 || mid6 AS d9,
        (substring(prov, 1, 1))::int AS d1,
        (substring(prov, 2, 1))::int AS d2,
        d3::int AS d3,
        (substring(mid6, 1, 1))::int AS d4,
        (substring(mid6, 2, 1))::int AS d5,
        (substring(mid6, 3, 1))::int AS d6,
        (substring(mid6, 4, 1))::int AS d7,
        (substring(mid6, 5, 1))::int AS d8,
        (substring(mid6, 6, 1))::int AS d9_val
      FROM test_calc
    ),
    sums AS (
      SELECT 
        id, rel_idx, d9,
        (CASE WHEN d1*2 >= 10 THEN d1*2 - 9 ELSE d1*2 END +
         d2*1 +
         CASE WHEN d3*2 >= 10 THEN d3*2 - 9 ELSE d3*2 END +
         d4*1 +
         CASE WHEN d5*2 >= 10 THEN d5*2 - 9 ELSE d5*2 END +
         d6*1 +
         CASE WHEN d7*2 >= 10 THEN d7*2 - 9 ELSE d7*2 END +
         d8*1 +
         CASE WHEN d9_val*2 >= 10 THEN d9_val*2 - 9 ELSE d9_val*2 END) AS s
      FROM digits
    )
    INSERT INTO res_partner (
      id, name, complete_name, vat, email, phone, street, type, 
      customer_rank, supplier_rank, active, autopost_bills, 
      commercial_partner_id, create_uid, write_uid, create_date, write_date
    )
    SELECT 
      id,
      'Cliente Ec ' || LPAD(rel_idx::text, 7, '0'),
      'Cliente Ec ' || LPAD(rel_idx::text, 7, '0'),
      CASE WHEN rel_idx % 3 = 0 THEN d9 || ((10 - (s % 10)) % 10)::text || '001'
           ELSE d9 || ((10 - (s % 10)) % 10)::text END,
      'cliente' || rel_idx || '@posmail.ec',
      '09' || LPAD((rel_idx % 100000000)::text, 8, '0'),
      'Calle ' || (rel_idx % 500 + 1) || ' y Av. ' || ((rel_idx * 3) % 100 + 1),
      'contact', 1, 0, TRUE, 'ask',
      id, 1, 1, NOW(), NOW()
    FROM sums;

    -- B. Catálogo de Productos Template (product_template)
    INSERT INTO product_template (
      id, name, uom_id, type, service_tracking, base_unit_count,
      list_price, available_in_pos, sale_ok, purchase_ok, active,
      default_code, barcode, pos_sequence, create_uid, write_uid, create_date, write_date
    )
    SELECT 
      v_base_prod_id + i,
      jsonb_build_object('en_US', 'Producto POS ' || LPAD(i::text, 7, '0'), 'es_EC', 'Producto POS ' || LPAD(i::text, 7, '0')),
      1, 'consu', 'no', 0.0,
      ROUND((0.50 + ((i * 7 + 13) % 24950) / 100.0)::numeric, 2),
      TRUE, TRUE, TRUE, TRUE,
      'PROD-' || LPAD(i::text, 7, '0'),
      '786' || LPAD(i::text, 10, '0'),
      i, 1, 1, NOW(), NOW()
    FROM generate_series(v_start, v_end) i;

    -- C. Variantes de Producto 1:1 (product_product)
    INSERT INTO product_product (
      id, product_tmpl_id, base_unit_count, default_code, barcode,
      active, lst_price, create_uid, write_uid, create_date, write_date
    )
    SELECT 
      v_base_prod_id + i,
      v_base_prod_id + i,
      0.0,
      'PROD-' || LPAD(i::text, 7, '0'),
      '786' || LPAD(i::text, 10, '0'),
      TRUE,
      ROUND((0.50 + ((i * 7 + 13) % 24950) / 100.0)::numeric, 2),
      1, 1, NOW(), NOW()
    FROM generate_series(v_start, v_end) i;

    -- D. Relación Fiscal de Impuesto IVA 15% (product_taxes_rel)
    INSERT INTO product_taxes_rel (prod_id, tax_id)
    SELECT v_base_prod_id + i, 1
    FROM generate_series(v_start, v_end) i
    ON CONFLICT DO NOTHING;

    -- E. Órdenes de Venta POS (pos_order)
    WITH order_data AS (
      SELECT 
        v_base_order_id + i AS order_id,
        i AS rel_idx,
        ROUND((0.50 + ((i * 7 + 13) % 24950) / 100.0)::numeric, 2) AS total_amount,
        NOW() - (((i % 365)) || ' days')::interval - (((i % 86400)) || ' seconds')::interval AS dt_order,
        v_base_partner_id + ((i - 1) % v_total_records) + 1 AS partner_fk
      FROM generate_series(v_start, v_end) i
    )
    INSERT INTO pos_order (
      id, company_id, session_id, config_id, user_id, partner_id,
      sale_journal, sequence_number, name, pos_reference, state,
      uuid, access_token, ticket_code, tracking_number,
      amount_total, amount_tax, amount_paid, amount_return,
      date_order, create_date, write_date, create_uid, write_uid
    )
    SELECT 
      order_id, 1, v_session_id, v_config_id, v_user_id, partner_fk,
      v_journal_id, rel_idx, 'POS/' || LPAD(rel_idx::text, 8, '0'), 'Order ' || LPAD(rel_idx::text, 8, '0'), 'paid',
      gen_random_uuid()::text, gen_random_uuid()::text, LPAD((rel_idx % 10000)::text, 4, '0'), ((rel_idx % 99) + 1)::text,
      total_amount, ROUND((total_amount - (total_amount / 1.15)), 2), total_amount, 0.00,
      dt_order, dt_order, dt_order, 1, 1
    FROM order_data;

    -- F. Líneas de Detalle POS (pos_order_line)
    WITH line_data AS (
      SELECT 
        v_base_line_id + i AS line_id,
        v_base_order_id + i AS order_fk,
        v_base_prod_id + ((i - 1) % v_total_records) + 1 AS prod_fk,
        ROUND((0.50 + ((i * 7 + 13) % 24950) / 100.0)::numeric, 2) AS total_amount,
        NOW() - (((i % 365)) || ' days')::interval - (((i % 86400)) || ' seconds')::interval AS dt_order,
        i AS rel_idx
      FROM generate_series(v_start, v_end) i
    )
    INSERT INTO pos_order_line (
      id, company_id, order_id, product_id, name, full_product_name,
      qty, price_unit, price_subtotal, price_subtotal_incl,
      uuid, is_total_cost_computed, create_date, write_date, create_uid, write_uid
    )
    SELECT 
      line_id, 1, order_fk, prod_fk, 'Línea #' || rel_idx, 'Producto POS ' || LPAD(prod_fk::text, 7, '0'),
      1.0, total_amount, ROUND((total_amount / 1.15), 2), total_amount,
      gen_random_uuid()::text, TRUE, dt_order, dt_order, 1, 1
    FROM line_data;

    -- G. Vínculo Fiscal de Impuesto 15% en Líneas (account_tax_pos_order_line_rel)
    INSERT INTO account_tax_pos_order_line_rel (pos_order_line_id, account_tax_id)
    SELECT v_base_line_id + i, 1
    FROM generate_series(v_start, v_end) i
    ON CONFLICT DO NOTHING;

    -- H. Pagos POS (pos_payment)
    WITH pay_data AS (
      SELECT 
        v_base_payment_id + i AS payment_id,
        v_base_order_id + i AS order_fk,
        ROUND((0.50 + ((i * 7 + 13) % 24950) / 100.0)::numeric, 2) AS total_amount,
        NOW() - (((i % 365)) || ' days')::interval - (((i % 86400)) || ' seconds')::interval AS dt_order,
        CASE (i % 3) WHEN 0 THEN 1 WHEN 1 THEN 2 ELSE 6 END AS method_fk,
        i AS rel_idx
      FROM generate_series(v_start, v_end) i
    )
    INSERT INTO pos_payment (
      id, pos_order_id, session_id, company_id, payment_method_id,
      amount, payment_date, name, uuid, create_date, write_date, create_uid, write_uid
    )
    SELECT 
      payment_id, order_fk, v_session_id, 1, method_fk,
      total_amount, dt_order, 'Pago #' || rel_idx, gen_random_uuid()::text, dt_order, dt_order, 1, 1
    FROM pay_data;

    RAISE NOTICE '[Odoo Seeder] Lote %/% completado: registros % a % poblados con éxito.',
      v_step, v_total_chunks, v_start, v_end;
  END LOOP;

  -- 4. Actualización Obligatoria de Secuencias PostgreSQL
  PERFORM setval('res_partner_id_seq', (SELECT MAX(id) FROM res_partner));
  PERFORM setval('product_template_id_seq', (SELECT MAX(id) FROM product_template));
  PERFORM setval('product_product_id_seq', (SELECT MAX(id) FROM product_product));
  PERFORM setval('pos_order_id_seq', (SELECT MAX(id) FROM pos_order));
  PERFORM setval('pos_order_line_id_seq', (SELECT MAX(id) FROM pos_order_line));
  PERFORM setval('pos_payment_id_seq', (SELECT MAX(id) FROM pos_payment));

  RAISE NOTICE '===============================================================';
  RAISE NOTICE '¡SECUENCIAS DE POSTGRESQL SINCRONIZADAS CORRECTAMENTE!';
  RAISE NOTICE '===============================================================';
END $$;

COMMIT;

-- Actualización del optimizador del planificador de consultas de PostgreSQL
ANALYZE res_partner;
ANALYZE product_template;
ANALYZE product_product;
ANALYZE pos_order;
ANALYZE pos_order_line;
ANALYZE pos_payment;
