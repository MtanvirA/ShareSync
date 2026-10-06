-- ============================================================================
-- ShareSync - Oracle Database Demonstration & Verification Script
-- ============================================================================
-- PURPOSE
--   A single SQL*Plus / Oracle script for demonstrating ShareSync to the
--   Oracle Database teacher.
--
-- HOW TO USE
--   1. Open Oracle SQL*Plus / SQLcl / SQL Developer.
--   2. Connect to the same Oracle schema used by ShareSync.
--   3. Run the READ-ONLY sections freely.
--   4. For sections marked [AFTER SOFTWARE OPERATION], perform that action
--      in the ShareSync application FIRST, then run the query underneath.
--   5. For sections marked [TEST], read the comments carefully. These are
--      controlled database tests and some intentionally attempt invalid data.
--
-- IMPORTANT
--   The frontend normally uses ASP.NET Core + Entity Framework Core for CRUD.
--   The Oracle stored procedure/function/trigger objects are still part of
--   the database design and can be demonstrated directly from Oracle.
--
--   Replace example IDs such as 1 with IDs returned by the discovery queries
--   if your database contains different data.
--
-- QUICK DEMO ORDER
--   A.  Session setup / database inventory
--   B.  Login / registration verification
--   C.  Portfolio verification
--   D.  Transaction -> Oracle -> View -> Audit Trigger
--   E.  Watchlist
--   F.  Company / price history
--   G.  Dividends
--   H.  Alerts / notifications
--   I.  Goals
--   J.  Reports / analytics
--   K.  Functions
--   L.  Stored procedures
--   M.  Constraints
--   N.  Indexes / metadata
-- ============================================================================


-- ============================================================================
-- 0. SESSION SETUP
-- ============================================================================
-- [BEFORE DEMO]
-- Run this once when you open the Oracle session.
-- It makes the output readable and enables DBMS_OUTPUT for PL/SQL tests.

SET SERVEROUTPUT ON SIZE UNLIMITED
SET LINESIZE 220
SET PAGESIZE 100
SET FEEDBACK ON
SET VERIFY OFF
SET DEFINE ON


-- ============================================================================
-- 1. WHO AM I CONNECTED AS?
-- ============================================================================
-- [BEFORE DEMO]
-- No software operation is required.
-- Use this first to prove which Oracle user/schema you are connected to.

SELECT USER AS oracle_user,
       SYS_CONTEXT('USERENV', 'DB_NAME') AS database_name,
       SYS_CONTEXT('USERENV', 'SERVICE_NAME') AS service_name
FROM dual;


-- ============================================================================
-- 2. DATABASE OBJECT INVENTORY
-- ============================================================================
-- [BEFORE DEMO]
-- No software operation is required.
-- Use this when the teacher asks:
--   "What Oracle objects did you create?"
--
-- Expected categories include:
--   TABLE, VIEW, INDEX, PROCEDURE, FUNCTION, TRIGGER, SEQUENCE, etc.

SELECT object_type,
       COUNT(*) AS object_count
FROM user_objects
GROUP BY object_type
ORDER BY object_type;


-- ============================================================================
-- 3. LIST ALL SHARESYNC TABLES
-- ============================================================================
-- [BEFORE DEMO]
-- No software operation is required.
-- This proves which tables exist in the current Oracle schema.

SELECT table_name
FROM user_tables
WHERE table_name IN (
    'APP_USERS',
    'SECTORS',
    'COMPANIES',
    'PORTFOLIOS',
    'WATCHLISTS',
    'WATCHLIST_ITEMS',
    'TRANSACTIONS',
    'DIVIDENDS',
    'TRANSACTION_AUDIT',
    'PORTFOLIO_SNAPSHOTS',
    'COMPANY_PRICE_HISTORY',
    'ALERTS',
    'NOTIFICATIONS',
    'PORTFOLIO_GOALS'
)
ORDER BY table_name;


-- ============================================================================
-- 4. TABLE ROW COUNTS
-- ============================================================================
-- [BEFORE DEMO]
-- No software operation is required.
-- Useful as a quick database health check.

SELECT 'APP_USERS' AS table_name, COUNT(*) AS row_count FROM app_users
UNION ALL
SELECT 'SECTORS', COUNT(*) FROM sectors
UNION ALL
SELECT 'COMPANIES', COUNT(*) FROM companies
UNION ALL
SELECT 'PORTFOLIOS', COUNT(*) FROM portfolios
UNION ALL
SELECT 'WATCHLISTS', COUNT(*) FROM watchlists
UNION ALL
SELECT 'WATCHLIST_ITEMS', COUNT(*) FROM watchlist_items
UNION ALL
SELECT 'TRANSACTIONS', COUNT(*) FROM transactions
UNION ALL
SELECT 'DIVIDENDS', COUNT(*) FROM dividends
UNION ALL
SELECT 'TRANSACTION_AUDIT', COUNT(*) FROM transaction_audit
UNION ALL
SELECT 'PORTFOLIO_SNAPSHOTS', COUNT(*) FROM portfolio_snapshots
UNION ALL
SELECT 'COMPANY_PRICE_HISTORY', COUNT(*) FROM company_price_history
UNION ALL
SELECT 'ALERTS', COUNT(*) FROM alerts
UNION ALL
SELECT 'NOTIFICATIONS', COUNT(*) FROM notifications
UNION ALL
SELECT 'PORTFOLIO_GOALS', COUNT(*) FROM portfolio_goals
ORDER BY table_name;


-- ============================================================================
-- 5. DESCRIBE THE MOST IMPORTANT TABLES
-- ============================================================================
-- [BEFORE DEMO]
-- No software operation is required.
-- Use DESC when the teacher asks about columns, data types, NULL rules, etc.

DESC app_users;
DESC sectors;
DESC companies;
DESC portfolios;
DESC transactions;
DESC transaction_audit;


-- ============================================================================
-- 6. SHOW PRIMARY KEYS, FOREIGN KEYS AND CHECK/UNIQUE CONSTRAINTS
-- ============================================================================
-- [BEFORE DEMO]
-- No software operation is required.
-- This is one of the best queries for an Oracle Database viva.

SELECT c.table_name,
       c.constraint_name,
       c.constraint_type,
       c.status,
       cc.column_name,
       cc.position
FROM user_constraints c
LEFT JOIN user_cons_columns cc
       ON c.constraint_name = cc.constraint_name
WHERE c.table_name IN (
    'APP_USERS',
    'SECTORS',
    'COMPANIES',
    'PORTFOLIOS',
    'WATCHLISTS',
    'WATCHLIST_ITEMS',
    'TRANSACTIONS',
    'DIVIDENDS',
    'TRANSACTION_AUDIT',
    'PORTFOLIO_SNAPSHOTS',
    'COMPANY_PRICE_HISTORY',
    'ALERTS',
    'NOTIFICATIONS',
    'PORTFOLIO_GOALS'
)
ORDER BY c.table_name, c.constraint_name, cc.position;


-- ============================================================================
-- 7. SHOW FOREIGN KEY RELATIONSHIPS ONLY
-- ============================================================================
-- [BEFORE DEMO]
-- No software operation is required.
-- R = Referential integrity / foreign key in USER_CONSTRAINTS.

SELECT fk.table_name AS child_table,
       fk.constraint_name AS foreign_key,
       pk.table_name AS parent_table,
       pk.constraint_name AS referenced_key
FROM user_constraints fk
JOIN user_constraints pk
     ON fk.r_constraint_name = pk.constraint_name
WHERE fk.constraint_type = 'R'
ORDER BY fk.table_name, fk.constraint_name;


-- ============================================================================
-- 8. USER REGISTRATION
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Register page -> create a new user -> registration succeeds.
--
-- Run this afterward to verify the new user reached Oracle.

SELECT user_id,
       name,
       email,
       role,
       is_active,
       created_at
FROM app_users
ORDER BY user_id DESC;


-- ============================================================================
-- 9. LOGIN / USER LOOKUP
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Log in to ShareSync.
--
-- Login itself normally performs a SELECT/lookup rather than inserting data.
-- Use this to verify the account exists and is active.
--
-- Replace the email with the account used for the demo.

SELECT user_id,
       name,
       email,
       role,
       is_active
FROM app_users
WHERE LOWER(email) = LOWER('tanvir@sharesync.com');


-- ============================================================================
-- 10. SECTORS
-- ============================================================================
-- [BEFORE DEMO]
-- No software operation is required.
-- Shows the parent table used by COMPANIES.

SELECT sector_id,
       sector_name,
       description
FROM sectors
ORDER BY sector_id;


-- ============================================================================
-- 11. COMPANIES + SECTORS
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Company/Admin company list or Company Detail page.
--
-- This JOIN demonstrates:
--   COMPANIES.SECTOR_ID -> SECTORS.SECTOR_ID

SELECT c.company_id,
       c.company_name,
       c.ticker_symbol,
       s.sector_name,
       c.current_price,
       c.market_cap,
       c.is_active
FROM companies c
JOIN sectors s
  ON c.sector_id = s.sector_id
ORDER BY s.sector_name, c.company_name;


-- ============================================================================
-- 12. PORTFOLIO CREATION
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Portfolio page -> Create Portfolio -> save.
--
-- Verify the newly created portfolio and its owner.

SELECT p.portfolio_id,
       p.portfolio_name,
       p.description,
       p.user_id,
       u.name AS owner_name,
       u.email,
       p.created_at
