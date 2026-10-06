-- ============================================================
-- ShareSync - MySQL Demo Data Reset Script
-- File: reset_demo_data.sql
-- Description: Restores pristine demo state for teacher evaluations
-- Usage: mysql -u root -p sharesync < database/mysql/reset_demo_data.sql
-- ============================================================

USE sharesync;

SELECT '>>> Resetting ShareSync demonstration dataset to pristine evaluation baseline...' AS status;

-- Temporarily disable foreign key checks for clean truncation
SET FOREIGN_KEY_CHECKS = 0;

TRUNCATE TABLE transaction_audit;
TRUNCATE TABLE transactions;
TRUNCATE TABLE portfolio_snapshots;
TRUNCATE TABLE watchlist_items;
TRUNCATE TABLE watchlists;
TRUNCATE TABLE portfolios;
TRUNCATE TABLE dividends;
TRUNCATE TABLE alerts;
TRUNCATE TABLE notifications;
TRUNCATE TABLE portfolio_goals;

SET FOREIGN_KEY_CHECKS = 1;

-- Repopulate reference seed data
SOURCE 02_seed.sql;

SELECT '>>> Demo dataset successfully reset! All portfolios, transactions, and watchlists are restored to baseline.' AS status;
