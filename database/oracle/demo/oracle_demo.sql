-- ============================================================================
-- SHARESYNC — ORACLE DATABASE DEMONSTRATION KIT
-- ============================================================================
-- Purpose: Complete, self-contained Oracle SQL demonstration script for
--          ShareSync release candidate verification and live classroom demo.
--
-- Target Database: Oracle Database 23ai / 26ai (Pluggable Database: FREEPDB1)
-- Schema Owner:    SHARESYNC
-- Application:     ShareSync Stock Portfolio & Market Intelligence System
-- ============================================================================

SET DEFINE OFF;
SET SERVEROUTPUT ON SIZE UNLIMITED;
SET LINESIZE 250;
SET PAGESIZE 100;
SET FEEDBACK ON;
SET HEADING ON;
SET TAB OFF;

-- Format common display columns for readable CLI/GUI output
COLUMN table_name FORMAT A24;
COLUMN column_name FORMAT A24;
COLUMN data_type FORMAT A18;
COLUMN nullable FORMAT A8;
COLUMN constraint_name FORMAT A30;
COLUMN constraint_type FORMAT A12;
COLUMN search_condition FORMAT A35;
COLUMN r_constraint_name FORMAT A25;
COLUMN index_name FORMAT A30;
COLUMN uniqueness FORMAT A10;
COLUMN object_name FORMAT A30;
COLUMN object_type FORMAT A16;
COLUMN status FORMAT A10;
COLUMN trigger_name FORMAT A25;
COLUMN triggering_event FORMAT A28;
COLUMN owner_name FORMAT A20;
COLUMN portfolio_name FORMAT A25;
COLUMN company_name FORMAT A26;
COLUMN ticker_symbol FORMAT A10;
COLUMN sector_name FORMAT A22;
COLUMN transaction_type FORMAT A8;
COLUMN action_type FORMAT A10;
COLUMN changed_by FORMAT A16;
COLUMN details FORMAT A45;
COLUMN alert_type FORMAT A22;
COLUMN goal_type FORMAT A24;
COLUMN notification_type FORMAT A20;
COLUMN title FORMAT A28;
COLUMN email FORMAT A25;
COLUMN role FORMAT A10;

PROMPT ============================================================================
PROMPT SHARESYNC LIVE ORACLE DEMONSTRATION KIT LOADED
PROMPT ============================================================================


-- ============================================================================
-- 1. SHOW ALL SHARESYNC TABLES
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 1: ALL SHARESYNC TABLES IN CURRENT SCHEMA
PROMPT 

SELECT 
    table_name,
    tablespace_name,
    num_rows,
    last_analyzed
FROM user_tables
ORDER BY table_name;


-- ============================================================================
-- 2. SHOW TABLE STRUCTURES
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 2: TABLE STRUCTURES (COLUMNS, TYPES, NULLABILITY, DEFAULTS)
PROMPT 

SELECT 
    table_name,
    column_id,
    column_name,
    data_type || 
    CASE 
        WHEN data_type IN ('VARCHAR2', 'CHAR') THEN '(' || data_length || ')'
        WHEN data_type = 'NUMBER' AND data_precision IS NOT NULL THEN '(' || data_precision || ',' || NVL(data_scale, 0) || ')'
        ELSE ''
    END AS data_type,
    nullable,
    data_default
FROM user_tab_columns
ORDER BY table_name, column_id;


-- ============================================================================
-- 3. SHOW PRIMARY / FOREIGN / UNIQUE / CHECK CONSTRAINTS
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 3: DATABASE CONSTRAINTS
PROMPT 

SELECT 
    c.table_name,
    c.constraint_name,
    CASE c.constraint_type
        WHEN 'P' THEN 'PRIMARY KEY'
        WHEN 'R' THEN 'FOREIGN KEY'
        WHEN 'U' THEN 'UNIQUE'
        WHEN 'C' THEN 'CHECK'
        ELSE c.constraint_type
    END AS constraint_type,
    c.r_constraint_name,
    c.delete_rule,
    c.status
FROM user_constraints c
WHERE c.table_name IN (
    'APP_USERS', 'SECTORS', 'COMPANIES', 'COMPANY_PRICE_HISTORY',
    'PORTFOLIOS', 'PORTFOLIO_SNAPSHOTS', 'PORTFOLIO_GOALS',
    'TRANSACTIONS', 'TRANSACTION_AUDIT', 'DIVIDENDS',
    'WATCHLISTS', 'WATCHLIST_ITEMS', 'ALERTS', 'NOTIFICATIONS'
)
ORDER BY c.table_name, c.constraint_type, c.constraint_name;