FROM portfolios p
JOIN app_users u
  ON p.user_id = u.user_id
ORDER BY p.portfolio_id DESC;


-- ============================================================================
-- 13. PORTFOLIO UPDATE
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Edit a portfolio name/description -> save.
--
-- Query the portfolio afterward.

SELECT portfolio_id,
       user_id,
       portfolio_name,
       description,
       created_at
FROM portfolios
WHERE portfolio_id = 1;


-- ============================================================================
-- 14. PORTFOLIO HOLDINGS - RAW TRANSACTION LOGIC
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open a portfolio detail page.
--
-- This shows how current holdings are derived from history.
--
-- BUY  = positive quantity
-- SELL = negative quantity
--
-- Example:
--   BUY  100
--   BUY   50
--   SELL  30
--   Current = 120

SELECT t.portfolio_id,
       p.portfolio_name,
       t.company_id,
       c.ticker_symbol,
       c.company_name,
       SUM(
           CASE
               WHEN t.transaction_type = 'BUY' THEN t.quantity
               WHEN t.transaction_type = 'SELL' THEN -t.quantity
               ELSE 0
           END
       ) AS current_quantity
FROM transactions t
JOIN portfolios p
  ON t.portfolio_id = p.portfolio_id
JOIN companies c
  ON t.company_id = c.company_id
GROUP BY t.portfolio_id,
         p.portfolio_name,
         t.company_id,
         c.ticker_symbol,
         c.company_name
HAVING SUM(
           CASE
               WHEN t.transaction_type = 'BUY' THEN t.quantity
               WHEN t.transaction_type = 'SELL' THEN -t.quantity
               ELSE 0
           END
       ) > 0
ORDER BY t.portfolio_id, t.company_id;


-- ============================================================================
-- 15. BUY TRANSACTION
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Transactions page -> Add Transaction -> BUY -> submit.
--
-- Immediately run this query to see the new row.
-- The application normally inserts through ASP.NET Core / EF Core.
--
-- This query demonstrates DML verification, not how the frontend itself
-- performs the INSERT.





-- ============================================================================
-- 16. TRANSACTION HISTORY FOR ONE PORTFOLIO
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Add/update/delete a transaction.
--
-- Change the portfolio_id if needed.

SELECT t.transaction_id,
       p.portfolio_name,
       c.ticker_symbol,
       c.company_name,
       t.transaction_type,
       t.quantity,
       t.price_per_share,
       ROUND(t.quantity * t.price_per_share, 2) AS total_amount,
       t.transaction_date
FROM transactions t
JOIN portfolios p
  ON t.portfolio_id = p.portfolio_id
JOIN companies c
  ON t.company_id = c.company_id
WHERE t.portfolio_id = 1
ORDER BY t.transaction_date DESC, t.transaction_id DESC;


-- ============================================================================
-- 17. CURRENT AVAILABLE QUANTITY
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Before attempting a SELL, or after a BUY/SELL.
--
-- This is the quantity the user currently owns.

SELECT portfolio_id,
       company_id,
       SUM(CASE WHEN transaction_type = 'BUY' THEN quantity ELSE 0 END)
       -
       SUM(CASE WHEN transaction_type = 'SELL' THEN quantity ELSE 0 END)
       AS available_quantity
FROM transactions
WHERE portfolio_id = 1
  AND company_id = 1
GROUP BY portfolio_id, company_id;


-- ============================================================================
-- 18. SELL TRANSACTION
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Transactions page -> Add Transaction -> SELL -> submit.
--
-- Run these two queries after the SELL:
--   1. Confirm the SELL row exists.
--   2. Confirm current holdings decreased.

SELECT transaction_id,
       transaction_type,
       quantity,
       price_per_share,
       transaction_date
FROM transactions
WHERE portfolio_id = 1
  AND company_id = 1
ORDER BY transaction_date DESC;


SELECT portfolio_id,
       company_id,
       SUM(CASE WHEN transaction_type = 'BUY' THEN quantity ELSE -quantity END)
       AS current_quantity
FROM transactions
WHERE portfolio_id = 1
  AND company_id = 1
GROUP BY portfolio_id, company_id;


-- ============================================================================
-- 19. TRANSACTION AUDIT AFTER BUY / SELL / UPDATE
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Add, edit, or delete a transaction.
--
-- The Oracle trigger TRG_TRANSACTIONS_AUDIT is defined as:
--   AFTER INSERT OR UPDATE OR DELETE ON TRANSACTIONS
--
-- Therefore an audit record should appear automatically.

SELECT audit_id,
       transaction_id,
       action_type,
       action_date,
       changed_by,
       details
FROM transaction_audit
ORDER BY audit_id DESC;


-- ============================================================================
-- 20. VERIFY ONLY INSERT AUDITS
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Add a new transaction.

SELECT audit_id,
       transaction_id,
       action_type,
       changed_by,
       details,
       action_date
FROM transaction_audit
WHERE action_type = 'INSERT'
ORDER BY audit_id DESC;


-- ============================================================================
-- 21. VERIFY UPDATE AUDITS
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Edit an existing transaction.
--
-- The trigger records old/new quantity and price in DETAILS.

SELECT audit_id,
       transaction_id,
       action_type,
       changed_by,
       details,
       action_date
FROM transaction_audit
WHERE action_type = 'UPDATE'
ORDER BY audit_id DESC;


-- ============================================================================
-- 22. VERIFY DELETE AUDITS
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Delete a transaction.
--
-- The trigger records DELETE.
-- TRANSACTION_ID may be NULL after the deleted transaction because the
-- foreign key is ON DELETE SET NULL.

SELECT audit_id,
       transaction_id,
       action_type,
       changed_by,
       details,
       action_date
FROM transaction_audit
WHERE action_type = 'DELETE'
ORDER BY audit_id DESC;


-- ============================================================================
-- 23. PORTFOLIO HOLDINGS VIEW
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Portfolio / Dashboard / Analytics after changing transactions.
--
-- VW_PORTFOLIO_HOLDINGS calculates:
--   current quantity
--   current market value
--   weighted average buy price
--   unrealized profit/loss
--
-- This is one of the BEST queries to show the teacher.

SELECT *
FROM vw_portfolio_holdings
ORDER BY portfolio_id, company_id;


-- ============================================================================
-- 24. VIEW FOR ONE PORTFOLIO
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open one portfolio.
--
-- Replace 1 with the portfolio ID being demonstrated.

SELECT portfolio_id,
       portfolio_name,
       ticker_symbol,
       company_name,
       current_quantity,
       current_price,
       current_market_value,
       weighted_average_buy_price,
       unrealized_profit_loss
FROM vw_portfolio_holdings
WHERE portfolio_id = 1
ORDER BY current_market_value DESC;


-- ============================================================================
-- 25. WEIGHTED AVERAGE PURCHASE PRICE
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   After BUY transactions, especially if you bought the same company at
--   different prices.
--
-- Formula:
--   Total BUY cost / Total BUY quantity

SELECT portfolio_id,
       company_id,
       fn_get_weighted_avg_price(portfolio_id, company_id)
       AS weighted_average_buy_price
FROM (
    SELECT DISTINCT portfolio_id, company_id
    FROM transactions
)
ORDER BY portfolio_id, company_id;


-- ============================================================================
-- 26. MANUAL WEIGHTED AVERAGE CALCULATION
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   After multiple BUY transactions for the same company.
--
-- This query lets you prove the function's result using plain SQL.

SELECT portfolio_id,
       company_id,
       SUM(quantity) AS total_buy_quantity,
       SUM(quantity * price_per_share) AS total_buy_cost,
       ROUND(SUM(quantity * price_per_share) / NULLIF(SUM(quantity), 0), 2)
       AS weighted_average_price
FROM transactions
WHERE transaction_type = 'BUY'
GROUP BY portfolio_id, company_id
ORDER BY portfolio_id, company_id;


-- ============================================================================
-- 27. UNREALIZED PROFIT / LOSS FUNCTION
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   After BUY/SELL or after changing a company's current price.
--
-- Formula:
--   Current market value - current quantity * weighted average buy price

SELECT portfolio_id,
       company_id,
       fn_calculate_unrealized_pl(portfolio_id, company_id)
       AS unrealized_profit_loss
FROM (
    SELECT DISTINCT portfolio_id, company_id
    FROM transactions
)
ORDER BY portfolio_id, company_id;


-- ============================================================================
-- 28. MANUAL UNREALIZED P/L CHECK
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   After changing transactions or company price.
--
-- This is an independent SQL calculation that can be compared with
-- FN_CALCULATE_UNREALIZED_PL and VW_PORTFOLIO_HOLDINGS.

SELECT t.portfolio_id,
       t.company_id,
       c.ticker_symbol,
       c.current_price,
       SUM(CASE WHEN t.transaction_type = 'BUY'
                THEN t.quantity
                ELSE -t.quantity END) AS current_quantity,
       ROUND(
           SUM(CASE WHEN t.transaction_type = 'BUY'
                    THEN t.quantity * t.price_per_share
                    ELSE 0 END)
           /
           NULLIF(
               SUM(CASE WHEN t.transaction_type = 'BUY'
                        THEN t.quantity
                        ELSE 0 END),
               0
           ),
           2
       ) AS avg_buy_price,
       ROUND(
           SUM(CASE WHEN t.transaction_type = 'BUY'
                    THEN t.quantity
                    ELSE -t.quantity END) * c.current_price,
           2
       ) AS current_market_value
