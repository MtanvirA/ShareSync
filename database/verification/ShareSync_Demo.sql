-- ==========================================
-- SHARESYNC - FINAL DEMO VERIFICATION SCRIPT
-- ==========================================
-- This script is READ-ONLY and safe for live demonstration.
-- It displays the complete state of the Oracle database.

-- 1. List Tables
SELECT table_name FROM user_tables ORDER BY table_name;

-- 2. Show Table Structures (Requires SQL*Plus DESCRIBE, or querying USER_TAB_COLUMNS)
SELECT table_name, column_name, data_type, data_length 
FROM user_tab_columns 
WHERE table_name IN ('COMPANIES', 'USERS', 'PORTFOLIOS', 'TRANSACTIONS', 'COMPANY_PRICE_HISTORY')
ORDER BY table_name, column_id;

-- 3. Show Primary Keys
SELECT constraint_name, table_name, status 
FROM user_constraints 
WHERE constraint_type = 'P';

-- 4. Show Foreign Keys
SELECT constraint_name, table_name, r_constraint_name 
FROM user_constraints 
WHERE constraint_type = 'R';

-- 5. Show Indexes
SELECT index_name, table_name, uniqueness 
FROM user_indexes;

-- 6. Show Views
SELECT view_name FROM user_views;

-- 7. Show Stored Procedures/Functions
SELECT object_name, object_type, status 
FROM user_objects 
WHERE object_type IN ('PROCEDURE', 'FUNCTION', 'PACKAGE');

-- 8. Show Triggers
SELECT trigger_name, trigger_type, triggering_event, table_name, status 
FROM user_triggers;

-- 9. Show Sample Companies
SELECT company_id, ticker_symbol, company_name, current_price 
FROM companies 
FETCH FIRST 5 ROWS ONLY;

-- 10. Show Sample Portfolios
SELECT portfolio_id, user_id, portfolio_name, total_value, cash_balance 
FROM portfolios 
FETCH FIRST 5 ROWS ONLY;

-- 11. Show Sample Transactions
SELECT transaction_id, portfolio_id, company_id, transaction_type, quantity, price_per_share, transaction_date 
FROM transactions 
ORDER BY transaction_date DESC 
FETCH FIRST 5 ROWS ONLY;

-- 12. Show Holdings Calculations
SELECT portfolio_id, company_id, total_shares, average_buy_price 
FROM portfolio_holdings 
FETCH FIRST 5 ROWS ONLY;

-- 13. Show Dividends
SELECT dividend_id, company_id, dividend_per_share, declaration_date 
FROM dividends 
FETCH FIRST 5 ROWS ONLY;

-- 14. Show Watchlists
SELECT w.watchlist_id, w.user_id, wi.company_id, wi.added_at 
FROM watchlists w 
JOIN watchlist_items wi ON w.watchlist_id = wi.watchlist_id 
FETCH FIRST 5 ROWS ONLY;

-- 15. Show Historical Market Data (Demo Companies)
SELECT company_id, trading_date, price, volume, source 
FROM company_price_history 
WHERE company_id IN (2, 3, 25) 
ORDER BY trading_date DESC 
FETCH FIRST 10 ROWS ONLY;

-- 16. Show Audit Records
SELECT audit_id, table_name, action, changed_by, changed_at 
FROM audit_logs 
ORDER BY changed_at DESC 
FETCH FIRST 10 ROWS ONLY;

-- 17. Relevant EXPLAIN PLAN (Investment Intelligence Query)
EXPLAIN PLAN FOR 
SELECT trading_date, price 
FROM company_price_history 
WHERE company_id = 3 AND trading_date >= SYSDATE - 1825;

SELECT * FROM TABLE(DBMS_XPLAN.DISPLAY);