-- ============================================================================
-- 4. SHOW IMPORTANT INDEXES
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 4: IMPORTANT INDEXES & INDEXED COLUMNS
PROMPT 

SELECT 
    i.table_name,
    i.index_name,
    i.uniqueness,
    c.column_name,
    c.column_position
FROM user_indexes i
JOIN user_ind_columns c ON i.index_name = c.index_name
ORDER BY i.table_name, i.index_name, c.column_position;


-- ============================================================================
-- 5. SHOW IMPORTANT VIEWS
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 5: REGISTERED VIEWS
PROMPT 

SELECT 
    view_name,
    text_length
FROM user_views;

PROMPT 
PROMPT Querying View Definition for VW_PORTFOLIO_HOLDINGS:
PROMPT 

SELECT text FROM user_views WHERE view_name = 'VW_PORTFOLIO_HOLDINGS';


-- ============================================================================
-- 6. SHOW STORED PROCEDURES / FUNCTIONS
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 6: STORED PROCEDURES AND FUNCTIONS
PROMPT 

SELECT 
    object_name,
    object_type,
    status,
    created,
    last_ddl_time
FROM user_objects
WHERE object_type IN ('PROCEDURE', 'FUNCTION')
ORDER BY object_type, object_name;

PROMPT 
PROMPT Procedure/Function Parameter Signatures:
PROMPT 

SELECT 
    object_name,
    argument_name,
    position,
    in_out,
    data_type
FROM user_arguments
WHERE object_name IN (
    'SP_RECORD_TRANSACTION',
    'SP_GENERATE_PORTFOLIO_SNAPSHOT',
    'FN_GET_WEIGHTED_AVG_PRICE',
    'FN_CALCULATE_UNREALIZED_PL'
)
ORDER BY object_name, position;


-- ============================================================================
-- 7. SHOW TRIGGERS
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 7: DATABASE TRIGGERS
PROMPT 

SELECT 
    trigger_name,
    table_name,
    trigger_type,
    triggering_event,
    status
FROM user_triggers;


-- ============================================================================
-- 8. SHOW DEMO USERS
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 8: REGISTERED APPLICATION USERS
PROMPT 

SELECT 
    user_id,
    name,
    email,
    role,
    is_active,
    TO_CHAR(created_at, 'YYYY-MM-DD HH24:MI:SS') AS registered_at
FROM app_users
ORDER BY user_id;


-- ============================================================================
-- 9. SHOW COMPANIES AND SECTORS
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 9: LISTED COMPANIES WITH SECTOR CLASSIFICATION
PROMPT 

SELECT 
    c.company_id,
    c.ticker_symbol,
    c.company_name,
    s.sector_name,
    TO_CHAR(c.current_price, 'FM999,990.00') AS current_price_bdt,
    TO_CHAR(c.market_cap, 'FM999,999,999,990.00') AS market_cap_bdt,
    c.is_active
FROM companies c
JOIN sectors s ON c.sector_id = s.sector_id
ORDER BY s.sector_name, c.ticker_symbol;


-- ============================================================================
-- 10. SHOW PORTFOLIOS
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 10: USER INVESTMENT PORTFOLIOS
PROMPT 

SELECT 
    p.portfolio_id,
    p.portfolio_name,
    u.name AS owner_name,
    u.email AS owner_email,
    p.description,
    TO_CHAR(p.created_at, 'YYYY-MM-DD HH24:MI:SS') AS created_at
FROM portfolios p
JOIN app_users u ON p.user_id = u.user_id
ORDER BY p.portfolio_id;


-- ============================================================================
-- 11. SHOW RECENT TRANSACTIONS
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 11: RECENT TRANSACTIONS LEDGER (LATEST 20)
PROMPT 

SELECT 
    t.transaction_id,
    p.portfolio_name,
    c.ticker_symbol,
    t.transaction_type,
    t.quantity,
    TO_CHAR(t.price_per_share, 'FM999,990.00') AS price_bdt,
    TO_CHAR(t.quantity * t.price_per_share, 'FM999,999,990.00') AS total_value_bdt,
    TO_CHAR(t.transaction_date, 'YYYY-MM-DD HH24:MI:SS') AS tx_date