FROM transactions t
JOIN companies c
  ON t.company_id = c.company_id
GROUP BY t.portfolio_id, t.company_id, c.ticker_symbol, c.current_price
HAVING SUM(CASE WHEN t.transaction_type = 'BUY'
                THEN t.quantity
                ELSE -t.quantity END) > 0;


-- ============================================================================
-- 29. WATCHLIST CREATION
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Watchlist page -> Create Watchlist -> save.

SELECT w.watchlist_id,
       w.user_id,
       u.name AS owner_name,
       w.watchlist_name,
       w.description,
       w.created_at
FROM watchlists w
JOIN app_users u
  ON w.user_id = u.user_id
ORDER BY w.watchlist_id DESC;


-- ============================================================================
-- 30. ADD COMPANY TO WATCHLIST
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Watchlist -> Add company -> optionally set target price.
--
-- The relationship is stored in WATCHLIST_ITEMS.

SELECT wi.watchlist_id,
       w.watchlist_name,
       wi.company_id,
       c.ticker_symbol,
       c.company_name,
       wi.target_price,
       c.current_price,
       wi.added_at
FROM watchlist_items wi
JOIN watchlists w
  ON wi.watchlist_id = w.watchlist_id
JOIN companies c
  ON wi.company_id = c.company_id
ORDER BY w.watchlist_name, c.ticker_symbol;


-- ============================================================================
-- 31. UPDATE WATCHLIST TARGET PRICE
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Edit a watchlist item -> change target price -> save.

SELECT w.watchlist_name,
       c.ticker_symbol,
       wi.target_price,
       c.current_price,
       ROUND(c.current_price - wi.target_price, 2) AS price_difference
FROM watchlist_items wi
JOIN watchlists w
  ON wi.watchlist_id = w.watchlist_id
JOIN companies c
  ON wi.company_id = c.company_id
ORDER BY w.watchlist_name, c.ticker_symbol;


-- ============================================================================
-- 32. WATCHLIST TARGET STATUS
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Watchlist after adding/updating a target price.
--
-- Shows whether current price has reached the target.

SELECT w.watchlist_name,
       c.ticker_symbol,
       c.current_price,
       wi.target_price,
       CASE
           WHEN wi.target_price IS NULL THEN 'NO_TARGET_SET'
           WHEN c.current_price <= wi.target_price THEN 'BUY_TARGET_REACHED'
           ELSE 'ABOVE_TARGET'
       END AS alert_status
FROM watchlist_items wi
JOIN watchlists w
  ON wi.watchlist_id = w.watchlist_id
JOIN companies c
  ON wi.company_id = c.company_id
ORDER BY w.watchlist_name, c.ticker_symbol;


-- ============================================================================
-- 33. WATCHLIST DUPLICATE CHECK
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Try adding the SAME company twice to the SAME watchlist.
--
-- The application should reject it.
-- The database also has:
--   PRIMARY KEY (watchlist_id, company_id)
--
-- This is a composite primary key.

SELECT watchlist_id,
       company_id,
       COUNT(*) AS occurrence_count
FROM watchlist_items
GROUP BY watchlist_id, company_id
HAVING COUNT(*) > 1;


-- ============================================================================
-- 34. DIVIDEND LIST
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Dividends page.
--
-- Shows the company, dividend per share and payment dates.

SELECT d.dividend_id,
       c.ticker_symbol,
       c.company_name,
       d.dividend_per_share,
       d.declaration_date,
       d.payment_date
FROM dividends d
JOIN companies c
  ON d.company_id = c.company_id
ORDER BY d.payment_date DESC;


-- ============================================================================
-- 35. DIVIDEND ESTIMATED PAYOUT
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Dividend Analytics / Dividend page.
--
-- Concept:
--   Estimated payout = dividend per share * currently held shares

SELECT d.dividend_id,
       c.ticker_symbol,
       d.dividend_per_share,
       NVL(h.current_shares, 0) AS shares_held,
       ROUND(d.dividend_per_share * NVL(h.current_shares, 0), 2)
       AS estimated_payout
FROM dividends d
JOIN companies c
  ON d.company_id = c.company_id
LEFT JOIN (
    SELECT company_id,
           SUM(
               CASE
                   WHEN transaction_type = 'BUY' THEN quantity
                   ELSE -quantity
               END
           ) AS current_shares
    FROM transactions
    WHERE portfolio_id = 1
    GROUP BY company_id
) h
  ON c.company_id = h.company_id
ORDER BY d.payment_date DESC;


-- ============================================================================
-- 36. COMPANY PRICE HISTORY
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Company Detail -> price chart.
--   Or Admin -> synchronize/update market prices.
--
-- This table stores time-series price snapshots.

SELECT ph.price_history_id,
       ph.company_id,
       c.ticker_symbol,
       c.company_name,
       ph.price,
       ph.open_price,
       ph.high_price,
       ph.low_price,
       ph.volume,
       ph.recorded_at
FROM company_price_history ph
JOIN companies c
  ON ph.company_id = c.company_id
ORDER BY ph.recorded_at DESC;


-- ============================================================================
-- 37. LATEST PRICE HISTORY PER COMPANY
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   After market synchronization / price refresh.

SELECT c.company_id,
       c.ticker_symbol,
       c.current_price,
       ph.price AS latest_history_price,
       ph.recorded_at
FROM companies c
LEFT JOIN (
    SELECT company_id,
           price,
           recorded_at,
           ROW_NUMBER() OVER (
               PARTITION BY company_id
               ORDER BY recorded_at DESC
           ) AS rn
    FROM company_price_history
) ph
  ON c.company_id = ph.company_id
 AND ph.rn = 1
ORDER BY c.company_id;


-- ============================================================================
-- 38. UPDATE CURRENT COMPANY PRICE - TEST ONLY
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Admin -> update a company's current price, OR perform a market sync.
--
-- Normally the application performs the update.
-- DO NOT execute this direct UPDATE during the teacher demo unless you
-- deliberately want to demonstrate the effect.
--
-- Example:
--
-- UPDATE companies
-- SET current_price = 160
-- WHERE company_id = 1;
--
-- COMMIT;
--
-- Then run:
--
-- SELECT company_id, ticker_symbol, current_price
-- FROM companies
-- WHERE company_id = 1;
--
-- Then run VW_PORTFOLIO_HOLDINGS again to see P/L change.
--
-- The following is intentionally commented out:
-- UPDATE companies SET current_price = 160 WHERE company_id = 1;
-- COMMIT;


-- ============================================================================
-- 39. ALERT CREATION
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Alerts page -> Create Alert -> save.
--
-- Supported alert types:
--   PRICE_ABOVE
--   PRICE_BELOW
--   PORTFOLIO_VALUE_ABOVE
--   PORTFOLIO_VALUE_BELOW

SELECT alert_id,
       user_id,
       company_id,
       portfolio_id,
       alert_type,
       threshold_value,
       is_active,
       created_at,
       triggered_at,
       message
FROM alerts
ORDER BY alert_id DESC;


-- ============================================================================
-- 40. ALERTS WITH COMPANY / PORTFOLIO NAMES
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Alerts page after creating/editing an alert.

SELECT a.alert_id,
       a.alert_type,
       a.threshold_value,
       a.is_active,
       c.ticker_symbol,
       c.company_name,
       p.portfolio_name,
       a.created_at,
       a.triggered_at,
       a.message
FROM alerts a
LEFT JOIN companies c
  ON a.company_id = c.company_id
LEFT JOIN portfolios p
  ON a.portfolio_id = p.portfolio_id
ORDER BY a.alert_id DESC;


-- ============================================================================
-- 41. NOTIFICATIONS
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Trigger an alert / receive a notification / open Notification Center.

SELECT notification_id,
       user_id,
       notification_type,
       title,
       message,
       is_read,
       created_at,
       related_entity_type,
       related_entity_id
FROM notifications
ORDER BY created_at DESC;


-- ============================================================================
-- 42. UNREAD NOTIFICATIONS
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Notification Center before/after marking notifications as read.

SELECT notification_id,
       notification_type,
       title,
       message,
       created_at
FROM notifications
WHERE user_id = 1
  AND is_read = 0
ORDER BY created_at DESC;


-- ============================================================================
-- 43. PORTFOLIO GOAL CREATION
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Goals page -> Create Goal -> save.
--
-- Supported goal types:
--   TARGET_PORTFOLIO_VALUE
--   TARGET_RETURN
--   TARGET_DIVIDEND_INCOME

SELECT goal_id,
       user_id,
       portfolio_id,
       goal_type,
       target_value,
       target_date,
       title,
       description,
       is_active,
       created_at,
       updated_at
FROM portfolio_goals
ORDER BY goal_id DESC;


-- ============================================================================
-- 44. PORTFOLIO GOAL WITH PORTFOLIO NAME
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Goals page.

SELECT g.goal_id,
       g.title,
       g.goal_type,
       g.target_value,
       g.target_date,
       p.portfolio_name,
       g.is_active
FROM portfolio_goals g
JOIN portfolios p
  ON g.portfolio_id = p.portfolio_id
WHERE g.user_id = 1
ORDER BY g.created_at DESC;


-- ============================================================================
-- 45. PORTFOLIO SNAPSHOTS
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Create a portfolio snapshot / open portfolio performance.
--
-- Snapshots store portfolio valuation at a particular date.

SELECT snapshot_id,
       portfolio_id,
       snapshot_date,
       total_value
