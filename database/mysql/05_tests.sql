-- ============================================================
-- ShareSync - MySQL Database Tests
-- ============================================================

USE sharesync;


-- ============================================================
-- TEST 1: Check table row counts
-- ============================================================

SELECT 'app_users' AS table_name, COUNT(*) AS row_count
FROM app_users

UNION ALL

SELECT 'sectors', COUNT(*)
FROM sectors

UNION ALL

SELECT 'companies', COUNT(*)
FROM companies

UNION ALL

SELECT 'portfolios', COUNT(*)
FROM portfolios

UNION ALL

SELECT 'watchlists', COUNT(*)
FROM watchlists

UNION ALL

SELECT 'watchlist_items', COUNT(*)
FROM watchlist_items

UNION ALL

SELECT 'transactions', COUNT(*)
FROM transactions

UNION ALL

SELECT 'dividends', COUNT(*)
FROM dividends

UNION ALL

SELECT 'transaction_audit', COUNT(*)
FROM transaction_audit

UNION ALL

SELECT 'portfolio_snapshots', COUNT(*)
FROM portfolio_snapshots;


-- ============================================================
-- TEST 2: Verify foreign-key relationships
-- ============================================================

SELECT
    p.portfolio_name,
    c.company_name,
    t.transaction_type,
    t.quantity,
    t.price_per_share
FROM transactions t
JOIN portfolios p
    ON t.portfolio_id = p.portfolio_id
JOIN companies c
    ON t.company_id = c.company_id
ORDER BY t.transaction_id;


-- ============================================================
-- TEST 3: Verify current holdings
-- ============================================================

SELECT
    p.portfolio_name,
    c.ticker_symbol,

    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity
            ELSE -t.quantity
        END
    ) AS current_quantity

FROM transactions t

JOIN portfolios p
    ON t.portfolio_id = p.portfolio_id

JOIN companies c
    ON t.company_id = c.company_id

GROUP BY
    p.portfolio_id,
    p.portfolio_name,
    c.company_id,
    c.ticker_symbol

HAVING current_quantity > 0;


-- ============================================================
-- TEST 4: Verify audit trigger
-- ============================================================

SELECT
    transaction_id,
    action_type,
    details
FROM transaction_audit
ORDER BY audit_id DESC;


-- ============================================================
-- TEST 5: Verify portfolio snapshots
-- ============================================================

SELECT
    p.portfolio_name,
    ps.snapshot_date,
    ps.total_value
FROM portfolio_snapshots ps

JOIN portfolios p
    ON ps.portfolio_id = p.portfolio_id

ORDER BY
    p.portfolio_name,
    ps.snapshot_date;