FROM transactions t
JOIN portfolios p ON t.portfolio_id = p.portfolio_id
JOIN companies c ON t.company_id = c.company_id
ORDER BY t.transaction_id DESC
FETCH FIRST 20 ROWS ONLY;


-- ============================================================================
-- 12. SHOW TRANSACTION AUDIT RECORDS
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 12: TRANSACTION AUDIT TRAIL (LATEST 20)
PROMPT 

SELECT 
    audit_id,
    transaction_id,
    action_type,
    changed_by,
    details,
    TO_CHAR(action_date, 'YYYY-MM-DD HH24:MI:SS') AS logged_at
FROM transaction_audit
ORDER BY audit_id DESC
FETCH FIRST 20 ROWS ONLY;


-- ============================================================================
-- 13. SHOW DIVIDENDS
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 13: COMPANY DIVIDEND ANNOUNCEMENTS
PROMPT 

SELECT 
    d.dividend_id,
    c.ticker_symbol,
    c.company_name,
    TO_CHAR(d.dividend_per_share, 'FM999,990.00') AS dividend_per_share_bdt,
    TO_CHAR(d.declaration_date, 'YYYY-MM-DD') AS declaration_date,
    TO_CHAR(d.payment_date, 'YYYY-MM-DD') AS payment_date
FROM dividends d
JOIN companies c ON d.company_id = c.company_id
ORDER BY d.payment_date DESC;


-- ============================================================================
-- 14. SHOW PRICE HISTORY
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 14: HISTORICAL CLOSING PRICES (LATEST 20 ENTRIES)
PROMPT 

SELECT 
    h.price_history_id,
    c.ticker_symbol,
    c.company_name,
    TO_CHAR(h.price, 'FM999,990.00') AS recorded_price_bdt,
    TO_CHAR(h.recorded_at, 'YYYY-MM-DD HH24:MI:SS') AS recorded_at
FROM company_price_history h
JOIN companies c ON h.company_id = c.company_id
ORDER BY h.recorded_at DESC, c.ticker_symbol
FETCH FIRST 20 ROWS ONLY;


-- ============================================================================
-- 15. SHOW ALERTS
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 15: USER CONFIGURED ALERTS
PROMPT 

SELECT 
    a.alert_id,
    u.name AS user_name,
    a.alert_type,
    TO_CHAR(a.threshold_value, 'FM999,990.00') AS threshold_bdt,
    c.ticker_symbol AS target_company,
    p.portfolio_name AS target_portfolio,
    a.is_active,
    TO_CHAR(a.created_at, 'YYYY-MM-DD') AS created_date,
    TO_CHAR(a.triggered_at, 'YYYY-MM-DD HH24:MI:SS') AS triggered_at
FROM alerts a
JOIN app_users u ON a.user_id = u.user_id
LEFT JOIN companies c ON a.company_id = c.company_id
LEFT JOIN portfolios p ON a.portfolio_id = p.portfolio_id
ORDER BY a.alert_id DESC;


-- ============================================================================
-- 16. SHOW NOTIFICATIONS
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 16: NOTIFICATION LOG (LATEST 20)
PROMPT 

SELECT 
    n.notification_id,
    u.name AS user_name,
    n.notification_type,
    n.title,
    n.message,
    n.is_read,
    TO_CHAR(n.created_at, 'YYYY-MM-DD HH24:MI:SS') AS created_at
FROM notifications n
JOIN app_users u ON n.user_id = u.user_id
ORDER BY n.notification_id DESC
FETCH FIRST 20 ROWS ONLY;


-- ============================================================================
-- 17. SHOW GOALS
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 17: PORTFOLIO FINANCIAL GOALS
PROMPT 

SELECT 
    g.goal_id,
    u.name AS user_name,
    p.portfolio_name,
    g.goal_type,
    g.title,
    TO_CHAR(g.target_value, 'FM999,999,990.00') AS target_value_bdt,
    TO_CHAR(g.target_date, 'YYYY-MM-DD') AS target_date,
    g.is_active
FROM portfolio_goals g
JOIN app_users u ON g.user_id = u.user_id
JOIN portfolios p ON g.portfolio_id = p.portfolio_id
ORDER BY g.goal_id DESC;


-- ============================================================================
-- 18. SHOW PORTFOLIO SNAPSHOTS
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 18: PORTFOLIO HISTORICAL VALUATION SNAPSHOTS (LATEST 20)
PROMPT 

