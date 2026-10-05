-- ============================================================
-- ShareSync - Seed Data: Company Price History
-- File: 09_insert_company_price_history.sql
-- Description: Baseline historical prices for initial equities.
-- ============================================================

INSERT INTO company_price_history (company_id, price, open_price, high_price, low_price, volume, recorded_at)
VALUES (1, 400.00, 398.00, 402.50, 396.00, 45000, SYSTIMESTAMP - INTERVAL '30' DAY);

INSERT INTO company_price_history (company_id, price, open_price, high_price, low_price, volume, recorded_at)
VALUES (1, 405.50, 401.00, 407.00, 400.50, 52000, SYSTIMESTAMP - INTERVAL '20' DAY);

INSERT INTO company_price_history (company_id, price, open_price, high_price, low_price, volume, recorded_at)
VALUES (1, 408.00, 405.00, 410.00, 404.00, 61000, SYSTIMESTAMP - INTERVAL '10' DAY);

INSERT INTO company_price_history (company_id, price, open_price, high_price, low_price, volume, recorded_at)
VALUES (1, 410.50, 408.00, 412.00, 407.50, 58000, SYSTIMESTAMP - INTERVAL '1' DAY);

INSERT INTO company_price_history (company_id, price, open_price, high_price, low_price, volume, recorded_at)
VALUES (2, 112.00, 110.00, 113.50, 109.50, 85000, SYSTIMESTAMP - INTERVAL '30' DAY);

INSERT INTO company_price_history (company_id, price, open_price, high_price, low_price, volume, recorded_at)
VALUES (2, 115.00, 112.50, 116.00, 112.00, 92000, SYSTIMESTAMP - INTERVAL '20' DAY);

INSERT INTO company_price_history (company_id, price, open_price, high_price, low_price, volume, recorded_at)
VALUES (2, 117.20, 115.00, 118.00, 114.50, 78000, SYSTIMESTAMP - INTERVAL '10' DAY);

INSERT INTO company_price_history (company_id, price, open_price, high_price, low_price, volume, recorded_at)
VALUES (2, 118.40, 117.00, 119.50, 116.50, 81000, SYSTIMESTAMP - INTERVAL '1' DAY);

INSERT INTO company_price_history (company_id, price, open_price, high_price, low_price, volume, recorded_at)
VALUES (3, 450.00, 448.00, 455.00, 445.00, 31000, SYSTIMESTAMP - INTERVAL '30' DAY);

INSERT INTO company_price_history (company_id, price, open_price, high_price, low_price, volume, recorded_at)
VALUES (3, 458.00, 451.00, 460.00, 450.00, 36000, SYSTIMESTAMP - INTERVAL '15' DAY);

INSERT INTO company_price_history (company_id, price, open_price, high_price, low_price, volume, recorded_at)
VALUES (3, 462.75, 458.50, 465.00, 457.00, 42000, SYSTIMESTAMP - INTERVAL '1' DAY);

INSERT INTO company_price_history (company_id, price, open_price, high_price, low_price, volume, recorded_at)
VALUES (4, 218.00, 215.00, 220.00, 214.00, 64000, SYSTIMESTAMP - INTERVAL '30' DAY);

INSERT INTO company_price_history (company_id, price, open_price, high_price, low_price, volume, recorded_at)
VALUES (4, 222.50, 218.50, 224.00, 217.50, 71000, SYSTIMESTAMP - INTERVAL '15' DAY);

INSERT INTO company_price_history (company_id, price, open_price, high_price, low_price, volume, recorded_at)
VALUES (4, 226.80, 223.00, 228.00, 222.00, 69000, SYSTIMESTAMP - INTERVAL '1' DAY);

COMMIT;