FROM portfolio_snapshots
ORDER BY portfolio_id, snapshot_date;


-- ============================================================================
-- 46. PORTFOLIO PERFORMANCE FROM SNAPSHOTS
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open portfolio performance chart.

SELECT p.portfolio_name,
       ps.snapshot_date,
       ps.total_value
FROM portfolio_snapshots ps
JOIN portfolios p
  ON ps.portfolio_id = p.portfolio_id
WHERE ps.portfolio_id = 1
ORDER BY ps.snapshot_date;


-- ============================================================================
-- 47. STORED PROCEDURE: GENERATE PORTFOLIO SNAPSHOT
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   You may use this when demonstrating the database-side snapshot procedure.
--
-- This procedure:
--   1. Checks the portfolio exists.
--   2. Calculates current holdings.
--   3. Multiplies holdings by current company price.
--   4. MERGEs the result into PORTFOLIO_SNAPSHOTS.
--
-- IMPORTANT:
--   The procedure does NOT COMMIT. The calling session/client controls COMMIT.
--
-- First discover a valid portfolio_id.

SELECT portfolio_id, portfolio_name
FROM portfolios
ORDER BY portfolio_id;


-- Now execute for portfolio 1.
VARIABLE v_total_value NUMBER
VARIABLE v_status VARCHAR2(50)
VARIABLE v_message VARCHAR2(500)

BEGIN
    sp_generate_portfolio_snapshot(
        p_portfolio_id  => 1,
        p_snapshot_date => TRUNC(SYSDATE),
        p_total_value   => :v_total_value,
        p_status        => :v_status,
        p_message       => :v_message
    );
END;
/

PRINT v_total_value
PRINT v_status
PRINT v_message

COMMIT;


-- Verify the result:
SELECT *
FROM portfolio_snapshots
WHERE portfolio_id = 1
ORDER BY snapshot_date DESC;


-- ============================================================================
-- 48. STORED PROCEDURE: RECORD A BUY
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Use this section ONLY when the teacher specifically asks you to
--   demonstrate the Oracle stored procedure itself.
--
-- Normal ShareSync frontend transaction creation currently uses the
-- ASP.NET Core / Entity Framework Core transaction service.
-- This procedure is a database-side implementation of transaction validation.
--
-- IMPORTANT:
--   Use a small test quantity so the demonstration is easy to undo if needed.

VARIABLE v_tx_id NUMBER
VARIABLE v_status VARCHAR2(50)
VARIABLE v_message VARCHAR2(500)

BEGIN
    sp_record_transaction(
        p_portfolio_id     => 1,
        p_company_id       => 1,
        p_transaction_type => 'BUY',
        p_quantity         => 1,
        p_price_per_share  => 120,
        p_changed_by       => 'ORACLE_DEMO',
        p_transaction_id   => :v_tx_id,
        p_status           => :v_status,
        p_message          => :v_message
    );
END;
/

PRINT v_tx_id
PRINT v_status
PRINT v_message

COMMIT;


-- Verify:
SELECT transaction_id,
       portfolio_id,
       company_id,
       transaction_type,
       quantity,
       price_per_share,
       transaction_date
FROM transactions
WHERE transaction_id = :v_tx_id;


-- ============================================================================
-- 49. STORED PROCEDURE: OVERSALE PREVENTION
-- ============================================================================
-- [TEST]
-- Software operation:
--   No software action is required.
--
-- This intentionally attempts to sell more shares than are available.
-- The procedure should return:
--   OVERSELL_REJECTED
--
-- This is one of the best live database-business-rule demonstrations.

VARIABLE v_tx_id NUMBER
VARIABLE v_status VARCHAR2(50)
VARIABLE v_message VARCHAR2(500)

BEGIN
    sp_record_transaction(
        p_portfolio_id     => 1,
        p_company_id       => 1,
        p_transaction_type => 'SELL',
        p_quantity         => 999999,
        p_price_per_share  => 100,
        p_changed_by       => 'ORACLE_DEMO',
        p_transaction_id   => :v_tx_id,
        p_status            => :v_status,
        p_message           => :v_message
    );
END;
/

PRINT v_tx_id
PRINT v_status
PRINT v_message

-- No COMMIT is needed because a rejected procedure call does not insert
-- the transaction.


-- ============================================================================
-- 50. FUNCTION: WEIGHTED AVERAGE PRICE
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   After multiple BUY transactions for the same company.
--
-- Function returns a single calculated NUMBER.

SELECT fn_get_weighted_avg_price(1, 1) AS weighted_average_price
FROM dual;


-- ============================================================================
-- 51. FUNCTION: UNREALIZED PROFIT / LOSS
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   After changing transactions or current company price.

SELECT fn_calculate_unrealized_pl(1, 1) AS unrealized_profit_loss
FROM dual;


-- ============================================================================
-- 52. SIX CORE REPORTS
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Reports page.
--
-- The following six queries correspond to the project's main academic
-- reporting concepts.
--
-- Change "1" to the portfolio ID being demonstrated.


-- REPORT 1: PORTFOLIO HOLDINGS
SELECT
    p.portfolio_id,
    p.portfolio_name,
    c.ticker_symbol,
    c.company_name,
    s.sector_name,
    SUM(CASE
        WHEN t.transaction_type = 'BUY' THEN t.quantity
        ELSE -t.quantity
    END) AS current_quantity,
    c.current_price,
    ROUND(
        SUM(CASE
            WHEN t.transaction_type = 'BUY' THEN t.quantity
            ELSE -t.quantity
        END) * c.current_price,
        2
    ) AS market_value,
    ROUND(
        SUM(CASE
            WHEN t.transaction_type = 'BUY'
            THEN t.quantity * t.price_per_share
            ELSE 0
        END)
        /
        NULLIF(
            SUM(CASE
                WHEN t.transaction_type = 'BUY' THEN t.quantity
                ELSE 0
            END),
            0
        ),
        2
    ) AS weighted_avg_buy_price,
    ROUND(
        (
            SUM(CASE
                WHEN t.transaction_type = 'BUY' THEN t.quantity
                ELSE -t.quantity
            END) * c.current_price
        )
        -
        (
            SUM(CASE
                WHEN t.transaction_type = 'BUY' THEN t.quantity
                ELSE -t.quantity
            END)
            *
            (
                SUM(CASE
                    WHEN t.transaction_type = 'BUY'
                    THEN t.quantity * t.price_per_share
                    ELSE 0
                END)
                /
                NULLIF(
                    SUM(CASE
                        WHEN t.transaction_type = 'BUY' THEN t.quantity
                        ELSE 0
                    END),
                    0
                )
            )
        ),
        2
    ) AS unrealized_profit_loss
FROM transactions t
JOIN portfolios p ON t.portfolio_id = p.portfolio_id
JOIN companies c  ON t.company_id = c.company_id
JOIN sectors s    ON c.sector_id = s.sector_id
WHERE p.portfolio_id = 1
GROUP BY p.portfolio_id,
         p.portfolio_name,
         c.ticker_symbol,
         c.company_name,
         s.sector_name,
         c.current_price
HAVING SUM(CASE
    WHEN t.transaction_type = 'BUY' THEN t.quantity
    ELSE -t.quantity
END) > 0
ORDER BY market_value DESC;


-- REPORT 2: PORTFOLIO PERFORMANCE / ROI
WITH PortfolioHoldings AS (
    SELECT
        t.portfolio_id,
        t.company_id,
        SUM(CASE
            WHEN t.transaction_type = 'BUY' THEN t.quantity
            ELSE -t.quantity
        END) AS current_qty,
        SUM(CASE
            WHEN t.transaction_type = 'BUY'
            THEN t.quantity * t.price_per_share
            ELSE 0
        END)
        /
        NULLIF(
            SUM(CASE
                WHEN t.transaction_type = 'BUY' THEN t.quantity
                ELSE 0
            END),
            0
        ) AS avg_cost,
        c.current_price
    FROM transactions t
    JOIN companies c ON t.company_id = c.company_id
    WHERE t.portfolio_id = 1
    GROUP BY t.portfolio_id, t.company_id, c.current_price
    HAVING SUM(CASE
        WHEN t.transaction_type = 'BUY' THEN t.quantity
        ELSE -t.quantity
    END) > 0
)
SELECT
    p.portfolio_id,
    p.portfolio_name,
    COUNT(h.company_id) AS total_companies_held,
    ROUND(SUM(h.current_qty * h.avg_cost), 2) AS total_cost_basis,
    ROUND(SUM(h.current_qty * h.current_price), 2) AS current_portfolio_value,
    ROUND(
        SUM(h.current_qty * h.current_price)
        - SUM(h.current_qty * h.avg_cost),
        2
    ) AS total_unrealized_profit_loss,
    ROUND(
        (
            (
                SUM(h.current_qty * h.current_price)
                - SUM(h.current_qty * h.avg_cost)
            )
            /
            NULLIF(SUM(h.current_qty * h.avg_cost), 0)
        ) * 100,
        2
    ) AS return_percentage
FROM portfolios p
LEFT JOIN PortfolioHoldings h
  ON p.portfolio_id = h.portfolio_id
WHERE p.portfolio_id = 1
GROUP BY p.portfolio_id, p.portfolio_name;


-- REPORT 3: TRANSACTION HISTORY
SELECT
    t.transaction_id,
    p.portfolio_name,
    c.ticker_symbol,
    c.company_name,
    t.transaction_type,
    t.quantity,
    t.price_per_share,
    ROUND(t.quantity * t.price_per_share, 2) AS total_amount,
    TO_CHAR(t.transaction_date, 'YYYY-MM-DD HH24:MI:SS') AS formatted_date