SELECT 
    s.snapshot_id,
    p.portfolio_name,
    u.name AS owner_name,
    TO_CHAR(s.snapshot_date, 'YYYY-MM-DD') AS snapshot_date,
    TO_CHAR(s.total_value, 'FM999,999,990.00') AS total_value_bdt
FROM portfolio_snapshots s
JOIN portfolios p ON s.portfolio_id = p.portfolio_id
JOIN app_users u ON p.user_id = u.user_id
ORDER BY s.snapshot_date DESC, p.portfolio_id
FETCH FIRST 20 ROWS ONLY;


-- ============================================================================
-- 19. SHOW PORTFOLIO HOLDINGS (VIA ORACLE VIEW)
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 19: AUTHORITATIVE HOLDINGS VIA VW_PORTFOLIO_HOLDINGS
PROMPT 

SELECT 
    portfolio_id,
    portfolio_name,
    ticker_symbol,
    company_name,
    current_quantity AS shares_held,
    TO_CHAR(weighted_average_buy_price, 'FM999,990.00') AS avg_buy_price_bdt,
    TO_CHAR(current_price, 'FM999,990.00') AS market_price_bdt,
    TO_CHAR(current_market_value, 'FM999,999,990.00') AS market_value_bdt,
    TO_CHAR(unrealized_profit_loss, 'FM999,999,990.00') AS unrealized_pl_bdt
FROM vw_portfolio_holdings
ORDER BY portfolio_id, ticker_symbol;


-- ============================================================================
-- 20. SHOW PORTFOLIO PERFORMANCE / P&L SUMMARY
-- ============================================================================
PROMPT 
PROMPT >>> SECTION 20: PORTFOLIO OVERALL PERFORMANCE & UNREALIZED GAIN/LOSS
PROMPT 

SELECT 
    portfolio_id,
    portfolio_name,
    COUNT(*) AS distinct_holdings,
    TO_CHAR(SUM(current_quantity * weighted_average_buy_price), 'FM999,999,990.00') AS total_invested_bdt,
    TO_CHAR(SUM(current_market_value), 'FM999,999,990.00') AS current_portfolio_value_bdt,
    TO_CHAR(SUM(unrealized_profit_loss), 'FM999,999,990.00') AS total_unrealized_pl_bdt,
    TO_CHAR(
        ROUND((SUM(unrealized_profit_loss) / NULLIF(SUM(current_quantity * weighted_average_buy_price), 0)) * 100, 2),
        'FM990.00'
    ) || '%' AS overall_return_pct
FROM vw_portfolio_holdings
GROUP BY portfolio_id, portfolio_name
ORDER BY portfolio_id;




-- ============================================================================
-- LIVE FRONTEND → ORACLE DEMONSTRATION
-- ============================================================================
-- Run each query immediately AFTER executing the corresponding action
-- on the ShareSync web user interface to confirm end-to-end database synchronization.
-- ============================================================================

PROMPT ============================================================================
PROMPT LIVE FRONTEND -> ORACLE VERIFICATION QUERIES
PROMPT ============================================================================

-- ----------------------------------------------------------------------------
-- ACTION A: CREATE PORTFOLIO
-- ----------------------------------------------------------------------------
-- FRONTEND ACTION:
--   1. Log into ShareSync frontend (e.g. as tanvir@sharesync.com).
--   2. Click "New Portfolio" button on the Dashboard or Portfolios page.
--   3. Enter Portfolio Name (e.g. 'Tech & Growth') and Description.
--   4. Click "Create Portfolio".
--
-- SQL QUERY:
PROMPT >>> ACTION A QUERY: Check latest created portfolio
SELECT 
    p.portfolio_id,
    p.user_id,
    u.name AS owner_name,
    p.portfolio_name,
    p.description,
    TO_CHAR(p.created_at, 'YYYY-MM-DD HH24:MI:SS') AS created_at
FROM portfolios p
JOIN app_users u ON p.user_id = u.user_id
ORDER BY p.portfolio_id DESC
FETCH FIRST 1 ROWS ONLY;

-- EXPECTED DATABASE CHANGE:
--   A new row appears in PORTFOLIOS with the user's USER_ID and entered PORTFOLIO_NAME.
-- ----------------------------------------------------------------------------


