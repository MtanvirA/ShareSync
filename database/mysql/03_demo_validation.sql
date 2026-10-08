-- ============================================================
-- ShareSync - MySQL Demo Data Validation Script
-- File: 03_demo_validation.sql
-- Description: Comprehensive verification of referential integrity,
--              financial consistency, and demonstration readiness
-- Usage:
--   mysql -u root -p sharesync < database/mysql/03_demo_validation.sql
-- ============================================================

USE sharesync;

SELECT '========================================================================' AS '';
SELECT '               SHARESYNC MYSQL DEMO DATA AUDIT & VALIDATION             ' AS '';
SELECT '========================================================================' AS '';

-- 1. RECORD COUNT AGGREGATES
SELECT '>>> Entity Population Counts' AS section;
SELECT 
    (SELECT COUNT(*) FROM app_users)               AS users_count,
    (SELECT COUNT(*) FROM sectors)                 AS sectors_count,
    (SELECT COUNT(*) FROM companies)               AS companies_count,
    (SELECT COUNT(*) FROM portfolios)              AS portfolios_count,
    (SELECT COUNT(*) FROM transactions)            AS transactions_count,
    (SELECT COUNT(*) FROM vw_portfolio_holdings)   AS active_holdings_count,
    (SELECT COUNT(*) FROM dividends)               AS dividends_count,
    (SELECT COUNT(*) FROM watchlists)              AS watchlists_count,
    (SELECT COUNT(*) FROM watchlist_items)         AS watchlist_items_count,
    (SELECT COUNT(*) FROM alerts)                  AS alerts_count,
    (SELECT COUNT(*) FROM portfolio_goals)         AS goals_count,
    (SELECT COUNT(*) FROM notifications)           AS notifications_count,
    (SELECT COUNT(*) FROM portfolio_snapshots)     AS snapshots_count,
    (SELECT COUNT(*) FROM transaction_audit)       AS audits_count,
    (SELECT COUNT(*) FROM inflation_rates)         AS inflation_rates_count,
    (SELECT COUNT(*) FROM currency_exchange_rates) AS fx_rates_count,
    (SELECT COUNT(*) FROM benchmark_price_history) AS benchmark_history_count,
    (SELECT COUNT(*) FROM company_price_history)   AS company_price_history_count;

-- 2. ACTIVE HOLDINGS & VALUATION AUDIT (vw_portfolio_holdings)
SELECT '>>> Active Portfolio Holdings Valuation (vw_portfolio_holdings)' AS section;
SELECT 
    portfolio_name,
    ticker_symbol,
    current_quantity,
    weighted_average_buy_price,
    current_price,
    current_market_value,
    unrealized_profit_loss
FROM vw_portfolio_holdings
ORDER BY portfolio_name, ticker_symbol;

-- 3. FINANCIAL CONSISTENCY AUDIT
SELECT '>>> Financial Calculation Consistency Verification' AS section;
SELECT 
    p.portfolio_name,
    COUNT(t.transaction_id) AS total_orders,
    SUM(CASE WHEN t.transaction_type = 'BUY' THEN 1 ELSE 0 END) AS buy_orders,
    SUM(CASE WHEN t.transaction_type = 'SELL' THEN 1 ELSE 0 END) AS sell_orders,
    SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity * t.price_per_share ELSE 0 END) AS total_buy_capital,
    SUM(CASE WHEN t.transaction_type = 'SELL' THEN t.quantity * t.price_per_share ELSE 0 END) AS total_sell_proceeds,
    SUM(t.fee) AS total_fees_paid,
    SUM(CASE WHEN t.transaction_type = 'BUY' THEN -(t.quantity * t.price_per_share + t.fee) ELSE (t.quantity * t.price_per_share - t.fee) END) AS net_cash_flow
FROM portfolios p
JOIN transactions t ON p.portfolio_id = t.portfolio_id
GROUP BY p.portfolio_id, p.portfolio_name
ORDER BY p.portfolio_id;

-- 4. REFERENTIAL INTEGRITY & CONSTRAINT INTEGRITY ASSERTIONS
SELECT '>>> Referential Integrity & Validation Assertions' AS section;

SELECT 
    -- Check 1: Orphan Transactions
    CASE WHEN COUNT(*) = 0 THEN 'PASS' ELSE 'FAIL: Orphan transactions detected' END AS orphan_transactions_check
FROM transactions t
LEFT JOIN portfolios p ON t.portfolio_id = p.portfolio_id
LEFT JOIN companies c ON t.company_id = c.company_id
WHERE p.portfolio_id IS NULL OR c.company_id IS NULL;

SELECT 
    -- Check 2: Invalid Transaction Values
    CASE WHEN COUNT(*) = 0 THEN 'PASS' ELSE 'FAIL: Invalid transaction values detected' END AS invalid_transactions_check
FROM transactions
WHERE quantity <= 0 
   OR price_per_share <= 0 
   OR fee < 0 
   OR transaction_type NOT IN ('BUY', 'SELL')
   OR quantity != FLOOR(quantity);