FROM transactions t
JOIN portfolios p ON t.portfolio_id = p.portfolio_id
JOIN companies c  ON t.company_id = c.company_id
WHERE p.portfolio_id = 1
ORDER BY t.transaction_date DESC, t.transaction_id DESC;


-- REPORT 4: COMPANY / SECTOR ALLOCATION
WITH SectorTotals AS (
    SELECT
        s.sector_name,
        SUM(t.quantity * c.current_price) AS sector_market_val,
        COUNT(DISTINCT c.company_id) AS companies_in_sector
    FROM transactions t
    JOIN companies c ON t.company_id = c.company_id
    JOIN sectors s   ON c.sector_id = s.sector_id
    WHERE t.portfolio_id = 1
    GROUP BY s.sector_name
)
SELECT
    sector_name,
    companies_in_sector,
    ROUND(sector_market_val, 2) AS sector_investment_value,
    ROUND(
        (
            sector_market_val
            / NULLIF(SUM(sector_market_val) OVER(), 0)
        ) * 100,
        2
    ) AS allocation_percentage
FROM SectorTotals
ORDER BY sector_investment_value DESC;


-- REPORT 5: DIVIDEND INCOME
SELECT
    d.dividend_id,
    c.ticker_symbol,
    c.company_name,
    d.dividend_per_share,
    d.declaration_date,
    d.payment_date,
    NVL(h.current_shares, 0) AS shares_held,
    ROUND(
        d.dividend_per_share * NVL(h.current_shares, 0),
        2
    ) AS estimated_payout
FROM dividends d
JOIN companies c ON d.company_id = c.company_id
LEFT JOIN (
    SELECT
        t.company_id,
        SUM(CASE
            WHEN t.transaction_type = 'BUY' THEN t.quantity
            ELSE -t.quantity
        END) AS current_shares
    FROM transactions t
    WHERE t.portfolio_id = 1
    GROUP BY t.company_id
) h
  ON c.company_id = h.company_id
ORDER BY d.payment_date DESC;


-- REPORT 6: WATCHLIST TARGET PRICE
SELECT
    w.watchlist_id,
    w.watchlist_name,
    c.ticker_symbol,
    c.company_name,
    c.current_price,
    wi.target_price,
    ROUND(c.current_price - wi.target_price, 2) AS price_difference,
    ROUND(
        (
            (c.current_price - wi.target_price)
            / NULLIF(wi.target_price, 0)
        ) * 100,
        2
    ) AS diff_percentage,
    CASE
        WHEN wi.target_price IS NOT NULL
             AND c.current_price <= wi.target_price
            THEN 'BUY_TARGET_REACHED'
        WHEN wi.target_price IS NOT NULL
             AND c.current_price > wi.target_price
            THEN 'ABOVE_TARGET'
        ELSE 'NO_TARGET_SET'
    END AS alert_status
FROM watchlist_items wi
JOIN watchlists w ON wi.watchlist_id = w.watchlist_id
JOIN companies c  ON wi.company_id = c.company_id
WHERE w.user_id = 1
ORDER BY w.watchlist_name, c.ticker_symbol;


-- ============================================================================
-- 53. BASIC AGGREGATION QUERIES
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Dashboard / Analytics / Reports.
--
-- Useful for explaining COUNT, SUM, GROUP BY and CASE.


-- Number of companies per sector
SELECT s.sector_name,
       COUNT(c.company_id) AS company_count
FROM sectors s
LEFT JOIN companies c
  ON s.sector_id = c.sector_id
GROUP BY s.sector_id, s.sector_name
ORDER BY company_count DESC;


-- BUY vs SELL counts and quantities
SELECT transaction_type,
       COUNT(*) AS transaction_count,
       SUM(quantity) AS total_shares
FROM transactions
GROUP BY transaction_type
ORDER BY transaction_type;


-- Total transaction value
SELECT ROUND(
           SUM(quantity * price_per_share),
           2
       ) AS total_transaction_value
FROM transactions;


-- Companies appearing in multiple watchlists
SELECT c.company_name,
       c.ticker_symbol,
       COUNT(wi.watchlist_id) AS watchlist_count
FROM companies c
JOIN watchlist_items wi
  ON c.company_id = wi.company_id
GROUP BY c.company_id, c.company_name, c.ticker_symbol
HAVING COUNT(wi.watchlist_id) > 1
ORDER BY watchlist_count DESC;


-- Dividend totals by company
SELECT c.company_name,
       c.ticker_symbol,
       COUNT(d.dividend_id) AS dividend_count,
       SUM(d.dividend_per_share) AS total_dividend_per_share
FROM companies c
LEFT JOIN dividends d
  ON c.company_id = d.company_id
GROUP BY c.company_id, c.company_name, c.ticker_symbol
ORDER BY total_dividend_per_share DESC NULLS LAST;


-- BUY and SELL value by portfolio
SELECT p.portfolio_name,
       COUNT(t.transaction_id) AS transaction_count,
       SUM(CASE
           WHEN t.transaction_type = 'BUY'
           THEN t.quantity * t.price_per_share
           ELSE 0
       END) AS total_buy_value,
       SUM(CASE
           WHEN t.transaction_type = 'SELL'
           THEN t.quantity * t.price_per_share
           ELSE 0
       END) AS total_sell_value
FROM portfolios p
LEFT JOIN transactions t
  ON p.portfolio_id = t.portfolio_id
GROUP BY p.portfolio_id, p.portfolio_name
ORDER BY p.portfolio_name;


-- ============================================================================
-- 54. DATABASE CONSTRAINT DEMONSTRATION
-- ============================================================================
-- [TEST]
-- Software operation:
--   No frontend operation is required.
--
-- These tests intentionally cause Oracle errors.
-- They are designed to prove that the database itself enforces rules.
--
-- Expected Oracle errors:
--   ORA-02290 = CHECK constraint violated
--   ORA-00001 = UNIQUE constraint violated
--
-- Each test is wrapped in its own block so the invalid statement is caught.
-- No invalid row should remain in the database.


SET SERVEROUTPUT ON SIZE UNLIMITED


-- TEST 1: Quantity must be > 0
BEGIN
    SAVEPOINT demo_ck_quantity;

    INSERT INTO transactions (
        portfolio_id,
        company_id,
        transaction_type,
        quantity,
        price_per_share
    )
    VALUES (1, 1, 'BUY', -10, 100);

    DBMS_OUTPUT.PUT_LINE('FAIL: Negative quantity was accepted.');
    ROLLBACK TO demo_ck_quantity;

EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE(
            'PASS: Negative quantity rejected -> ' || SQLERRM
        );
        ROLLBACK TO demo_ck_quantity;
END;
/


-- TEST 2: Price must be > 0
BEGIN
    SAVEPOINT demo_ck_price;

    INSERT INTO transactions (
        portfolio_id,
        company_id,
        transaction_type,
        quantity,
        price_per_share
    )
    VALUES (1, 1, 'BUY', 10, -100);

    DBMS_OUTPUT.PUT_LINE('FAIL: Negative price was accepted.');
    ROLLBACK TO demo_ck_price;

EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE(
            'PASS: Negative price rejected -> ' || SQLERRM
        );
        ROLLBACK TO demo_ck_price;
END;
/


-- TEST 3: Transaction type must be BUY or SELL
BEGIN
    SAVEPOINT demo_ck_type;

    INSERT INTO transactions (
        portfolio_id,
        company_id,
        transaction_type,
        quantity,
        price_per_share
    )
    VALUES (1, 1, 'HOLD', 10, 100);

    DBMS_OUTPUT.PUT_LINE('FAIL: Invalid transaction type was accepted.');
    ROLLBACK TO demo_ck_type;

EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE(
            'PASS: Invalid transaction type rejected -> ' || SQLERRM
        );
        ROLLBACK TO demo_ck_type;
END;
/


-- TEST 4: Dividend payment date cannot be before declaration date
BEGIN
    SAVEPOINT demo_ck_dividend;

    INSERT INTO dividends (
        company_id,
        dividend_per_share,
        declaration_date,
        payment_date
    )
    VALUES (
        1,
        10,
        DATE '2026-05-10',
        DATE '2026-05-01'
    );

    DBMS_OUTPUT.PUT_LINE('FAIL: Invalid dividend dates were accepted.');
    ROLLBACK TO demo_ck_dividend;

EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE(
            'PASS: Invalid dividend dates rejected -> ' || SQLERRM
        );
        ROLLBACK TO demo_ck_dividend;
END;
/


-- TEST 5: Watchlist composite primary key
-- The same company cannot occur twice in the same watchlist.
--
-- This first finds an existing pair, then tries to insert it again.

DECLARE
    v_watchlist_id NUMBER;
    v_company_id   NUMBER;
BEGIN
    SELECT watchlist_id, company_id
    INTO v_watchlist_id, v_company_id
    FROM (
        SELECT watchlist_id, company_id
        FROM watchlist_items
        ORDER BY watchlist_id, company_id
    )
    WHERE ROWNUM = 1;

    SAVEPOINT demo_pk_watchlist;

    INSERT INTO watchlist_items (
        watchlist_id,
        company_id,
        target_price
    )
    VALUES (
        v_watchlist_id,
        v_company_id,
        999
    );

    DBMS_OUTPUT.PUT_LINE(
        'FAIL: Duplicate watchlist/company pair was accepted.'
    );

    ROLLBACK TO demo_pk_watchlist;

