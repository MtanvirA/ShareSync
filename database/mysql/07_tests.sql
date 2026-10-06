-- ============================================================
-- ShareSync - MySQL End-to-End Verification Test Script
-- File: 07_tests.sql
-- Description: Automated test cases verifying schema integrity, triggers, procedures & views
-- ============================================================

USE sharesync;

-- ============================================================
-- TEST 1: VERIFY ALL 14 TABLES EXIST
-- ============================================================
SELECT 
    table_name,
    table_rows,
    create_time
FROM information_schema.tables
WHERE table_schema = 'sharesync' AND table_type = 'BASE TABLE'
ORDER BY table_name;

-- ============================================================
-- TEST 2: ROW COUNT AUDIT ACROSS ALL ENTITIES
-- ============================================================
SELECT 'APP_USERS' AS entity, COUNT(*) AS total_rows FROM app_users
UNION ALL SELECT 'SECTORS', COUNT(*) FROM sectors
UNION ALL SELECT 'COMPANIES', COUNT(*) FROM companies
UNION ALL SELECT 'PORTFOLIOS', COUNT(*) FROM portfolios
UNION ALL SELECT 'WATCHLISTS', COUNT(*) FROM watchlists
UNION ALL SELECT 'WATCHLIST_ITEMS', COUNT(*) FROM watchlist_items
UNION ALL SELECT 'TRANSACTIONS', COUNT(*) FROM transactions
UNION ALL SELECT 'DIVIDENDS', COUNT(*) FROM dividends
UNION ALL SELECT 'TRANSACTION_AUDIT', COUNT(*) FROM transaction_audit
UNION ALL SELECT 'PORTFOLIO_SNAPSHOTS', COUNT(*) FROM portfolio_snapshots
UNION ALL SELECT 'COMPANY_PRICE_HISTORY', COUNT(*) FROM company_price_history
UNION ALL SELECT 'ALERTS', COUNT(*) FROM alerts
UNION ALL SELECT 'NOTIFICATIONS', COUNT(*) FROM notifications
UNION ALL SELECT 'PORTFOLIO_GOALS', COUNT(*) FROM portfolio_goals;

-- ============================================================
-- TEST 3: VIEW VERIFICATION (vw_portfolio_holdings)
-- ============================================================
SELECT 
    portfolio_name,
    ticker_symbol,
    current_quantity,
    current_price,
    weighted_average_buy_price,
    current_market_value,
    unrealized_profit_loss
FROM vw_portfolio_holdings;

-- ============================================================
-- TEST 4: TRIGGER & AUDIT VERIFICATION (INSERT, UPDATE, DELETE)
-- ============================================================

-- Step 4.1: Record initial audit count
SET @initial_audit_count = (SELECT COUNT(*) FROM transaction_audit);

-- Step 4.2: Insert a test transaction -> Verifies INSERT trigger
INSERT INTO transactions (portfolio_id, company_id, transaction_type, quantity, price_per_share, transaction_date)
VALUES (1, 1, 'BUY', 10.0000, 380.00, NOW());

SET @test_tx_id = LAST_INSERT_ID();

-- Step 4.3: Update test transaction -> Verifies UPDATE trigger
UPDATE transactions
SET quantity = 15.0000, price_per_share = 382.00
WHERE transaction_id = @test_tx_id;

-- Step 4.4: Delete test transaction -> Verifies DELETE trigger and ON DELETE SET NULL / permanent audit preservation
DELETE FROM transactions
WHERE transaction_id = @test_tx_id;

-- Step 4.5: Check audit entries generated for the test cycle
SELECT 
    audit_id,
    transaction_id,
    action_type,
    changed_by,
    details,
    action_date
FROM transaction_audit
ORDER BY audit_id DESC
LIMIT 3;

-- ============================================================
-- TEST 5: STORED FUNCTION EXECUTION
-- ============================================================
SELECT 
    fn_get_weighted_avg_price(1, 1) AS gp_weighted_avg_price,
    fn_calculate_unrealized_pl(1, 1) AS gp_unrealized_profit_loss;

-- ============================================================
-- TEST 6: STORED PROCEDURE EXECUTION & RETURN VALUES
-- ============================================================
CALL sp_record_transaction(1, 2, 'BUY', 25.0000, 225.00, 'test_runner', @v_id, @v_status, @v_msg);
SELECT @v_id AS new_tx_id, @v_status AS status, @v_msg AS msg;

-- Cleanup the test row
DELETE FROM transactions WHERE transaction_id = @v_id;

-- ============================================================
-- TEST 7: INVESTMENT INTELLIGENCE HISTORICAL PRICING CHECK
-- ============================================================
SELECT 
    c.ticker_symbol,
    COUNT(h.price_history_id) AS total_history_points,
    MIN(h.price) AS min_price,
    MAX(h.price) AS max_price,
    AVG(h.price) AS avg_price,
    MIN(h.trading_date) AS earliest_date,
    MAX(h.trading_date) AS latest_date
FROM companies c
INNER JOIN company_price_history h ON c.company_id = h.company_id
GROUP BY c.company_id, c.ticker_symbol;