-- ----------------------------------------------------------------------------
-- ACTION B: BUY SHARES
-- ----------------------------------------------------------------------------
-- FRONTEND ACTION:
--   1. Open the portfolio created in Action A (or an existing portfolio).
--   2. Click "Add Transaction".
--   3. Select Company: 'GP' (Grameenphone Ltd.), Type: 'BUY'.
--   4. Enter Quantity: 100, Price: 340.80.
--   5. Click "Execute Transaction".
--
-- SQL QUERY:
PROMPT >>> ACTION B QUERY 1: Inspect newly inserted BUY transaction
SELECT 
    t.transaction_id,
    t.portfolio_id,
    c.ticker_symbol,
    t.transaction_type,
    t.quantity,
    t.price_per_share,
    TO_CHAR(t.transaction_date, 'YYYY-MM-DD HH24:MI:SS') AS transaction_date
FROM transactions t
JOIN companies c ON t.company_id = c.company_id
ORDER BY t.transaction_id DESC
FETCH FIRST 1 ROWS ONLY;

PROMPT >>> ACTION B QUERY 2: Confirm TRG_TRANSACTIONS_AUDIT automatically logged the BUY
SELECT 
    audit_id,
    transaction_id,
    action_type,
    changed_by,
    details,
    TO_CHAR(action_date, 'YYYY-MM-DD HH24:MI:SS') AS action_date
FROM transaction_audit
ORDER BY audit_id DESC
FETCH FIRST 1 ROWS ONLY;

PROMPT >>> ACTION B QUERY 3: Confirm VW_PORTFOLIO_HOLDINGS updated
SELECT 
    portfolio_id,
    ticker_symbol,
    current_quantity,
    weighted_average_buy_price,
    current_market_value,
    unrealized_profit_loss
FROM vw_portfolio_holdings
WHERE portfolio_id = (SELECT MAX(portfolio_id) FROM transactions);

-- EXPECTED DATABASE CHANGE:
--   1. TRANSACTIONS table receives a new row with TRANSACTION_TYPE = 'BUY'.
--   2. TRG_TRANSACTIONS_AUDIT fires, inserting an 'INSERT' record into TRANSACTION_AUDIT.
--   3. VW_PORTFOLIO_HOLDINGS immediately reflects current_quantity = 100 with accurate cost basis.
-- ----------------------------------------------------------------------------


-- ----------------------------------------------------------------------------
-- ACTION C: SELL SHARES
-- ----------------------------------------------------------------------------
-- FRONTEND ACTION:
--   1. Open the portfolio holding from Action B.
--   2. Click "Sell Shares".
--   3. Select Company: 'GP', Type: 'SELL'.
--   4. Enter Quantity: 40 (must be <= available holdings), Price: 350.00.
--   5. Click "Execute Transaction".
--
-- SQL QUERY:
PROMPT >>> ACTION C QUERY 1: Inspect newly inserted SELL transaction
SELECT 
    t.transaction_id,
    t.portfolio_id,
    c.ticker_symbol,
    t.transaction_type,
    t.quantity,
    t.price_per_share,
    TO_CHAR(t.transaction_date, 'YYYY-MM-DD HH24:MI:SS') AS transaction_date
FROM transactions t
JOIN companies c ON t.company_id = c.company_id
ORDER BY t.transaction_id DESC
FETCH FIRST 1 ROWS ONLY;

PROMPT >>> ACTION C QUERY 2: Confirm holdings reduced while maintaining average buy cost basis
SELECT 
    portfolio_id,
    ticker_symbol,
    current_quantity AS remaining_shares,
    weighted_average_buy_price,
    current_market_value,
    unrealized_profit_loss
FROM vw_portfolio_holdings
WHERE portfolio_id = (SELECT portfolio_id FROM transactions WHERE transaction_id = (SELECT MAX(transaction_id) FROM transactions));

-- EXPECTED DATABASE CHANGE:
--   1. TRANSACTIONS table receives a new row with TRANSACTION_TYPE = 'SELL'.
--   2. In VW_PORTFOLIO_HOLDINGS, current_quantity reduces by 40 (100 -> 60).
--   3. weighted_average_buy_price remains 340.80 (weighted average cost is not corrupted by a sell).
-- ----------------------------------------------------------------------------


-- ----------------------------------------------------------------------------
-- ACTION D: ADD COMPANY TO WATCHLIST
-- ----------------------------------------------------------------------------
-- FRONTEND ACTION:
--   1. Navigate to "Watchlist" or "Companies" page.
--   2. Click "Add to Watchlist" on company 'BATBC' (British American Tobacco).
--   3. Enter Target Price: 425.00.
--   4. Click "Save".
--
-- SQL QUERY:
PROMPT >>> ACTION D QUERY: Check Watchlist items
SELECT 
    w.watchlist_id,
    w.watchlist_name,
    u.name AS owner_name,
    c.ticker_symbol,
    c.company_name,
    TO_CHAR(wi.target_price, 'FM999,990.00') AS target_price_bdt,
    TO_CHAR(c.current_price, 'FM999,990.00') AS current_price_bdt,
    TO_CHAR(wi.added_at, 'YYYY-MM-DD HH24:MI:SS') AS added_at