EXCEPTION
    WHEN NO_DATA_FOUND THEN
        DBMS_OUTPUT.PUT_LINE(
            'SKIP: No existing watchlist item is available for this test.'
        );
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE(
            'PASS: Duplicate composite key rejected -> ' || SQLERRM
        );
        ROLLBACK TO demo_pk_watchlist;
END;
/


-- ============================================================================
-- 55. UNIQUE CONSTRAINT TEST: DUPLICATE TICKER
-- ============================================================================
-- [TEST]
-- Software operation:
--   No software action required.
--
-- This attempts to insert a company with an existing ticker.
-- The UNIQUE constraint should reject it.

DECLARE
    v_ticker companies.ticker_symbol%TYPE;
    v_sector_id NUMBER;
BEGIN
    SELECT ticker_symbol, sector_id
    INTO v_ticker, v_sector_id
    FROM companies
    WHERE ROWNUM = 1;

    SAVEPOINT demo_unique_ticker;

    INSERT INTO companies (
        company_name,
        ticker_symbol,
        sector_id,
        current_price
    )
    VALUES (
        'Duplicate Ticker Test',
        v_ticker,
        v_sector_id,
        100
    );

    DBMS_OUTPUT.PUT_LINE(
        'FAIL: Duplicate ticker was accepted.'
    );

    ROLLBACK TO demo_unique_ticker;

EXCEPTION
    WHEN NO_DATA_FOUND THEN
        DBMS_OUTPUT.PUT_LINE(
            'SKIP: No company exists for duplicate ticker test.'
        );
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE(
            'PASS: Duplicate ticker rejected -> ' || SQLERRM
        );
        ROLLBACK TO demo_unique_ticker;
END;
/


-- ============================================================================
-- 56. UNIQUE CONSTRAINT TEST: DUPLICATE USER EMAIL
-- ============================================================================
-- [TEST]
-- Software operation:
--   No software action required.
--
-- The first INSERT creates a temporary email, the second INSERT tries the
-- same email. The entire test is rolled back.

BEGIN
    SAVEPOINT demo_unique_email;

    INSERT INTO app_users (
        name,
        email,
        password_hash
    )
    VALUES (
        'Demo Unique Test User',
        'unique_demo_test@sharesync.local',
        'demo_hash'
    );

    BEGIN
        INSERT INTO app_users (
            name,
            email,
            password_hash
        )
        VALUES (
            'Demo Duplicate User',
            'unique_demo_test@sharesync.local',
            'demo_hash'
        );

        DBMS_OUTPUT.PUT_LINE(
            'FAIL: Duplicate email was accepted.'
        );

    EXCEPTION
        WHEN OTHERS THEN
            DBMS_OUTPUT.PUT_LINE(
                'PASS: Duplicate email rejected -> ' || SQLERRM
            );
    END;

    ROLLBACK TO demo_unique_email;
END;
/


-- ============================================================================
-- 57. FOREIGN KEY TEST
-- ============================================================================
-- [TEST]
-- Software operation:
--   No software action required.
--
-- Attempts to create a portfolio for a non-existing user.
-- The foreign key should reject it.

BEGIN
    SAVEPOINT demo_fk;

    INSERT INTO portfolios (
        user_id,
        portfolio_name,
        description
    )
    VALUES (
        999999999,
        'Invalid FK Test',
        'Should be rejected'
    );

    DBMS_OUTPUT.PUT_LINE(
        'FAIL: Invalid foreign key was accepted.'
    );

    ROLLBACK TO demo_fk;

EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE(
            'PASS: Foreign key rejected -> ' || SQLERRM
        );
        ROLLBACK TO demo_fk;
END;
/


-- ============================================================================
-- 58. ACID / ROLLBACK DEMONSTRATION
-- ============================================================================
-- [TEST]
-- Software operation:
--   No software operation required.
--
-- Demonstrates ATOMICITY:
--   We insert a temporary company and then ROLLBACK.
--   The inserted row disappears.

DECLARE
    v_before NUMBER;
    v_after  NUMBER;
BEGIN
    SELECT COUNT(*)
    INTO v_before
    FROM companies;

    SAVEPOINT demo_atomicity;

    INSERT INTO companies (
        company_name,
        ticker_symbol,
        sector_id,
        current_price
    )
    VALUES (
        'ACID Temporary Company',
        'ACIDTMP',
        1,
        50
    );

    ROLLBACK TO demo_atomicity;

    SELECT COUNT(*)
    INTO v_after
    FROM companies;

    IF v_before = v_after THEN
        DBMS_OUTPUT.PUT_LINE(
            'PASS: ROLLBACK restored the original company count.'
        );
    ELSE
        DBMS_OUTPUT.PUT_LINE(
            'FAIL: Company count changed unexpectedly.'
        );
    END IF;
END;
/


-- ============================================================================
-- 59. DIRECT TRIGGER TEST
-- ============================================================================
-- [TEST]
-- Software operation:
--   No software operation required.
--
-- This is the cleanest way to prove the Oracle trigger itself works:
--   1. Direct INSERT into TRANSACTIONS.
--   2. Trigger automatically inserts into TRANSACTION_AUDIT.
--   3. Test transaction and audit can then be cleaned up.
--
-- This bypasses the frontend intentionally.

DECLARE
    v_tx_id       NUMBER;
    v_before_audit NUMBER;
    v_after_audit  NUMBER;
BEGIN
    SELECT COUNT(*)
    INTO v_before_audit
    FROM transaction_audit;

    INSERT INTO transactions (
        portfolio_id,
        company_id,
        transaction_type,
        quantity,
        price_per_share
    )
    VALUES (
        1,
        1,
        'BUY',
        1,
        120
    )
    RETURNING transaction_id INTO v_tx_id;

    SELECT COUNT(*)
    INTO v_after_audit
    FROM transaction_audit;

    IF v_after_audit > v_before_audit THEN
        DBMS_OUTPUT.PUT_LINE(
            'PASS: Trigger created an audit record for transaction '
            || v_tx_id
        );
    ELSE
        DBMS_OUTPUT.PUT_LINE(
            'FAIL: Audit record was not created.'
        );
    END IF;

    -- Clean up the test transaction.
    -- Because the audit row is ON DELETE SET NULL, the audit remains.
    DELETE FROM transactions
    WHERE transaction_id = v_tx_id;

    COMMIT;
END;
/


-- ============================================================================
-- 60. VERIFY THE TRIGGER DEFINITION
-- ============================================================================
-- [BEFORE DEMO / VIVA]
-- No software operation is required.
-- Use this if the teacher asks:
--   "Show me the trigger source."

SELECT trigger_name,
       trigger_type,
       triggering_event,
       table_name,
       status
FROM user_triggers
WHERE trigger_name = 'TRG_TRANSACTIONS_AUDIT';


-- Full trigger source
SELECT line,
       text
FROM user_source
WHERE name = 'TRG_TRANSACTIONS_AUDIT'
  AND type = 'TRIGGER'
ORDER BY line;


-- ============================================================================
-- 61. VERIFY VIEW DEFINITION
-- ============================================================================
-- [BEFORE DEMO / VIVA]
-- No software operation is required.

SELECT view_name
FROM user_views
WHERE view_name = 'VW_PORTFOLIO_HOLDINGS';


SELECT text
FROM user_views
WHERE view_name = 'VW_PORTFOLIO_HOLDINGS';


-- ============================================================================
-- 62. VERIFY FUNCTION / PROCEDURE OBJECTS
-- ============================================================================
-- [BEFORE DEMO / VIVA]
-- No software operation is required.

SELECT object_name,
       object_type,
       status
FROM user_objects
WHERE object_name IN (
    'FN_GET_WEIGHTED_AVG_PRICE',
    'FN_CALCULATE_UNREALIZED_PL',
    'SP_RECORD_TRANSACTION',
    'SP_GENERATE_PORTFOLIO_SNAPSHOT',
    'TRG_TRANSACTIONS_AUDIT',
    'VW_PORTFOLIO_HOLDINGS'
)
ORDER BY object_type, object_name;


-- ============================================================================
-- 63. SHOW STORED FUNCTION SOURCE
-- ============================================================================
-- [BEFORE DEMO / VIVA]
-- No software operation is required.

SELECT line,
       text
FROM user_source
WHERE name = 'FN_GET_WEIGHTED_AVG_PRICE'
  AND type = 'FUNCTION'
ORDER BY line;


SELECT line,
       text
FROM user_source
WHERE name = 'FN_CALCULATE_UNREALIZED_PL'
  AND type = 'FUNCTION'
ORDER BY line;


-- ============================================================================
-- 64. SHOW STORED PROCEDURE SOURCE
-- ============================================================================
-- [BEFORE DEMO / VIVA]
-- No software operation is required.

SELECT line,
       text
FROM user_source
WHERE name = 'SP_RECORD_TRANSACTION'
  AND type = 'PROCEDURE'
ORDER BY line;


SELECT line,
       text
FROM user_source
WHERE name = 'SP_GENERATE_PORTFOLIO_SNAPSHOT'
  AND type = 'PROCEDURE'
ORDER BY line;


-- ============================================================================
-- 65. SHOW INDEXES
-- ============================================================================
-- [BEFORE DEMO / VIVA]
-- No software operation is required.
--
-- Use this if the teacher asks:
--   "Where are your indexes and why?"