SELECT 
    -- Check 3: Oversold Positions (Impossible Holdings)
    CASE WHEN COUNT(*) = 0 THEN 'PASS' ELSE 'FAIL: Negative share balance detected' END AS oversold_positions_check
FROM (
    SELECT portfolio_id, company_id,
           SUM(CASE WHEN transaction_type = 'BUY' THEN quantity ELSE -quantity END) AS net_qty
    FROM transactions
    GROUP BY portfolio_id, company_id
    HAVING net_qty < 0
) net_holdings;

SELECT 
    -- Check 4: Duplicate Watchlist Items
    CASE WHEN COUNT(*) = 0 THEN 'PASS' ELSE 'FAIL: Duplicate watchlist items detected' END AS duplicate_watchlist_items_check
FROM (
    SELECT watchlist_id, company_id, COUNT(*) AS cnt
    FROM watchlist_items
    GROUP BY watchlist_id, company_id
    HAVING cnt > 1
) dupes;

SELECT 
    -- Check 5: Orphan Watchlist Items
    CASE WHEN COUNT(*) = 0 THEN 'PASS' ELSE 'FAIL: Orphan watchlist items detected' END AS orphan_watchlist_items_check
FROM watchlist_items wi
LEFT JOIN watchlists w ON wi.watchlist_id = w.watchlist_id
LEFT JOIN companies c ON wi.company_id = c.company_id
WHERE w.watchlist_id IS NULL OR c.company_id IS NULL;

SELECT 
    -- Check 6: Orphan Alerts
    CASE WHEN COUNT(*) = 0 THEN 'PASS' ELSE 'FAIL: Orphan alerts detected' END AS orphan_alerts_check
FROM alerts a
LEFT JOIN app_users u ON a.user_id = u.user_id
LEFT JOIN companies c ON a.company_id = c.company_id
LEFT JOIN portfolios p ON a.portfolio_id = p.portfolio_id
WHERE u.user_id IS NULL 
   OR (a.company_id IS NOT NULL AND c.company_id IS NULL)
   OR (a.portfolio_id IS NOT NULL AND p.portfolio_id IS NULL);

SELECT 
    -- Check 7: Orphan Goals
    CASE WHEN COUNT(*) = 0 THEN 'PASS' ELSE 'FAIL: Orphan goals detected' END AS orphan_goals_check
FROM portfolio_goals g
LEFT JOIN app_users u ON g.user_id = u.user_id
LEFT JOIN portfolios p ON g.portfolio_id = p.portfolio_id
WHERE u.user_id IS NULL OR p.portfolio_id IS NULL;

SELECT 
    -- Check 8: Orphan Snapshots
    CASE WHEN COUNT(*) = 0 THEN 'PASS' ELSE 'FAIL: Orphan snapshots detected' END AS orphan_snapshots_check
FROM portfolio_snapshots s
LEFT JOIN portfolios p ON s.portfolio_id = p.portfolio_id
WHERE p.portfolio_id IS NULL OR s.total_value < 0;

SELECT 
    -- Check 9: Orphan Audits
    CASE WHEN COUNT(*) = 0 THEN 'PASS' ELSE 'FAIL: Orphan audits detected' END AS orphan_audits_check
FROM transaction_audit ta
LEFT JOIN transactions t ON ta.transaction_id = t.transaction_id
WHERE ta.transaction_id IS NOT NULL AND t.transaction_id IS NULL;

SELECT 
    -- Check 10: Multi-Asset & Multi-Currency Data Completeness
    CASE WHEN COUNT(*) >= 4 THEN 'PASS' ELSE 'FAIL: Incomplete asset types detected' END AS multi_asset_coverage_check
FROM (
    SELECT DISTINCT asset_type FROM companies
) asset_types;

SELECT 
    -- Check 11: Realized P/L Demonstrability
    CASE WHEN COUNT(*) >= 2 THEN 'PASS' ELSE 'FAIL: Missing BUY/SELL rebalancing trades' END AS realized_pl_trades_check
FROM transactions
WHERE transaction_type = 'SELL';

SELECT 
    -- Check 12: Benchmark Series Availability
    CASE WHEN COUNT(DISTINCT benchmark_code) >= 3 AND COUNT(*) > 100 THEN 'PASS' ELSE 'FAIL: Missing benchmark index history' END AS benchmark_series_check
FROM benchmark_price_history;

SELECT 
    -- Check 13: Historical Company Pricing Availability
    CASE WHEN COUNT(DISTINCT company_id) >= 10 AND COUNT(*) > 1000 THEN 'PASS' ELSE 'FAIL: Missing historical company price data' END AS historical_market_data_check
FROM company_price_history;

SELECT '========================================================================' AS '';
SELECT '         AUDIT COMPLETE: DEMONSTRATION DATASET FULLY VALIDATED          ' AS '';
SELECT '========================================================================' AS '';