FROM watchlist_items wi
JOIN watchlists w ON wi.watchlist_id = w.watchlist_id
JOIN app_users u ON w.user_id = u.user_id
JOIN companies c ON wi.company_id = c.company_id
ORDER BY wi.added_at DESC
FETCH FIRST 1 ROWS ONLY;

-- EXPECTED DATABASE CHANGE:
--   A new record is inserted into WATCHLIST_ITEMS with (WATCHLIST_ID, COMPANY_ID) and TARGET_PRICE.
-- ----------------------------------------------------------------------------


-- ----------------------------------------------------------------------------
-- ACTION E: CREATE ALERT
-- ----------------------------------------------------------------------------
-- FRONTEND ACTION:
--   1. Navigate to the "Alerts" tab.
--   2. Click "Create Alert".
--   3. Choose Type: 'PRICE_ABOVE', Target Company: 'GP', Threshold: 360.00.
--   4. Click "Set Alert".
--
-- SQL QUERY:
PROMPT >>> ACTION E QUERY: Check newly created Alert
SELECT 
    a.alert_id,
    u.name AS owner_name,
    a.alert_type,
    c.ticker_symbol,
    TO_CHAR(a.threshold_value, 'FM999,990.00') AS threshold_bdt,
    a.is_active,
    TO_CHAR(a.created_at, 'YYYY-MM-DD HH24:MI:SS') AS created_at
FROM alerts a
JOIN app_users u ON a.user_id = u.user_id
LEFT JOIN companies c ON a.company_id = c.company_id
ORDER BY a.alert_id DESC
FETCH FIRST 1 ROWS ONLY;

-- EXPECTED DATABASE CHANGE:
--   A new row appears in ALERTS with IS_ACTIVE = 1 and THRESHOLD_VALUE = 360.00.
-- ----------------------------------------------------------------------------


-- ----------------------------------------------------------------------------
-- ACTION F: CREATE PORTFOLIO GOAL
-- ----------------------------------------------------------------------------
-- FRONTEND ACTION:
--   1. Navigate to the "Goals" page.
--   2. Click "Add Financial Goal".
--   3. Select Portfolio, Goal Type: 'TARGET_PORTFOLIO_VALUE'.
--   4. Title: 'Year-End 2026 Milestone', Target Value: 100,000.
--   5. Click "Save Goal".
--
-- SQL QUERY:
PROMPT >>> ACTION F QUERY: Check newly created Goal
SELECT 
    g.goal_id,
    u.name AS owner_name,
    p.portfolio_name,
    g.goal_type,
    g.title,
    TO_CHAR(g.target_value, 'FM999,999,990.00') AS target_value_bdt,
    g.is_active,
    TO_CHAR(g.created_at, 'YYYY-MM-DD HH24:MI:SS') AS created_at
FROM portfolio_goals g
JOIN app_users u ON g.user_id = u.user_id
JOIN portfolios p ON g.portfolio_id = p.portfolio_id
ORDER BY g.goal_id DESC
FETCH FIRST 1 ROWS ONLY;

-- EXPECTED DATABASE CHANGE:
--   A new row is stored in PORTFOLIO_GOALS linked to the selected portfolio.
-- ----------------------------------------------------------------------------


-- ----------------------------------------------------------------------------
-- ACTION G: TRIGGER / PRODUCE NOTIFICATION
-- ----------------------------------------------------------------------------
-- FRONTEND ACTION:
--   1. An alert condition triggers or a market notice is broadcast to user.
--   2. Frontend notification bell shows an unread badge.
--
-- SQL QUERY:
PROMPT >>> ACTION G QUERY: Inspect latest notification in stream
SELECT 
    n.notification_id,
    u.name AS recipient,
    n.notification_type,
    n.title,
    n.message,
    n.is_read,
    TO_CHAR(n.created_at, 'YYYY-MM-DD HH24:MI:SS') AS created_at
FROM notifications n
JOIN app_users u ON n.user_id = u.user_id
ORDER BY n.notification_id DESC
FETCH FIRST 1 ROWS ONLY;

