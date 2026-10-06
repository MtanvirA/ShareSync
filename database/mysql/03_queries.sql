-- ============================================================
-- ShareSync - MySQL Demonstration Queries & Operations
-- File: 03_queries.sql
-- Description: Interactive verification queries demonstrating core RDBMS capabilities
-- ============================================================

USE sharesync;

-- ============================================================
-- 1. BASIC & FILTERED QUERIES (SELECT, WHERE, ORDER BY, LIMIT)
-- ============================================================

-- Active users with their registered role
SELECT user_id, name, email, role, is_active, created_at
FROM app_users
WHERE is_active = 1
ORDER BY created_at DESC;

-- Companies sorted by market capitalization
SELECT company_id, company_name, ticker_symbol, current_price, market_cap
FROM companies
WHERE current_price > 100.00
ORDER BY market_cap DESC
LIMIT 5;

-- ============================================================
-- 2. MULTI-TABLE JOINS (INNER JOIN, LEFT JOIN)
-- ============================================================

-- Portfolios and their owner details
SELECT 
    p.portfolio_id,
    p.portfolio_name,
    u.name AS owner_name,
    u.email AS owner_email,
    p.created_at
FROM portfolios p
INNER JOIN app_users u ON p.user_id = u.user_id;

-- Watchlists with item counts and companies tracked
SELECT 
    w.watchlist_name,
    u.name AS investor_name,
    c.ticker_symbol,
    c.current_price,
    wi.target_price,
    ROUND(((c.current_price - wi.target_price) / wi.target_price) * 100, 2) AS pct_variance_from_target
FROM watchlists w
INNER JOIN app_users u ON w.user_id = u.user_id
INNER JOIN watchlist_items wi ON w.watchlist_id = wi.watchlist_id
INNER JOIN companies c ON wi.company_id = c.company_id
ORDER BY w.watchlist_name, c.ticker_symbol;

-- Sector allocation summary (LEFT JOIN)
SELECT 
    s.sector_name,
    COUNT(c.company_id) AS total_companies,
    COALESCE(AVG(c.current_price), 0) AS avg_share_price,
    COALESCE(SUM(c.market_cap), 0) AS total_sector_mcap
FROM sectors s
LEFT JOIN companies c ON s.sector_id = c.sector_id
GROUP BY s.sector_id, s.sector_name
ORDER BY total_sector_mcap DESC;

-- ============================================================
-- 3. AGGREGATIONS WITH GROUP BY & HAVING
-- ============================================================

-- Portfolios with more than 2 transactions
SELECT 
    p.portfolio_id,
    p.portfolio_name,
    COUNT(t.transaction_id) AS total_transactions,
    SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity * t.price_per_share ELSE 0 END) AS total_invested_capital
FROM portfolios p
INNER JOIN transactions t ON p.portfolio_id = t.portfolio_id
GROUP BY p.portfolio_id, p.portfolio_name
HAVING COUNT(t.transaction_id) >= 2
ORDER BY total_invested_capital DESC;

-- ============================================================
-- 4. SUBQUERIES (Scalar & Correlated)
-- ============================================================

-- Companies whose current price exceeds their sector average
SELECT 
    c.company_name,
    c.ticker_symbol,
    c.current_price,
    s.sector_name
FROM companies c
INNER JOIN sectors s ON c.sector_id = s.sector_id
WHERE c.current_price > (
    SELECT AVG(c2.current_price)
    FROM companies c2
    WHERE c2.sector_id = c.sector_id
);

-- ============================================================
-- 5. COMMON TABLE EXPRESSIONS (CTE) & WINDOW FUNCTIONS
-- ============================================================

-- Rank companies within each sector by current share price using CTE & RANK()
WITH SectorRankings AS (
    SELECT 
        c.ticker_symbol,
        c.company_name,
        s.sector_name,
        c.current_price,
        RANK() OVER (PARTITION BY c.sector_id ORDER BY c.current_price DESC) AS price_rank_in_sector
    FROM companies c
    INNER JOIN sectors s ON c.sector_id = s.sector_id
)
SELECT * 
FROM SectorRankings
WHERE price_rank_in_sector <= 2;

