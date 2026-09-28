-- ============================================================
-- ShareSync - MySQL Seed Data
-- ============================================================

USE sharesync;


-- ============================================================
-- 1. USERS
-- ============================================================

INSERT INTO app_users (user_id, name, email)
VALUES
    (1, 'Tanvir Anjum', 'tanvir@sharesync.com'),
    (2, 'Rahim Ahmed', 'rahim@sharesync.com'),
    (3, 'Nusrat Jahan', 'nusrat@sharesync.com');


-- ============================================================
-- 2. SECTORS
-- ============================================================

INSERT INTO sectors (sector_id, sector_name, description)
VALUES
    (1, 'Telecommunication', 'Telecommunication and mobile network services'),
    (2, 'Pharmaceuticals', 'Pharmaceutical and healthcare companies'),
    (3, 'Consumer Goods', 'Consumer products and FMCG companies'),
    (4, 'Financial Services', 'Banking, financial and investment services');


-- ============================================================
-- 3. COMPANIES
-- ============================================================

INSERT INTO companies
    (company_id, company_name, ticker_symbol, sector_id, current_price, market_cap)
VALUES
    (1, 'Grameenphone PLC', 'GP', 1, 410.50, 555000000000),
    (2, 'BEXIMCO Pharmaceuticals PLC', 'BEXIMCO', 2, 118.40, 53500000000),
    (3, 'British American Tobacco Bangladesh', 'BATBC', 3, 462.75, 250000000000),
    (4, 'Square Pharmaceuticals PLC', 'SQURPHARMA', 2, 226.80, 200000000000);


-- ============================================================
-- 4. PORTFOLIOS
-- ============================================================

INSERT INTO portfolios
    (portfolio_id, user_id, portfolio_name, description)
VALUES
    (1, 1, 'Main Portfolio', 'Primary investment portfolio'),
    (2, 1, 'Long Term Holdings', 'Long-term investment portfolio'),
    (3, 2, 'Growth Portfolio', 'Portfolio focused on growth companies');


-- ============================================================
-- 5. WATCHLISTS
-- ============================================================

INSERT INTO watchlists
    (watchlist_id, user_id, watchlist_name, description)
VALUES
    (1, 1, 'My Watchlist', 'Companies currently being monitored'),
    (2, 1, 'Dividend Stocks', 'Companies monitored for dividend opportunities'),
    (3, 2, 'Growth Stocks', 'Potential growth investments');


-- ============================================================
-- 6. WATCHLIST ITEMS
-- ============================================================

INSERT INTO watchlist_items
    (watchlist_id, company_id, target_price)
VALUES
    (1, 1, 400.00),
    (1, 2, 110.00),
    (1, 4, 220.00),
    (2, 1, 390.00),
    (2, 3, 450.00),
    (3, 2, 105.00),
    (3, 4, 215.00);


-- ============================================================
-- 7. TRANSACTIONS
-- ============================================================

INSERT INTO transactions
    (transaction_id, portfolio_id, company_id, transaction_type,
     quantity, price_per_share, transaction_date)
VALUES

    -- Tanvir - Main Portfolio
    (1, 1, 1, 'BUY', 100, 380.00, '2026-04-05 10:15:00'),
    (2, 1, 2, 'BUY', 200, 105.00, '2026-04-12 11:30:00'),
    (3, 1, 3, 'BUY', 50, 445.00, '2026-05-03 09:45:00'),
    (4, 1, 1, 'BUY', 50, 395.00, '2026-05-18 10:20:00'),
    (5, 1, 2, 'SELL', 50, 112.00, '2026-06-10 13:15:00'),
    (6, 1, 4, 'BUY', 100, 210.00, '2026-06-22 10:10:00'),

    -- Tanvir - Long Term Holdings
    (7, 2, 4, 'BUY', 150, 198.00, '2026-04-20 11:00:00'),
    (8, 2, 3, 'BUY', 30, 430.00, '2026-05-15 10:30:00'),
    (9, 2, 4, 'BUY', 50, 205.00, '2026-07-08 12:20:00'),

    -- Rahim - Growth Portfolio
    (10, 3, 2, 'BUY', 300, 98.00, '2026-04-15 09:50:00'),
    (11, 3, 4, 'BUY', 100, 202.00, '2026-05-25 11:10:00'),
    (12, 3, 2, 'SELL', 100, 108.00, '2026-07-12 14:00:00');


-- ============================================================
-- 8. DIVIDENDS
-- ============================================================

INSERT INTO dividends
    (dividend_id, company_id, dividend_per_share,
     declaration_date, payment_date)
VALUES
    (1, 1, 10.00, '2026-03-10', '2026-04-05'),
    (2, 3, 20.00, '2026-04-15', '2026-05-10'),
    (3, 4, 5.00, '2026-05-20', '2026-06-15'),
    (4, 2, 3.50, '2026-06-05', '2026-07-01');


-- ============================================================
-- 9. PORTFOLIO SNAPSHOTS
-- ============================================================

INSERT INTO portfolio_snapshots
    (snapshot_id, portfolio_id, snapshot_date, total_value)
VALUES
    (1, 1, '2026-04-30', 132000.00),
    (2, 1, '2026-05-31', 141500.00),
    (3, 1, '2026-06-30', 149800.00),
    (4, 1, '2026-07-31', 158600.00),
    (5, 1, '2026-08-31', 171200.00),
    (6, 1, '2026-09-15', 186450.00),

    (7, 2, '2026-06-30', 82000.00),
    (8, 2, '2026-07-31', 87500.00),
    (9, 2, '2026-08-31', 91200.00);


-- ============================================================
-- Verification
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

SELECT 'portfolio_snapshots', COUNT(*)
FROM portfolio_snapshots;