-- EXPECTED DATABASE CHANGE:
--   A new record exists in NOTIFICATIONS with IS_READ = 0 (false).
-- ----------------------------------------------------------------------------


-- ----------------------------------------------------------------------------
-- ACTION H: VIEW REPORT
-- ----------------------------------------------------------------------------
-- FRONTEND ACTION:
--   1. Open the "Reports" tab.
--   2. View Portfolio Holdings Report or Performance Report.
--
-- SQL QUERY:
PROMPT >>> ACTION H QUERY: Authoritative query backing the Reports page
SELECT 
    h.portfolio_name,
    h.ticker_symbol,
    h.company_name,
    h.current_quantity,
    TO_CHAR(h.weighted_average_buy_price, 'FM999,990.00') AS avg_cost_bdt,
    TO_CHAR(h.current_price, 'FM999,990.00') AS market_price_bdt,
    TO_CHAR(h.current_market_value, 'FM999,999,990.00') AS market_value_bdt,
    TO_CHAR(h.unrealized_profit_loss, 'FM999,999,990.00') AS unrealized_pl_bdt,
    TO_CHAR(
        ROUND((h.unrealized_profit_loss / NULLIF(h.current_quantity * h.weighted_average_buy_price, 0)) * 100, 2),
        'FM990.00'
    ) || '%' AS return_pct
FROM vw_portfolio_holdings h
ORDER BY h.portfolio_name, h.current_market_value DESC;

-- EXPECTED DATABASE CHANGE:
--   Read-only query: Values displayed on the Report page match this query exactly to the paisa.
-- ----------------------------------------------------------------------------




-- ============================================================================
-- SAFE CONSTRAINT & BUSINESS RULE DEMONSTRATIONS (NEGATIVE TESTING)
-- ============================================================================
-- The following tests demonstrate that database constraints and business
-- rules reject invalid data.
-- SAFETY GUARANTEE:
--   Every test is isolated within a sub-block using SAVEPOINTS and unconditional
--   ROLLBACK. No data is modified, corrupted, or left behind.
-- ============================================================================

PROMPT ============================================================================
PROMPT RUNNING SAFE CONSTRAINT & BUSINESS RULE NEGATIVE TESTS
PROMPT ============================================================================

DECLARE
    v_test_port_id NUMBER;
    v_test_comp_id NUMBER;
    v_dummy_id     NUMBER;
    v_status       VARCHAR2(50);
    v_msg          VARCHAR2(500);
