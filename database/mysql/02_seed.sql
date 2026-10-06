-- ============================================================
-- ShareSync - Realistic Demonstration Dataset for MySQL
-- File: 02_seed.sql
-- Description: Complete seed data covering all 14 application entities
-- ============================================================

USE sharesync;

-- Disable foreign key checks for clean, repeatable insertion
SET FOREIGN_KEY_CHECKS = 0;

TRUNCATE TABLE transaction_audit;
TRUNCATE TABLE transactions;
TRUNCATE TABLE watchlist_items;
TRUNCATE TABLE watchlists;
TRUNCATE TABLE portfolio_goals;
TRUNCATE TABLE notifications;
TRUNCATE TABLE alerts;
TRUNCATE TABLE portfolio_snapshots;
TRUNCATE TABLE company_price_history;
TRUNCATE TABLE dividends;
TRUNCATE TABLE portfolios;
TRUNCATE TABLE companies;
TRUNCATE TABLE sectors;
TRUNCATE TABLE app_users;

SET FOREIGN_KEY_CHECKS = 1;

-- ============================================================
-- 1. APP_USERS (Password123# and Admin123# pre-hashed via ShareSync PasswordHasher)
-- ============================================================
INSERT INTO app_users (user_id, name, email, password_hash, role, is_active, created_at)
VALUES
(1, 'Demo Investor', 'investor@sharesync.com', 'ozOJ8GVWyz3VJnrFM+R/zQ==.Bgq78cPlarDa7KPehH1LMC2Up9VdyNbPbLvzw45g6EA=', 'INVESTOR', 1, DATE_SUB(NOW(), INTERVAL 90 DAY)),
(2, 'Tanvir Anjum', 'tanvir@sharesync.com', 'ozOJ8GVWyz3VJnrFM+R/zQ==.Bgq78cPlarDa7KPehH1LMC2Up9VdyNbPbLvzw45g6EA=', 'INVESTOR', 1, DATE_SUB(NOW(), INTERVAL 90 DAY)),
(3, 'System Administrator', 'admin@sharesync.com', '4ivxEgBzpuLDnOpZvtYPfw==.bPSkAXZlwYp25mGQ8+W9732+X95DUKVmzO0KBTh07ZE=', 'ADMIN', 1, DATE_SUB(NOW(), INTERVAL 120 DAY));

-- ============================================================
-- 2. SECTORS
-- ============================================================
INSERT INTO sectors (sector_id, sector_name, description)
VALUES
(1, 'Telecommunication', 'Mobile networks, telecommunication infrastructure and digital services'),
(2, 'Pharmaceuticals', 'Pharmaceutical manufacturing, clinical research and healthcare products'),
(3, 'Consumer Goods', 'Fast-moving consumer products, tobacco, food and household goods'),
(4, 'Financial Services', 'Commercial banks, non-banking financial institutions and insurance'),
(5, 'Technology', 'Software engineering, digital solutions and IT hardware systems'),
(6, 'Energy & Power', 'Power generation, natural gas distribution and clean energy solutions'),
(7, 'Engineering & Industrial', 'Heavy manufacturing, electronics assembly and construction materials');

-- ============================================================
-- 3. COMPANIES
-- ============================================================
INSERT INTO companies (company_id, company_name, ticker_symbol, sector_id, current_price, market_cap, is_active, created_at)
VALUES
(1, 'Grameenphone Ltd.', 'GP', 1, 382.50, 516480000000.00, 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(2, 'Square Pharmaceuticals PLC', 'SQUAREPHARMA', 2, 228.40, 202450000000.00, 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(3, 'British American Tobacco Bangladesh', 'BATBC', 3, 465.20, 251200000000.00, 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(4, 'BRAC Bank PLC', 'BRACBANK', 4, 62.75, 96800000000.00, 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(5, 'Beximco Pharmaceuticals Ltd.', 'BXPHARMA', 2, 142.10, 63400000000.00, 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(6, 'Renata PLC', 'RENATA', 2, 785.00, 89100000000.00, 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(7, 'LafargeHolcim Bangladesh Ltd.', 'LHBL', 7, 71.30, 82800000000.00, 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(8, 'Walton Hi-Tech Industries PLC', 'WALTONHIL', 7, 692.50, 209800000000.00, 1, DATE_SUB(NOW(), INTERVAL 120 DAY));

-- ============================================================
-- 4. PORTFOLIOS
-- ============================================================
INSERT INTO portfolios (portfolio_id, user_id, portfolio_name, description, created_at)
VALUES
(1, 1, 'Long Term Growth', 'Blue-chip equities focused on long-term compound capital appreciation', DATE_SUB(NOW(), INTERVAL 75 DAY)),
(2, 1, 'Dividend Income Portfolio', 'High-dividend yield equities for passive recurring cash flows', DATE_SUB(NOW(), INTERVAL 60 DAY)),
(3, 1, 'Tech & High-Growth Portfolio', 'High beta, market leader growth allocations', DATE_SUB(NOW(), INTERVAL 45 DAY)),
(4, 2, 'Primary Portfolio', 'Personal diversified equity tracker', DATE_SUB(NOW(), INTERVAL 70 DAY));

-- ============================================================
-- 5. WATCHLISTS
-- ============================================================
INSERT INTO watchlists (watchlist_id, user_id, watchlist_name, description, created_at)
VALUES
(1, 1, 'Blue Chip Priorities', 'Top benchmark companies tracked for attractive re-entry valuation', DATE_SUB(NOW(), INTERVAL 70 DAY)),
(2, 1, 'Healthcare & Pharma', 'Pharmaceutical leaders tracked for clinical earnings beats', DATE_SUB(NOW(), INTERVAL 50 DAY)),
(3, 2, 'Growth Equities', 'High potential targets', DATE_SUB(NOW(), INTERVAL 65 DAY));

-- ============================================================
-- 6. WATCHLIST_ITEMS
-- ============================================================
INSERT INTO watchlist_items (watchlist_id, company_id, target_price, added_at)
VALUES
(1, 1, 370.00, DATE_SUB(NOW(), INTERVAL 60 DAY)),
(1, 3, 440.00, DATE_SUB(NOW(), INTERVAL 55 DAY)),
(1, 4, 58.00, DATE_SUB(NOW(), INTERVAL 40 DAY)),
(2, 2, 215.00, DATE_SUB(NOW(), INTERVAL 45 DAY)),
(2, 5, 135.00, DATE_SUB(NOW(), INTERVAL 30 DAY)),
(2, 6, 750.00, DATE_SUB(NOW(), INTERVAL 25 DAY)),
(3, 1, 365.00, DATE_SUB(NOW(), INTERVAL 50 DAY)),
(3, 2, 220.00, DATE_SUB(NOW(), INTERVAL 40 DAY));

-- ============================================================
-- 7. TRANSACTIONS
-- ============================================================
INSERT INTO transactions (transaction_id, portfolio_id, company_id, transaction_type, quantity, price_per_share, transaction_date)
VALUES
(1,  1, 1, 'BUY',  300.0000, 365.00, DATE_SUB(NOW(), INTERVAL 70 DAY)),
(2,  1, 1, 'BUY',  200.0000, 372.50, DATE_SUB(NOW(), INTERVAL 50 DAY)),
(3,  1, 1, 'SELL', 100.0000, 385.00, DATE_SUB(NOW(), INTERVAL 20 DAY)),
(4,  1, 2, 'BUY',  400.0000, 218.00, DATE_SUB(NOW(), INTERVAL 65 DAY)),
(5,  1, 2, 'BUY',  250.0000, 222.00, DATE_SUB(NOW(), INTERVAL 35 DAY)),
(6,  1, 4, 'BUY',  1500.0000, 58.50, DATE_SUB(NOW(), INTERVAL 60 DAY)),
(7,  1, 4, 'BUY',  1000.0000, 60.00, DATE_SUB(NOW(), INTERVAL 30 DAY)),
(8,  2, 3, 'BUY',  300.0000, 448.00, DATE_SUB(NOW(), INTERVAL 55 DAY)),
(9,  2, 3, 'BUY',  150.0000, 455.00, DATE_SUB(NOW(), INTERVAL 25 DAY)),
(10, 2, 1, 'BUY',  250.0000, 368.00, DATE_SUB(NOW(), INTERVAL 45 DAY)),
(11, 2, 7, 'BUY',  1200.0000, 68.50, DATE_SUB(NOW(), INTERVAL 40 DAY)),
(12, 3, 5, 'BUY',  500.0000, 138.00, DATE_SUB(NOW(), INTERVAL 40 DAY)),
(13, 3, 8, 'BUY',  150.0000, 680.00, DATE_SUB(NOW(), INTERVAL 30 DAY)),
(14, 4, 1, 'BUY',  200.0000, 370.00, DATE_SUB(NOW(), INTERVAL 65 DAY)),
(15, 4, 2, 'BUY',  300.0000, 220.00, DATE_SUB(NOW(), INTERVAL 50 DAY));

-- ============================================================
-- 8. DIVIDENDS
-- ============================================================
INSERT INTO dividends (dividend_id, company_id, dividend_per_share, declaration_date, payment_date)
VALUES
(1, 1, 12.50, DATE_SUB(CURDATE(), INTERVAL 60 DAY), DATE_SUB(CURDATE(), INTERVAL 30 DAY)),
(2, 2, 6.00,  DATE_SUB(CURDATE(), INTERVAL 75 DAY), DATE_SUB(CURDATE(), INTERVAL 45 DAY)),
(3, 3, 30.00, DATE_SUB(CURDATE(), INTERVAL 90 DAY), DATE_SUB(CURDATE(), INTERVAL 60 DAY)),
(4, 4, 2.50,  DATE_SUB(CURDATE(), INTERVAL 45 DAY), DATE_SUB(CURDATE(), INTERVAL 15 DAY)),
(5, 7, 4.00,  DATE_SUB(CURDATE(), INTERVAL 50 DAY), DATE_SUB(CURDATE(), INTERVAL 20 DAY));

-- ============================================================
-- 9. PORTFOLIO_SNAPSHOTS (Daily progression over past 30 days)
-- ============================================================
INSERT INTO portfolio_snapshots (portfolio_id, snapshot_date, total_value) VALUES
(1, DATE_SUB(CURDATE(), INTERVAL 30 DAY), 348500.00),
(1, DATE_SUB(CURDATE(), INTERVAL 25 DAY), 352100.00),
(1, DATE_SUB(CURDATE(), INTERVAL 20 DAY), 350800.00),
(1, DATE_SUB(CURDATE(), INTERVAL 15 DAY), 356400.00),
(1, DATE_SUB(CURDATE(), INTERVAL 10 DAY), 362900.00),
(1, DATE_SUB(CURDATE(), INTERVAL 5 DAY),  368200.00),
(1, DATE_SUB(CURDATE(), INTERVAL 1 DAY),  374150.00),
(2, DATE_SUB(CURDATE(), INTERVAL 30 DAY), 282000.00),
(2, DATE_SUB(CURDATE(), INTERVAL 20 DAY), 285400.00),
(2, DATE_SUB(CURDATE(), INTERVAL 10 DAY), 291200.00),
(2, DATE_SUB(CURDATE(), INTERVAL 1 DAY),  296500.00),
(3, DATE_SUB(CURDATE(), INTERVAL 30 DAY), 168000.00),
(3, DATE_SUB(CURDATE(), INTERVAL 15 DAY), 171500.00),
(3, DATE_SUB(CURDATE(), INTERVAL 1 DAY),  174925.00);

-- ============================================================
-- 10. COMPANY_PRICE_HISTORY (Historical dataset for Investment Intelligence)
-- ============================================================
INSERT INTO company_price_history (company_id, price, open_price, high_price, low_price, volume, recorded_at, trading_date, source, source_dataset, source_doi, import_batch_id)
VALUES
-- GP (Grameenphone Ltd.)
(1, 355.00, 352.00, 358.00, 350.00, 48000, DATE_SUB(NOW(), INTERVAL 60 DAY), DATE_SUB(CURDATE(), INTERVAL 60 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(1, 358.50, 355.00, 361.00, 354.00, 52000, DATE_SUB(NOW(), INTERVAL 55 DAY), DATE_SUB(CURDATE(), INTERVAL 55 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(1, 362.00, 359.00, 364.50, 357.50, 61000, DATE_SUB(NOW(), INTERVAL 50 DAY), DATE_SUB(CURDATE(), INTERVAL 50 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(1, 366.50, 363.00, 368.00, 361.00, 57000, DATE_SUB(NOW(), INTERVAL 45 DAY), DATE_SUB(CURDATE(), INTERVAL 45 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(1, 370.00, 367.00, 372.00, 365.00, 69000, DATE_SUB(NOW(), INTERVAL 40 DAY), DATE_SUB(CURDATE(), INTERVAL 40 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(1, 368.00, 371.00, 373.00, 366.00, 45000, DATE_SUB(NOW(), INTERVAL 35 DAY), DATE_SUB(CURDATE(), INTERVAL 35 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(1, 372.50, 369.00, 375.00, 368.00, 58000, DATE_SUB(NOW(), INTERVAL 30 DAY), DATE_SUB(CURDATE(), INTERVAL 30 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(1, 376.00, 373.00, 378.00, 371.50, 64000, DATE_SUB(NOW(), INTERVAL 25 DAY), DATE_SUB(CURDATE(), INTERVAL 25 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(1, 374.00, 376.50, 379.00, 372.00, 53000, DATE_SUB(NOW(), INTERVAL 20 DAY), DATE_SUB(CURDATE(), INTERVAL 20 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(1, 378.50, 375.00, 381.00, 374.00, 71000, DATE_SUB(NOW(), INTERVAL 15 DAY), DATE_SUB(CURDATE(), INTERVAL 15 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(1, 380.00, 378.00, 382.50, 377.00, 62000, DATE_SUB(NOW(), INTERVAL 10 DAY), DATE_SUB(CURDATE(), INTERVAL 10 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(1, 381.50, 379.50, 383.00, 378.50, 66000, DATE_SUB(NOW(), INTERVAL 5 DAY),  DATE_SUB(CURDATE(), INTERVAL 5 DAY),  'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(1, 382.50, 381.00, 384.00, 380.00, 59000, DATE_SUB(NOW(), INTERVAL 1 DAY),  DATE_SUB(CURDATE(), INTERVAL 1 DAY),  'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),

-- SQUAREPHARMA
(2, 214.00, 212.00, 216.00, 211.00, 65000, DATE_SUB(NOW(), INTERVAL 60 DAY), DATE_SUB(CURDATE(), INTERVAL 60 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(2, 216.50, 214.50, 218.00, 213.50, 71000, DATE_SUB(NOW(), INTERVAL 50 DAY), DATE_SUB(CURDATE(), INTERVAL 50 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(2, 219.00, 217.00, 221.00, 216.00, 83000, DATE_SUB(NOW(), INTERVAL 40 DAY), DATE_SUB(CURDATE(), INTERVAL 40 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(2, 221.50, 219.00, 223.00, 218.00, 78000, DATE_SUB(NOW(), INTERVAL 30 DAY), DATE_SUB(CURDATE(), INTERVAL 30 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(2, 224.00, 222.00, 225.50, 221.00, 92000, DATE_SUB(NOW(), INTERVAL 20 DAY), DATE_SUB(CURDATE(), INTERVAL 20 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(2, 226.00, 224.00, 227.00, 223.50, 85000, DATE_SUB(NOW(), INTERVAL 10 DAY), DATE_SUB(CURDATE(), INTERVAL 10 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(2, 228.40, 226.00, 229.50, 225.00, 89000, DATE_SUB(NOW(), INTERVAL 1 DAY),  DATE_SUB(CURDATE(), INTERVAL 1 DAY),  'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),

-- BATBC
(3, 442.00, 440.00, 446.00, 438.00, 32000, DATE_SUB(NOW(), INTERVAL 60 DAY), DATE_SUB(CURDATE(), INTERVAL 60 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(3, 448.50, 443.00, 451.00, 442.00, 38000, DATE_SUB(NOW(), INTERVAL 45 DAY), DATE_SUB(CURDATE(), INTERVAL 45 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(3, 455.00, 449.00, 458.00, 448.00, 41000, DATE_SUB(NOW(), INTERVAL 30 DAY), DATE_SUB(CURDATE(), INTERVAL 30 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(3, 461.00, 456.00, 464.00, 454.00, 44000, DATE_SUB(NOW(), INTERVAL 15 DAY), DATE_SUB(CURDATE(), INTERVAL 15 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(3, 465.20, 461.00, 467.50, 460.00, 47000, DATE_SUB(NOW(), INTERVAL 1 DAY),  DATE_SUB(CURDATE(), INTERVAL 1 DAY),  'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),

-- BRACBANK
(4, 56.50, 55.00, 57.50, 54.50, 110000, DATE_SUB(NOW(), INTERVAL 60 DAY), DATE_SUB(CURDATE(), INTERVAL 60 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(4, 58.00, 56.50, 59.00, 56.00, 125000, DATE_SUB(NOW(), INTERVAL 45 DAY), DATE_SUB(CURDATE(), INTERVAL 45 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(4, 60.25, 58.50, 61.00, 58.00, 140000, DATE_SUB(NOW(), INTERVAL 30 DAY), DATE_SUB(CURDATE(), INTERVAL 30 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(4, 61.80, 60.50, 62.50, 60.00, 132000, DATE_SUB(NOW(), INTERVAL 15 DAY), DATE_SUB(CURDATE(), INTERVAL 15 DAY), 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001'),
(4, 62.75, 61.50, 63.20, 61.00, 158000, DATE_SUB(NOW(), INTERVAL 1 DAY),  DATE_SUB(CURDATE(), INTERVAL 1 DAY),  'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1', 'BATCH-INIT-001');

-- ============================================================
-- 11. ALERTS
-- ============================================================
INSERT INTO alerts (alert_id, user_id, company_id, portfolio_id, alert_type, threshold_value, is_active, created_at, triggered_at, message)
VALUES
(1, 1, 1, NULL, 'PRICE_ABOVE', 375.00, 0, DATE_SUB(NOW(), INTERVAL 15 DAY), DATE_SUB(NOW(), INTERVAL 2 DAY), 'Grameenphone PLC (GP) crossed above threshold ৳375.00'),
(2, 1, 3, NULL, 'PRICE_BELOW', 430.00, 1, DATE_SUB(NOW(), INTERVAL 10 DAY), NULL, 'Alert if BATBC drops below ৳430.00'),
(3, 1, NULL, 1, 'PORTFOLIO_VALUE_ABOVE', 350000.00, 0, DATE_SUB(NOW(), INTERVAL 25 DAY), DATE_SUB(NOW(), INTERVAL 10 DAY), 'Milestone reached: Long Term Growth exceeded ৳350,000'),
(4, 2, 2, NULL, 'PRICE_ABOVE', 225.00, 0, DATE_SUB(NOW(), INTERVAL 12 DAY), DATE_SUB(NOW(), INTERVAL 3 DAY), 'Square Pharmaceuticals crossed target ৳225.00');

-- ============================================================
-- 12. NOTIFICATIONS
-- ============================================================
INSERT INTO notifications (notification_id, user_id, notification_type, title, message, is_read, created_at, related_entity_type, related_entity_id)
VALUES
(1, 1, 'SYSTEM', 'Welcome to ShareSync', 'Your portfolio and real-time market tracker is fully configured and ready for trading.', 1, DATE_SUB(NOW(), INTERVAL 30 DAY), 'PORTFOLIO', 1),
(2, 1, 'PRICE_ALERT', 'Price Alert: GP Reached Target', 'Grameenphone PLC (GP) crossed your price threshold of ৳375.00 at ৳382.50.', 0, DATE_SUB(NOW(), INTERVAL 2 DAY), 'COMPANY', 1),
(3, 1, 'DIVIDEND', 'Dividend Declared: SQUAREPHARMA', 'Square Pharmaceuticals PLC announced a dividend of ৳6.00 per share with record date approaching.', 0, DATE_SUB(NOW(), INTERVAL 3 DAY), 'COMPANY', 2),
(4, 1, 'GOAL_PROGRESS', 'Goal Milestone: 50% Achieved', 'You have achieved over 50% of your Retirement Milestone 250k goal.', 1, DATE_SUB(NOW(), INTERVAL 7 DAY), 'GOAL', 1),
(5, 2, 'SYSTEM', 'Welcome to ShareSync', 'Your investment tracker is ready.', 1, DATE_SUB(NOW(), INTERVAL 25 DAY), 'PORTFOLIO', 4);

-- ============================================================
-- 13. PORTFOLIO_GOALS
-- ============================================================
INSERT INTO portfolio_goals (goal_id, user_id, portfolio_id, goal_type, target_value, target_date, title, description, is_active, created_at)
VALUES
(1, 1, 1, 'TARGET_PORTFOLIO_VALUE', 500000.00, DATE_ADD(CURDATE(), INTERVAL 365 DAY), 'Retirement Milestone 500k', 'Target portfolio valuation for long-term compound growth', 1, DATE_SUB(NOW(), INTERVAL 30 DAY)),
(2, 1, 2, 'TARGET_DIVIDEND_INCOME',  35000.00,  DATE_ADD(CURDATE(), INTERVAL 180 DAY), 'Annual Dividend Target',  'Generate ৳35,000 passive annual cash dividends', 1, DATE_SUB(NOW(), INTERVAL 20 DAY)),
(3, 1, 3, 'TARGET_RETURN',           20.00,     DATE_ADD(CURDATE(), INTERVAL 120 DAY), '20% Alpha Return Target', 'Achieve 20% net return in high growth equities', 1, DATE_SUB(NOW(), INTERVAL 15 DAY));

-- ============================================================
-- 14. INITIAL AUDIT TRAIL LOG
-- ============================================================
INSERT INTO transaction_audit (transaction_id, action_type, action_date, changed_by, details)
VALUES
(1, 'INSERT', DATE_SUB(NOW(), INTERVAL 70 DAY), 'investor@sharesync.com', 'Initial BUY order: 300 GP shares @ 365.00'),
(2, 'INSERT', DATE_SUB(NOW(), INTERVAL 50 DAY), 'investor@sharesync.com', 'Accumulation BUY: 200 GP shares @ 372.50'),
(3, 'INSERT', DATE_SUB(NOW(), INTERVAL 20 DAY), 'investor@sharesync.com', 'Profit taking SELL: 100 GP shares @ 385.00'),
(4, 'INSERT', DATE_SUB(NOW(), INTERVAL 65 DAY), 'investor@sharesync.com', 'Initial BUY order: 400 SQUAREPHARMA @ 218.00'),
(NULL, 'DELETE', DATE_SUB(NOW(), INTERVAL 10 DAY), 'investor@sharesync.com', 'Trigger: Deleted test tx #99: BUY 50 shares @ 350.00 (Historical audit record preserved)');