-- Cumulative cash invested progression per portfolio using CTE
WITH TxSummary AS (
    SELECT 
        portfolio_id,
        DATE(transaction_date) AS tx_date,
        SUM(CASE WHEN transaction_type = 'BUY' THEN quantity * price_per_share ELSE -(quantity * price_per_share) END) AS daily_cash_flow
    FROM transactions
    GROUP BY portfolio_id, DATE(transaction_date)
)
SELECT 
    p.portfolio_name,
    ts.tx_date,
    ts.daily_cash_flow,
    SUM(ts.daily_cash_flow) OVER (PARTITION BY ts.portfolio_id ORDER BY ts.tx_date) AS cumulative_invested
FROM TxSummary ts
INNER JOIN portfolios p ON ts.portfolio_id = p.portfolio_id
ORDER BY p.portfolio_name, ts.tx_date;

-- ============================================================
-- 6. VIEW EXECUTION (vw_portfolio_holdings)
-- ============================================================

-- Query live holdings view with mark-to-market valuations
SELECT 
    portfolio_name,
    ticker_symbol,
    current_quantity,
    current_price,
    weighted_average_buy_price,
    current_market_value,
    unrealized_profit_loss,
    ROUND((unrealized_profit_loss / (current_quantity * weighted_average_buy_price)) * 100, 2) AS return_percentage
FROM vw_portfolio_holdings
ORDER BY portfolio_name, unrealized_profit_loss DESC;

-- ============================================================
-- 7. STORED FUNCTION CALLS
-- ============================================================

-- Call fn_get_weighted_avg_price and fn_calculate_unrealized_pl
SELECT 
    p.portfolio_name,
    c.ticker_symbol,
    fn_get_weighted_avg_price(p.portfolio_id, c.company_id) AS calc_weighted_avg_price,
    fn_calculate_unrealized_pl(p.portfolio_id, c.company_id) AS calc_unrealized_pl
FROM portfolios p
CROSS JOIN companies c
WHERE fn_get_weighted_avg_price(p.portfolio_id, c.company_id) > 0;

-- ============================================================
-- 8. STORED PROCEDURE CALLS
-- ============================================================

-- Call sp_record_transaction for a valid BUY order
CALL sp_record_transaction(
    1,                      -- Portfolio 1 (Long Term Growth)
    3,                      -- Company 3 (BATBC)
    'BUY',                  -- Transaction Type
    50.0000,                -- Quantity
    465.00,                 -- Price per share
    'teacher@demo.com',     -- Changed by
    @new_tx_id,             -- OUT transaction ID
    @tx_status,             -- OUT status
    @tx_message             -- OUT message
);

SELECT @new_tx_id AS created_transaction_id, @tx_status AS status, @tx_message AS message;

-- Test oversell protection in sp_record_transaction
CALL sp_record_transaction(
    1,
    3,
    'SELL',
    999999.0000,            -- Excess quantity (oversell)
    465.00,
    'teacher@demo.com',
    @bad_tx_id,
    @bad_status,
    @bad_message
);

SELECT @bad_tx_id AS oversell_tx_id, @bad_status AS oversell_status, @bad_message AS oversell_message;

-- Call sp_generate_portfolio_snapshot
CALL sp_generate_portfolio_snapshot(
    1,
    CURDATE(),
    @snap_val,
    @snap_status,
    @snap_msg
);

SELECT @snap_val AS snapshot_total_value, @snap_status AS status, @snap_msg AS message;

-- ============================================================
-- 9. TRANSACTION INTEGRITY, SAVEPOINTS, COMMIT & ROLLBACK
-- ============================================================

-- Demonstrate atomic transaction with ROLLBACK
START TRANSACTION;

INSERT INTO transactions (portfolio_id, company_id, transaction_type, quantity, price_per_share, transaction_date)
VALUES (1, 1, 'BUY', 99.0000, 380.00, NOW());

SAVEPOINT sv_test_point;

-- Verify row appears in current transaction session
SELECT COUNT(*) AS tx_count_in_uncommitted_tx FROM transactions WHERE quantity = 99.0000;

-- Rollback to clean state
ROLLBACK TO sv_test_point;
ROLLBACK;

-- Verify rollback was successful
SELECT COUNT(*) AS tx_count_after_rollback FROM transactions WHERE quantity = 99.0000;

-- ============================================================
-- 10. AUDIT TRAIL VERIFICATION
-- ============================================================

-- Verify audit rows generated automatically by triggers
SELECT 
    audit_id,
    transaction_id,
    action_type,
    action_date,
    changed_by,
    details
FROM transaction_audit
ORDER BY audit_id DESC
LIMIT 10;