SELECT index_name,
       table_name,
       index_type,
       uniqueness,
       status
FROM user_indexes
WHERE table_name IN (
    'TRANSACTIONS',
    'DIVIDENDS',
    'COMPANIES',
    'COMPANY_PRICE_HISTORY',
    'ALERTS',
    'NOTIFICATIONS',
    'PORTFOLIO_GOALS'
)
ORDER BY table_name, index_name;


-- ============================================================================
-- 66. SHOW INDEX COLUMNS
-- ============================================================================
-- [BEFORE DEMO / VIVA]
-- No software operation is required.

SELECT index_name,
       table_name,
       column_name,
       column_position
FROM user_ind_columns
WHERE index_name IN (
    'IDX_TX_PORTFOLIO_DATE',
    'IDX_TX_COMPANY',
    'IDX_DIVIDENDS_COMP_PAYDATE',
    'IDX_COMPANIES_SECTOR',
    'IDX_PRICE_HIST_COMP_DATE',
    'IDX_ALERTS_USER',
    'IDX_ALERTS_PRICE_EVAL',
    'IDX_ALERTS_PORT_EVAL',
    'IDX_NOTIFICATIONS_USER',
    'IDX_NOTIFICATIONS_ENTITY',
    'IDX_PORTFOLIO_GOALS_USER',
    'IDX_PORTFOLIO_GOALS_PORTFOLIO'
)
ORDER BY index_name, column_position;


-- ============================================================================
-- 67. EXPLAIN PLAN FOR A COMMON QUERY
-- ============================================================================
-- [VIVA / OPTIONAL]
-- Software operation:
--   No software operation required.
--
-- This demonstrates that indexes can be investigated with Oracle's optimizer.
--
-- NOTE:
--   PLAN_TABLE must be available in your schema for this demonstration.

EXPLAIN PLAN FOR
SELECT transaction_id,
       transaction_type,
       quantity,
       price_per_share,
       transaction_date
FROM transactions
WHERE portfolio_id = 1
  AND transaction_date <= SYSDATE;

SELECT *
FROM TABLE(DBMS_XPLAN.DISPLAY);


-- ============================================================================
-- 68. COMPANY / SECTOR JOIN
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Company list / Admin / Company detail.

SELECT c.company_name,
       c.ticker_symbol,
       s.sector_name,
       c.current_price
FROM companies c
JOIN sectors s
  ON c.sector_id = s.sector_id
ORDER BY s.sector_name, c.company_name;


-- ============================================================================
-- 69. USER -> PORTFOLIO RELATIONSHIP
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Create/edit portfolio.

SELECT u.user_id,
       u.name,
       u.email,
       p.portfolio_id,
       p.portfolio_name
FROM app_users u
JOIN portfolios p
  ON u.user_id = p.user_id
ORDER BY u.user_id, p.portfolio_id;


-- ============================================================================
-- 70. PORTFOLIO -> TRANSACTION -> COMPANY RELATIONSHIP
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Add a transaction.

SELECT p.portfolio_name,
       c.ticker_symbol,
       t.transaction_type,
       t.quantity,
       t.price_per_share,
       t.transaction_date
FROM portfolios p
JOIN transactions t
  ON p.portfolio_id = t.portfolio_id
JOIN companies c
  ON t.company_id = c.company_id
ORDER BY t.transaction_date DESC;


-- ============================================================================
-- 71. WATCHLIST -> COMPANY MANY-TO-MANY RELATIONSHIP
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Add companies to a watchlist.

SELECT w.watchlist_name,
       c.ticker_symbol,
       c.company_name,
       wi.target_price
FROM watchlists w
JOIN watchlist_items wi
  ON w.watchlist_id = wi.watchlist_id
JOIN companies c
  ON wi.company_id = c.company_id
ORDER BY w.watchlist_name, c.ticker_symbol;


-- ============================================================================
-- 72. DIVIDENDS BY COMPANY
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Add/edit/delete a dividend from the appropriate administrative workflow.

SELECT c.ticker_symbol,
       c.company_name,
       COUNT(d.dividend_id) AS dividend_count,
       ROUND(SUM(d.dividend_per_share), 2) AS total_dividend_per_share
FROM companies c
LEFT JOIN dividends d
  ON c.company_id = d.company_id
GROUP BY c.company_id, c.company_name, c.ticker_symbol
ORDER BY total_dividend_per_share DESC NULLS LAST;


-- ============================================================================
-- 73. TRANSACTION COUNTS BY TYPE
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Add BUY/SELL transactions.

SELECT transaction_type,
       COUNT(*) AS number_of_transactions,
       SUM(quantity) AS total_quantity,
       ROUND(SUM(quantity * price_per_share), 2) AS total_value
FROM transactions
GROUP BY transaction_type
ORDER BY transaction_type;


-- ============================================================================
-- 74. CURRENT HOLDINGS FOR ALL PORTFOLIOS
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Add/edit/delete BUY or SELL transactions.

SELECT portfolio_id,
       portfolio_name,
       company_id,
       ticker_symbol,
       company_name,
       current_quantity,
       current_price,
       current_market_value,
       weighted_average_buy_price,
       unrealized_profit_loss
FROM vw_portfolio_holdings
ORDER BY portfolio_id, current_market_value DESC;


-- ============================================================================
-- 75. DATA INTEGRITY CHECK: NEGATIVE HOLDINGS
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   After BUY/SELL operations.
--
-- This should normally return ZERO rows.
-- A result would indicate an invalid negative holding.

SELECT portfolio_id,
       company_id,
       SUM(
           CASE
               WHEN transaction_type = 'BUY' THEN quantity
               ELSE -quantity
           END
       ) AS current_quantity
FROM transactions
GROUP BY portfolio_id, company_id
HAVING SUM(
           CASE
               WHEN transaction_type = 'BUY' THEN quantity
               ELSE -quantity
           END
       ) < 0;


-- ============================================================================
-- 76. DATA INTEGRITY CHECK: INVALID TRANSACTION TYPES
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   After transaction operations.
--
-- Should return ZERO rows because the CHECK constraint only allows BUY/SELL.

SELECT *
FROM transactions
WHERE transaction_type NOT IN ('BUY', 'SELL');


-- ============================================================================
-- 77. DATA INTEGRITY CHECK: INVALID QUANTITY / PRICE
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   After transaction operations.
--
-- Both should return ZERO rows.

SELECT *
FROM transactions
WHERE quantity <= 0;


SELECT *
FROM transactions
WHERE price_per_share <= 0;


-- ============================================================================
-- 78. DATA INTEGRITY CHECK: INVALID DIVIDEND DATES
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   After dividend operations.
--
-- Should return ZERO rows.

SELECT *
FROM dividends
WHERE payment_date < declaration_date;


-- ============================================================================
-- 79. DATA INTEGRITY CHECK: ORPHANED FOREIGN KEYS
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   After portfolio/company/watchlist changes.
--
-- These should all return ZERO rows.


-- Portfolios without valid users
SELECT p.*
FROM portfolios p
LEFT JOIN app_users u
  ON p.user_id = u.user_id
WHERE u.user_id IS NULL;


-- Companies without valid sectors
SELECT c.*
FROM companies c
LEFT JOIN sectors s
  ON c.sector_id = s.sector_id
WHERE s.sector_id IS NULL;


-- Transactions without valid portfolios
SELECT t.*
FROM transactions t
LEFT JOIN portfolios p
  ON t.portfolio_id = p.portfolio_id
WHERE p.portfolio_id IS NULL;


-- Transactions without valid companies
SELECT t.*
FROM transactions t
LEFT JOIN companies c
  ON t.company_id = c.company_id
WHERE c.company_id IS NULL;


-- ============================================================================
-- 80. ADMIN DATABASE HEALTH CHECK
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Admin -> System Diagnostics.
--
-- These queries reproduce the important counts shown by an admin dashboard.

SELECT COUNT(*) AS total_users
FROM app_users;


SELECT COUNT(*) AS active_users
FROM app_users
WHERE is_active = 1;


SELECT COUNT(*) AS total_companies
FROM companies;


SELECT COUNT(*) AS active_companies
FROM companies
WHERE is_active = 1;


SELECT COUNT(*) AS total_sectors
FROM sectors;


SELECT COUNT(*) AS total_portfolios
FROM portfolios;


SELECT COUNT(*) AS total_transactions
FROM transactions;


SELECT COUNT(*) AS total_audit_records
FROM transaction_audit;


SELECT COUNT(*) AS total_price_history_records
FROM company_price_history;


-- ============================================================================
-- 81. LATEST MARKET SYNC / PRICE HISTORY TIME
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Admin -> market synchronization / refresh.
--
-- Useful to show the latest database timestamp for market data.

SELECT MAX(recorded_at) AS latest_price_history_time,
       COUNT(*) AS total_price_history_rows
FROM company_price_history;


-- ============================================================================
-- 82. DSE / MARKET DATA STATUS PER COMPANY
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Perform a market synchronization.

SELECT c.company_id,
       c.ticker_symbol,
       c.current_price,
       MAX(ph.recorded_at) AS latest_recorded_at
FROM companies c
LEFT JOIN company_price_history ph
  ON c.company_id = ph.company_id
GROUP BY c.company_id, c.ticker_symbol, c.current_price
ORDER BY c.company_id;


-- ============================================================================
-- 83. PORTFOLIO VALUE BY COMPANY
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Dashboard / Analytics.