BEGIN
    DBMS_OUTPUT.PUT_LINE('----------------------------------------------------------------------------');
    DBMS_OUTPUT.PUT_LINE('STARTING CONTROLLED DATABASE CONSTRAINT DEMONSTRATION');
    DBMS_OUTPUT.PUT_LINE('----------------------------------------------------------------------------');

    -- Setup isolated test context
    SAVEPOINT sp_demo_tests;

    SELECT MIN(portfolio_id) INTO v_test_port_id FROM portfolios;
    SELECT MIN(company_id) INTO v_test_comp_id FROM companies;

    -- -------------------------------------------------------------------------
    -- TEST 1: INVALID QUANTITY (CK_TRANSACTIONS_QUANTITY)
    -- -------------------------------------------------------------------------
    BEGIN
        DBMS_OUTPUT.PUT('TEST 1: Insert Quantity = 0: ');
        INSERT INTO transactions (portfolio_id, company_id, transaction_type, quantity, price_per_share, transaction_date)
        VALUES (v_test_port_id, v_test_comp_id, 'BUY', 0, 100, SYSDATE);
        DBMS_OUTPUT.PUT_LINE('FAILED - Constraint did not prevent zero quantity!');
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE = -2290 THEN
                DBMS_OUTPUT.PUT_LINE('PASSED [ORA-02290: check constraint CK_TRANSACTIONS_QUANTITY violated]');
            ELSE
                DBMS_OUTPUT.PUT_LINE('CAUGHT ERROR: ' || SQLERRM);
            END IF;
    END;

    -- -------------------------------------------------------------------------
    -- TEST 2: INVALID PRICE (CK_TRANSACTIONS_PRICE)
    -- -------------------------------------------------------------------------
    BEGIN
        DBMS_OUTPUT.PUT('TEST 2: Insert Price <= 0 (Price = -50): ');
        INSERT INTO transactions (portfolio_id, company_id, transaction_type, quantity, price_per_share, transaction_date)
        VALUES (v_test_port_id, v_test_comp_id, 'BUY', 10, -50, SYSDATE);
        DBMS_OUTPUT.PUT_LINE('FAILED - Constraint did not prevent negative price!');
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE = -2290 THEN
                DBMS_OUTPUT.PUT_LINE('PASSED [ORA-02290: check constraint CK_TRANSACTIONS_PRICE violated]');
            ELSE
                DBMS_OUTPUT.PUT_LINE('CAUGHT ERROR: ' || SQLERRM);
            END IF;
    END;

    -- -------------------------------------------------------------------------
    -- TEST 3: INVALID FOREIGN KEY (FK_TRANSACTIONS_COMPANY)
    -- -------------------------------------------------------------------------
    BEGIN
        DBMS_OUTPUT.PUT('TEST 3: Insert with Non-Existent Company ID (999999): ');
        INSERT INTO transactions (portfolio_id, company_id, transaction_type, quantity, price_per_share, transaction_date)
        VALUES (v_test_port_id, 999999, 'BUY', 10, 100, SYSDATE);
        DBMS_OUTPUT.PUT_LINE('FAILED - Foreign key did not reject invalid company!');
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE = -2291 THEN
                DBMS_OUTPUT.PUT_LINE('PASSED [ORA-02291: integrity constraint FK_TRANSACTIONS_COMPANY violated - parent key not found]');
            ELSE
                DBMS_OUTPUT.PUT_LINE('CAUGHT ERROR: ' || SQLERRM);
            END IF;
    END;

    -- -------------------------------------------------------------------------
    -- TEST 4: DUPLICATE UNIQUE VALUE (UQ_COMPANIES_TICKER)
    -- -------------------------------------------------------------------------
    BEGIN
        DBMS_OUTPUT.PUT('TEST 4: Insert Duplicate Ticker Symbol (GP): ');
        INSERT INTO companies (company_name, ticker_symbol, sector_id, current_price)
        VALUES ('Duplicate Grameenphone Test', 'GP', 1, 350.00);
        DBMS_OUTPUT.PUT_LINE('FAILED - Unique constraint did not prevent duplicate ticker!');
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE = -1 THEN
                DBMS_OUTPUT.PUT_LINE('PASSED [ORA-00001: unique constraint UQ_COMPANIES_TICKER violated]');
            ELSE
                DBMS_OUTPUT.PUT_LINE('CAUGHT ERROR: ' || SQLERRM);
            END IF;
    END;

    -- -------------------------------------------------------------------------
    -- TEST 5: OVERSELLING REJECTION VIA SP_RECORD_TRANSACTION
    -- -------------------------------------------------------------------------
    BEGIN
        DBMS_OUTPUT.PUT('TEST 5: Attempt to SELL 9,999,999 shares (exceeding holdings): ');
        sp_record_transaction(
            p_portfolio_id     => v_test_port_id,
            p_company_id       => v_test_comp_id,
            p_transaction_type => 'SELL',
            p_quantity         => 9999999,
            p_price_per_share  => 300,
            p_changed_by       => 'DEMO_USER',
            p_transaction_id   => v_dummy_id,
            p_status           => v_status,
            p_message          => v_msg
        );
        IF v_status = 'OVERSELL_REJECTED' AND v_dummy_id IS NULL THEN
            DBMS_OUTPUT.PUT_LINE('PASSED [SP_RECORD_TRANSACTION returned OVERSELL_REJECTED: ' || v_msg || ']');
        ELSE
            DBMS_OUTPUT.PUT_LINE('FAILED - Procedure did not reject oversell. Status: ' || v_status);
        END IF;
    EXCEPTION
        WHEN OTHERS THEN
            DBMS_OUTPUT.PUT_LINE('ERROR: ' || SQLERRM);
    END;

    -- Clean rollback of test data
    ROLLBACK TO sp_demo_tests;
    DBMS_OUTPUT.PUT_LINE('----------------------------------------------------------------------------');
    DBMS_OUTPUT.PUT_LINE('NEGATIVE TESTS COMPLETE — ALL TEST TRANSACTIONS ROLLED BACK SAFELY.');
    DBMS_OUTPUT.PUT_LINE('----------------------------------------------------------------------------');
END;
/

PROMPT 
PROMPT ============================================================================
PROMPT SHARESYNC ORACLE DEMONSTRATION COMPLETE
PROMPT ============================================================================