SELECT portfolio_id,
       portfolio_name,
       ticker_symbol,
       current_quantity,
       current_price,
       current_market_value
FROM vw_portfolio_holdings
ORDER BY portfolio_id, current_market_value DESC;


-- ============================================================================
-- 84. TOTAL CURRENT PORTFOLIO VALUE
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Dashboard.
--
-- This adds current market values from the holdings view.

SELECT portfolio_id,
       portfolio_name,
       ROUND(SUM(current_market_value), 2) AS total_current_value
FROM vw_portfolio_holdings
GROUP BY portfolio_id, portfolio_name
ORDER BY portfolio_id;


-- ============================================================================
-- 85. TOTAL UNREALIZED P/L BY PORTFOLIO
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Analytics.

SELECT portfolio_id,
       portfolio_name,
       ROUND(SUM(unrealized_profit_loss), 2) AS total_unrealized_profit_loss
FROM vw_portfolio_holdings
GROUP BY portfolio_id, portfolio_name
ORDER BY portfolio_id;


-- ============================================================================
-- 86. SECTOR ALLOCATION
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Analytics -> sector allocation.

WITH SectorTotals AS (
    SELECT
        t.portfolio_id,
        s.sector_name,
        SUM(
            (
                CASE
                    WHEN t.transaction_type = 'BUY' THEN t.quantity
                    ELSE -t.quantity
                END
            ) * c.current_price
        ) AS sector_market_value
    FROM transactions t
    JOIN companies c
      ON t.company_id = c.company_id
    JOIN sectors s
      ON c.sector_id = s.sector_id
    WHERE t.portfolio_id = 1
    GROUP BY t.portfolio_id, s.sector_name
)
SELECT sector_name,
       ROUND(sector_market_value, 2) AS sector_market_value,
       ROUND(
           sector_market_value
           / NULLIF(SUM(sector_market_value) OVER(), 0)
           * 100,
           2
       ) AS allocation_percentage
FROM SectorTotals
ORDER BY sector_market_value DESC;


-- ============================================================================
-- 87. SIMPLE PORTFOLIO CONCENTRATION / HHI
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Analytics.
--
-- HHI = sum of squared ownership percentages.
-- It measures concentration rather than "good" or "bad" performance.
--
-- Example:
--   Company A = 70%
--   Company B = 30%
--   HHI = 70^2 + 30^2 = 5800
--
-- This query calculates HHI for the selected portfolio.

WITH CompanyValues AS (
    SELECT
        company_id,
        current_market_value
    FROM vw_portfolio_holdings
    WHERE portfolio_id = 1
),
Weights AS (
    SELECT
        company_id,
        current_market_value,
        current_market_value
        / NULLIF(SUM(current_market_value) OVER(), 0) AS weight
    FROM CompanyValues
)
SELECT ROUND(
           SUM(POWER(weight * 100, 2)),
           2
       ) AS hhi
FROM Weights;


-- ============================================================================
-- 88. TRANSACTION VALUE BY MONTH
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   After multiple transaction operations.
--
-- Useful for analytics and explaining GROUP BY / TRUNC(date).

SELECT TRUNC(transaction_date, 'MM') AS transaction_month,
       transaction_type,
       ROUND(SUM(quantity * price_per_share), 2) AS total_value
FROM transactions
GROUP BY TRUNC(transaction_date, 'MM'), transaction_type
ORDER BY transaction_month, transaction_type;


-- ============================================================================
-- 89. TOP HOLDINGS
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Dashboard / Analytics.

SELECT ticker_symbol,
       company_name,
       current_quantity,
       current_market_value,
       unrealized_profit_loss
FROM vw_portfolio_holdings
WHERE portfolio_id = 1
ORDER BY current_market_value DESC
FETCH FIRST 5 ROWS ONLY;


-- ============================================================================
-- 90. RECENT ACTIVITY
-- ============================================================================
-- [AFTER SOFTWARE OPERATION]
-- Software operation:
--   Open Activity page.

SELECT *
FROM (
    SELECT
        t.transaction_id AS activity_id,
        'TRANSACTION' AS activity_type,
        t.transaction_type || ' ' ||
        c.ticker_symbol || ' ' ||
        t.quantity || ' shares' AS activity_description,
        t.transaction_date AS activity_time
    FROM transactions t
    JOIN companies c
      ON t.company_id = c.company_id

    UNION ALL

    SELECT
        a.audit_id,
        'AUDIT',
        a.action_type || ' transaction #' ||
        NVL(TO_CHAR(a.transaction_id), 'DELETED') AS activity_description,
        a.action_date
    FROM transaction_audit a
)
ORDER BY activity_time DESC
FETCH FIRST 30 ROWS ONLY;


-- ============================================================================
-- 91. TABLE SIZE / COLUMN INFORMATION FOR VIVA
-- ============================================================================
-- [VIVA]
-- Software operation:
--   None.
--
-- Shows column data types and NULL rules.

SELECT table_name,
       column_id,
       column_name,
       data_type,
       data_length,
       nullable
FROM user_tab_columns
WHERE table_name IN (
    'APP_USERS',
    'SECTORS',
    'COMPANIES',
    'PORTFOLIOS',
    'WATCHLISTS',
    'WATCHLIST_ITEMS',
    'TRANSACTIONS',
    'DIVIDENDS',
    'TRANSACTION_AUDIT',
    'PORTFOLIO_SNAPSHOTS',
    'COMPANY_PRICE_HISTORY',
    'ALERTS',
    'NOTIFICATIONS',
    'PORTFOLIO_GOALS'
)
ORDER BY table_name, column_id;


-- ============================================================================
-- 92. SHOW ALL VIEWS
-- ============================================================================
-- [VIVA]

SELECT view_name
FROM user_views
ORDER BY view_name;


-- ============================================================================
-- 93. SHOW ALL TRIGGERS
-- ============================================================================
-- [VIVA]

SELECT trigger_name,
       table_name,
       triggering_event,
       status
FROM user_triggers
ORDER BY trigger_name;


-- ============================================================================
-- 94. SHOW ALL FUNCTIONS AND PROCEDURES
-- ============================================================================
-- [VIVA]

SELECT object_name,
       object_type,
       status
FROM user_objects
WHERE object_type IN ('FUNCTION', 'PROCEDURE')
ORDER BY object_type, object_name;


-- ============================================================================
-- 95. CHECK INVALID ORACLE OBJECTS
-- ============================================================================
-- [VIVA / BEFORE DEMO]
-- Software operation:
--   None.
--
-- Ideally this returns ZERO rows.
-- If an object is INVALID, investigate before the demonstration.

SELECT object_name,
       object_type,
       status
FROM user_objects
WHERE status <> 'VALID'
ORDER BY object_type, object_name;


-- ============================================================================
-- 96. FINAL DATABASE SANITY CHECK
-- ============================================================================
-- [BEFORE LEAVING THE DATABASE]
-- Software operation:
--   None.
--
-- These are quick checks that should normally produce sensible results.

SELECT
    (SELECT COUNT(*) FROM app_users)              AS users,
    (SELECT COUNT(*) FROM companies)              AS companies,
    (SELECT COUNT(*) FROM portfolios)             AS portfolios,
    (SELECT COUNT(*) FROM transactions)           AS transactions,
    (SELECT COUNT(*) FROM transaction_audit)      AS audit_records,
    (SELECT COUNT(*) FROM dividends)              AS dividends,
    (SELECT COUNT(*) FROM watchlist_items)        AS watchlist_items,
    (SELECT COUNT(*) FROM portfolio_snapshots)    AS snapshots,
    (SELECT COUNT(*) FROM company_price_history)  AS price_history,
    (SELECT COUNT(*) FROM alerts)                 AS alerts,
    (SELECT COUNT(*) FROM notifications)          AS notifications,
    (SELECT COUNT(*) FROM portfolio_goals)        AS goals
FROM dual;


-- ============================================================================
-- 97. FINAL TEACHER DEMO SEQUENCE
-- ============================================================================
-- If the teacher says "Show me the database operation", use this exact flow:
--
--   STEP 1
--   Perform in software:
--       Transactions -> Add BUY
--
--   STEP 2
--   Run:
--       SELECT * FROM transactions ORDER BY transaction_id DESC;
--
--   STEP 3
--   Run:
--       SELECT * FROM transaction_audit ORDER BY audit_id DESC;
--
--   STEP 4
--   Run:
--       SELECT * FROM vw_portfolio_holdings;
--
--   STEP 5
--   Explain:
--       TRANSACTIONS is the source history.
--       The VIEW derives current holdings.
--       The TRIGGER automatically creates audit records.
--
--   STEP 6
--   Perform in software:
--       Attempt SELL greater than available quantity.
--
--   STEP 7
--   Explain:
--       Application validation rejects overselling.
--       SP_RECORD_TRANSACTION also contains database-side oversell logic.
--       CHECK constraints independently enforce valid quantity/price/type.
--
--   STEP 8
--   Run:
--       SELECT fn_get_weighted_avg_price(1,1) FROM dual;
--
--   STEP 9
--   Run:
--       SELECT fn_calculate_unrealized_pl(1,1) FROM dual;
--
--   STEP 10
--   Show:
--       USER_CONSTRAINTS
--       USER_INDEXES
--       USER_SOURCE
--
-- ============================================================================
-- END OF SHARESYNC ORACLE DEMONSTRATION SCRIPT
-- ============================================================================
