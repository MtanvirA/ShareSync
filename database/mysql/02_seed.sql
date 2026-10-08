-- ============================================================
-- ShareSync - Realistic Demonstration Dataset for MySQL
-- File: 02_seed.sql
-- Description: Deterministic, internally consistent evaluation dataset
--              covering all 17 application entities for teacher demo
-- Usage: mysql -u root -p sharesync < database/mysql/02_seed.sql
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
TRUNCATE TABLE dividends;
TRUNCATE TABLE portfolios;
TRUNCATE TABLE companies;
TRUNCATE TABLE sectors;
TRUNCATE TABLE app_users;
TRUNCATE TABLE inflation_rates;
TRUNCATE TABLE currency_exchange_rates;

-- Note: company_price_history and benchmark_price_history are preserved
-- to maintain the validated 48,672 historical company and 5,922 benchmark records.

SET FOREIGN_KEY_CHECKS = 1;

-- ============================================================
-- 1. APP_USERS (Pre-hashed via ShareSync PBKDF2 PasswordHasher)
-- Passwords:
-- investor@sharesync.com -> Password123# (Primary Teacher Demo Account)
-- tanvir@sharesync.com   -> Password123# (Secondary Personal Demo Account)
-- admin@sharesync.com    -> Admin123#    (System Administrator Account)
-- ============================================================
INSERT INTO app_users (user_id, name, email, password_hash, role, is_active, created_at)
VALUES
(1, 'Tanvir Anjum',            'investor@sharesync.com', 'ozOJ8GVWyz3VJnrFM+R/zQ==.Bgq78cPlarDa7KPehH1LMC2Up9VdyNbPbLvzw45g6EA=', 'INVESTOR', 1, DATE_SUB(NOW(), INTERVAL 95 DAY)),
(2, 'Tanvir Anjum (Personal)', 'tanvir@sharesync.com',   'ozOJ8GVWyz3VJnrFM+R/zQ==.Bgq78cPlarDa7KPehH1LMC2Up9VdyNbPbLvzw45g6EA=', 'INVESTOR', 1, DATE_SUB(NOW(), INTERVAL 90 DAY)),
(3, 'System Administrator',     'admin@sharesync.com',    '4ivxEgBzpuLDnOpZvtYPfw==.bPSkAXZlwYp25mGQ8+W9732+X95DUKVmzO0KBTh07ZE=', 'ADMIN',    1, DATE_SUB(NOW(), INTERVAL 120 DAY));

-- ============================================================
-- 2. SECTORS
-- ============================================================
INSERT INTO sectors (sector_id, sector_name, description)
VALUES
(1, 'Telecommunication',       'Mobile networks, telecommunication infrastructure and digital services'),
(2, 'Pharmaceuticals',        'Pharmaceutical manufacturing, clinical research and healthcare products'),
(3, 'Consumer Goods',          'Fast-moving consumer products, tobacco, food and household goods'),
(4, 'Financial Services',      'Commercial banks, non-banking financial institutions and insurance'),
(5, 'Technology',              'Software engineering, digital solutions and IT hardware systems'),
(6, 'Energy & Power',          'Power generation, natural gas distribution and clean energy solutions'),
(7, 'Engineering & Industrial', 'Heavy manufacturing, electronics assembly and construction materials');

-- ============================================================
-- 3. COMPANIES (Canonical Real DSE Universe & Multi-Asset Instruments)
-- ============================================================
INSERT INTO companies (company_id, company_name, ticker_symbol, sector_id, current_price, market_cap, asset_type, currency, is_active, created_at)
VALUES
(1,  'Grameenphone Ltd.',                        'GP',          1,  242.00, 327570000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(2,  'Square Pharmaceuticals PLC',               'SQUAREPHARMA', 2,  228.40, 202450000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(3,  'British American Tobacco Bangladesh',      'BATBC',       3,  222.60, 118638000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(4,  'BRAC Bank PLC',                            'BRACBANK',    4,   66.40, 105600000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(5,  'Beximco Pharmaceuticals Ltd.',             'BXPHARMA',    2,  144.60,  63600000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(6,  'Renata PLC',                               'RENATA',      2,  449.90,  51200000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(7,  'LafargeHolcim Bangladesh Ltd.',            'LHBL',        7,   71.30,  82800000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(8,  'Walton Hi-Tech Industries PLC',            'WALTONHIL',   7,  331.10, 100650000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(9,  'DSE 30 Shariah Index ETF',                 'DSE30ETF',    4,  105.50,   1500000000.00, 'ETF',         'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(10, 'Grameen One Scheme Two Mutual Fund',       'GRAMEEN1MF',  4,   18.20,   3200000000.00, 'MUTUAL_FUND', 'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(11, 'BDT Liquid Cash Reserve',                  'CASH-BDT',    4,    1.00,   1000000000.00, 'CASH',        'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(12, 'SPDR S&P 500 ETF Trust',                   'SPY',         5,  580.00, 560000000000.00, 'ETF',         'USD', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(13, 'USD Treasury Cash Reserve',                'CASH-USD',    4,    1.00,   1000000000.00, 'CASH',        'USD', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(14, 'Marico Bangladesh Limited',                'MARICO',      3, 2651.00,  83610000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(15, 'Agricultural Marketing Company Ltd. (Pran)','AMCL(PRAN)',  3,  100.00,    800000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY));

-- ============================================================
-- 4. PORTFOLIOS (Multi-Portfolio, Multi-Currency)
-- User 1: 4 distinct portfolios for evaluation
-- User 2: 1 portfolio
-- ============================================================
INSERT INTO portfolios (portfolio_id, user_id, portfolio_name, description, currency, created_at)
VALUES
(1, 1, 'Main Investment Portfolio',     'Core diversified DSE blue-chip holdings and dividend compounders',           'BDT', DATE_SUB(NOW(), INTERVAL 90 DAY)),
(2, 1, 'Long-Term Growth Portfolio',     'High-conviction growth equities across pharma, manufacturing, and consumer', 'BDT', DATE_SUB(NOW(), INTERVAL 80 DAY)),
(3, 1, 'Dividend Income Portfolio',      'High-yield equities generating steady recurring quarterly cash dividends',   'BDT', DATE_SUB(NOW(), INTERVAL 80 DAY)),
(4, 1, 'Global & Multi-Asset Portfolio', 'Balanced allocation across ETFs, mutual funds, cash liquidity, and global',  'BDT', DATE_SUB(NOW(), INTERVAL 75 DAY)),
(5, 2, 'Primary Portfolio',              'Personal diversified equity tracker',                                       'BDT', DATE_SUB(NOW(), INTERVAL 70 DAY));

-- ============================================================
-- 5. WATCHLISTS
-- ============================================================
INSERT INTO watchlists (watchlist_id, user_id, watchlist_name, description, created_at)
VALUES
(1, 1, 'Blue Chip Core Watch',         'Top benchmark companies tracked for attractive re-entry valuation', DATE_SUB(NOW(), INTERVAL 70 DAY)),
(2, 1, 'Growth & Healthcare Targets',  'Pharmaceutical and industrial leaders tracked for clinical growth',  DATE_SUB(NOW(), INTERVAL 60 DAY)),
(3, 1, 'Multi-Asset & Yield Trackers', 'ETFs, mutual funds, and high-yield dividend cash cows',              DATE_SUB(NOW(), INTERVAL 50 DAY)),
(4, 2, 'Secondary Watchlist',          'High potential targets and mutual funds',                            DATE_SUB(NOW(), INTERVAL 65 DAY));

-- ============================================================
-- 6. WATCHLIST_ITEMS
-- ============================================================
INSERT INTO watchlist_items (watchlist_id, company_id, target_price, added_at)
VALUES
-- Watchlist 1 (Blue Chip Core Watch)
(1, 1,   240.00, DATE_SUB(NOW(), INTERVAL 65 DAY)),
(1, 2,   225.00, DATE_SUB(NOW(), INTERVAL 60 DAY)),
(1, 3,   218.00, DATE_SUB(NOW(), INTERVAL 55 DAY)),
(1, 4,    64.00, DATE_SUB(NOW(), INTERVAL 50 DAY)),
(1, 14, 2600.00, DATE_SUB(NOW(), INTERVAL 45 DAY)),
-- Watchlist 2 (Growth & Healthcare Targets)
(2, 5,   140.00, DATE_SUB(NOW(), INTERVAL 55 DAY)),
(2, 6,   440.00, DATE_SUB(NOW(), INTERVAL 50 DAY)),
(2, 7,    69.00, DATE_SUB(NOW(), INTERVAL 45 DAY)),
(2, 8,   325.00, DATE_SUB(NOW(), INTERVAL 40 DAY)),
(2, 15,   98.00, DATE_SUB(NOW(), INTERVAL 35 DAY)),
-- Watchlist 3 (Multi-Asset & Yield Trackers)
(3, 9,   102.00, DATE_SUB(NOW(), INTERVAL 45 DAY)),
(3, 10,   17.50, DATE_SUB(NOW(), INTERVAL 40 DAY)),
(3, 12,  570.00, DATE_SUB(NOW(), INTERVAL 35 DAY)),
(3, 3,   220.00, DATE_SUB(NOW(), INTERVAL 30 DAY)),
-- Watchlist 4 (User 2)
(4, 1,   238.00, DATE_SUB(NOW(), INTERVAL 50 DAY)),
(4, 14, 2580.00, DATE_SUB(NOW(), INTERVAL 40 DAY));

-- ============================================================
-- 7. TRANSACTIONS
-- Comprehensive, realistic trade ledger across 69 transactions
-- Demonstrates: Multiple BUY tranches, Partial SELL, Profitable SELL,
--               Loss-making SELL, Zero-fee trade, Multi-Asset, Multi-Currency.
-- Strict business rule adherence: every SELL qty <= accumulated BUY qty.
-- ============================================================
INSERT INTO transactions (transaction_id, portfolio_id, company_id, transaction_type, quantity, price_per_share, fee, transaction_date)
VALUES
-- ------------------------------------------------------------
-- PORTFOLIO 1: Main Investment Portfolio (User 1)
-- ------------------------------------------------------------
-- GP (Company 1)
(1,  1, 1,  'BUY',   400.0000,  230.00, 20.00, DATE_SUB(NOW(), INTERVAL 85 DAY)),
(2,  1, 1,  'BUY',   300.0000,  235.00, 15.00, DATE_SUB(NOW(), INTERVAL 70 DAY)),
(3,  1, 1,  'SELL',  150.0000,  245.00, 12.00, DATE_SUB(NOW(), INTERVAL 50 DAY)), -- Profitable SELL: Gain +৳1,917.00
(4,  1, 1,  'BUY',   200.0000,  238.00, 10.00, DATE_SUB(NOW(), INTERVAL 35 DAY)),
(5,  1, 1,  'SELL',  100.0000,  248.00,  8.00, DATE_SUB(NOW(), INTERVAL 15 DAY)), -- Profitable SELL: Gain +৳1,417.00
-- SQUAREPHARMA (Company 2)
(6,  1, 2,  'BUY',   500.0000,  225.00, 25.00, DATE_SUB(NOW(), INTERVAL 80 DAY)),
(7,  1, 2,  'BUY',   300.0000,  232.00, 15.00, DATE_SUB(NOW(), INTERVAL 65 DAY)),
(8,  1, 2,  'SELL',  150.0000,  220.00, 10.00, DATE_SUB(NOW(), INTERVAL 45 DAY)), -- Loss-making SELL: Loss -৳1,154.50
(9,  1, 2,  'BUY',   200.0000,  224.00,  0.00, DATE_SUB(NOW(), INTERVAL 30 DAY)), -- Zero-fee BUY promo
(10, 1, 2,  'SELL',  100.0000,  234.00, 10.00, DATE_SUB(NOW(), INTERVAL 12 DAY)), -- Profitable SELL: Gain +৳716.00
-- BRACBANK (Company 4)
(11, 1, 4,  'BUY',  1500.0000,   58.00, 20.00, DATE_SUB(NOW(), INTERVAL 75 DAY)),
(12, 1, 4,  'BUY',  1000.0000,   60.00, 15.00, DATE_SUB(NOW(), INTERVAL 55 DAY)),
(13, 1, 4,  'SELL',  500.0000,   64.00, 10.00, DATE_SUB(NOW(), INTERVAL 38 DAY)), -- Profitable SELL: Gain +৳2,590.00
(14, 1, 4,  'BUY',  1000.0000,   62.50, 15.00, DATE_SUB(NOW(), INTERVAL 22 DAY)),
(15, 1, 4,  'SELL',  400.0000,   68.00, 10.00, DATE_SUB(NOW(), INTERVAL 10 DAY)), -- Profitable SELL: Gain +৳3,247.00
-- BATBC (Company 3)
(16, 1, 3,  'BUY',   300.0000,  212.00, 18.00, DATE_SUB(NOW(), INTERVAL 70 DAY)),
(17, 1, 3,  'BUY',   200.0000,  218.00, 12.00, DATE_SUB(NOW(), INTERVAL 48 DAY)),
(18, 1, 3,  'SELL',  100.0000,  225.00,  8.00, DATE_SUB(NOW(), INTERVAL 25 DAY)), -- Profitable SELL: Gain +৳1,052.00
-- MARICO (Company 14)
(19, 1, 14, 'BUY',    40.0000, 2520.00, 25.00, DATE_SUB(NOW(), INTERVAL 60 DAY)),
(20, 1, 14, 'BUY',    20.0000, 2580.00, 15.00, DATE_SUB(NOW(), INTERVAL 40 DAY)),
(21, 1, 14, 'SELL',   10.0000, 2680.00, 12.00, DATE_SUB(NOW(), INTERVAL 18 DAY)), -- Profitable SELL: Gain +৳1,388.00
-- CASH-BDT (Company 11)
(22, 1, 11, 'BUY', 50000.0000,    1.00,  0.00, DATE_SUB(NOW(), INTERVAL 80 DAY)), -- Cash deposit
(23, 1, 11, 'BUY', 25000.0000,    1.00,  0.00, DATE_SUB(NOW(), INTERVAL 30 DAY)), -- Cash deposit

-- ------------------------------------------------------------
-- PORTFOLIO 2: Long-Term Growth Portfolio (User 1)
-- ------------------------------------------------------------
-- RENATA (Company 6)
(24, 2, 6,  'BUY',   200.0000,  435.00, 22.00, DATE_SUB(NOW(), INTERVAL 78 DAY)),
(25, 2, 6,  'BUY',   150.0000,  440.00, 18.00, DATE_SUB(NOW(), INTERVAL 58 DAY)),
(26, 2, 6,  'SELL',   50.0000,  430.00,  8.00, DATE_SUB(NOW(), INTERVAL 32 DAY)), -- Loss-making SELL: Loss -৳365.00
(27, 2, 6,  'BUY',   100.0000,  445.00, 12.00, DATE_SUB(NOW(), INTERVAL 14 DAY)),
-- WALTONHIL (Company 8)
(28, 2, 8,  'BUY',   250.0000,  320.00, 20.00, DATE_SUB(NOW(), INTERVAL 72 DAY)),
(29, 2, 8,  'BUY',   150.0000,  325.00, 15.00, DATE_SUB(NOW(), INTERVAL 52 DAY)),
(30, 2, 8,  'SELL',   80.0000,  338.00, 10.00, DATE_SUB(NOW(), INTERVAL 28 DAY)), -- Profitable SELL: Gain +৳1,279.60
(31, 2, 8,  'BUY',    80.0000,  328.00, 10.00, DATE_SUB(NOW(), INTERVAL 16 DAY)),
-- LHBL (Company 7)
(32, 2, 7,  'BUY',  1500.0000,   67.00, 20.00, DATE_SUB(NOW(), INTERVAL 68 DAY)),
(33, 2, 7,  'BUY',  1000.0000,   69.00, 15.00, DATE_SUB(NOW(), INTERVAL 46 DAY)),
(34, 2, 7,  'SELL',  500.0000,   73.00, 10.00, DATE_SUB(NOW(), INTERVAL 26 DAY)), -- Profitable SELL: Gain +৳2,590.00
-- BXPHARMA (Company 5)
(35, 2, 5,  'BUY',   600.0000,  138.00, 18.00, DATE_SUB(NOW(), INTERVAL 64 DAY)),
(36, 2, 5,  'BUY',   400.0000,  141.00, 12.00, DATE_SUB(NOW(), INTERVAL 42 DAY)),
(37, 2, 5,  'SELL',  200.0000,  136.00, 10.00, DATE_SUB(NOW(), INTERVAL 20 DAY)), -- Loss-making SELL: Loss -৳650.00
-- AMCL(PRAN) (Company 15)
(38, 2, 15, 'BUY',   500.0000,   95.00, 12.00, DATE_SUB(NOW(), INTERVAL 55 DAY)),
(39, 2, 15, 'BUY',   300.0000,   97.00,  8.00, DATE_SUB(NOW(), INTERVAL 30 DAY)),
(40, 2, 15, 'SELL',  100.0000,  102.00,  5.00, DATE_SUB(NOW(), INTERVAL 10 DAY)), -- Profitable SELL: Gain +৳620.00

-- ------------------------------------------------------------
-- PORTFOLIO 3: Dividend Income Portfolio (User 1)
-- ------------------------------------------------------------
-- BATBC (Company 3)
(41, 3, 3,  'BUY',   500.0000,  210.00, 25.00, DATE_SUB(NOW(), INTERVAL 76 DAY)),
(42, 3, 3,  'BUY',   300.0000,  214.00, 18.00, DATE_SUB(NOW(), INTERVAL 54 DAY)),
(43, 3, 3,  'SELL',  100.0000,  224.00, 10.00, DATE_SUB(NOW(), INTERVAL 34 DAY)), -- Profitable SELL: Gain +৳1,240.00
-- GP (Company 1)
(44, 3, 1,  'BUY',   400.0000,  232.00, 20.00, DATE_SUB(NOW(), INTERVAL 74 DAY)),
(45, 3, 1,  'BUY',   200.0000,  236.00, 12.00, DATE_SUB(NOW(), INTERVAL 44 DAY)),
(46, 3, 1,  'SELL',  100.0000,  244.00,  8.00, DATE_SUB(NOW(), INTERVAL 18 DAY)), -- Profitable SELL: Gain +৳1,059.00
-- MARICO (Company 14)
(47, 3, 14, 'BUY',    30.0000, 2500.00, 20.00, DATE_SUB(NOW(), INTERVAL 66 DAY)),
(48, 3, 14, 'BUY',    20.0000, 2550.00, 15.00, DATE_SUB(NOW(), INTERVAL 36 DAY)),
(49, 3, 14, 'SELL',   10.0000, 2510.00, 10.00, DATE_SUB(NOW(), INTERVAL 14 DAY)), -- Loss-making SELL: Loss -৳110.00
-- SQUAREPHARMA (Company 2)
(50, 3, 2,  'BUY',   400.0000,  226.00, 20.00, DATE_SUB(NOW(), INTERVAL 62 DAY)),
(51, 3, 2,  'BUY',   200.0000,  228.00, 10.00, DATE_SUB(NOW(), INTERVAL 40 DAY)),
(52, 3, 2,  'SELL',  100.0000,  232.00,  8.00, DATE_SUB(NOW(), INTERVAL 12 DAY)), -- Profitable SELL: Gain +৳525.00
-- BRACBANK (Company 4)
(53, 3, 4,  'BUY',  1200.0000,   59.00, 15.00, DATE_SUB(NOW(), INTERVAL 56 DAY)),
(54, 3, 4,  'BUY',   800.0000,   61.00, 10.00, DATE_SUB(NOW(), INTERVAL 24 DAY)),

-- ------------------------------------------------------------
-- PORTFOLIO 4: Global & Multi-Asset Portfolio (User 1)
-- ------------------------------------------------------------
-- DSE30ETF (Company 9, ETF in BDT)
(55, 4, 9,  'BUY',  1000.0000,   98.00, 20.00, DATE_SUB(NOW(), INTERVAL 72 DAY)),
(56, 4, 9,  'BUY',   600.0000,  101.00, 15.00, DATE_SUB(NOW(), INTERVAL 48 DAY)),
(57, 4, 9,  'SELL',  300.0000,  107.00, 10.00, DATE_SUB(NOW(), INTERVAL 22 DAY)), -- Profitable SELL: Gain +৳2,351.00
-- GRAMEEN1MF (Company 10, MUTUAL_FUND in BDT)
(58, 4, 10, 'BUY',  6000.0000,   16.50, 25.00, DATE_SUB(NOW(), INTERVAL 70 DAY)),
(59, 4, 10, 'BUY',  4000.0000,   17.00, 18.00, DATE_SUB(NOW(), INTERVAL 45 DAY)),
(60, 4, 10, 'SELL', 2000.0000,   18.50, 12.00, DATE_SUB(NOW(), INTERVAL 20 DAY)), -- Profitable SELL: Gain +৳3,588.00
-- CASH-BDT (Company 11, CASH in BDT)
(61, 4, 11, 'BUY', 40000.0000,    1.00,  0.00, DATE_SUB(NOW(), INTERVAL 65 DAY)), -- Cash deposit
-- SPY (Company 12, ETF in USD)
(62, 4, 12, 'BUY',    20.0000,  540.00,  5.00, DATE_SUB(NOW(), INTERVAL 60 DAY)),
(63, 4, 12, 'BUY',    15.0000,  560.00,  5.00, DATE_SUB(NOW(), INTERVAL 35 DAY)),
(64, 4, 12, 'SELL',    5.0000,  585.00,  3.00, DATE_SUB(NOW(), INTERVAL 15 DAY)), -- Profitable SELL: Gain +$179.15
-- CASH-USD (Company 13, CASH in USD)
(65, 4, 13, 'BUY',  3000.0000,    1.00,  0.00, DATE_SUB(NOW(), INTERVAL 55 DAY)), -- Cash deposit

-- ------------------------------------------------------------
-- PORTFOLIO 5: Primary Portfolio (User 2 - tanvir@sharesync.com)
-- ------------------------------------------------------------
(66, 5, 1,  'BUY',   200.0000,  235.00, 10.00, DATE_SUB(NOW(), INTERVAL 65 DAY)),
(67, 5, 14, 'BUY',    50.0000, 2500.00, 25.00, DATE_SUB(NOW(), INTERVAL 50 DAY)),
(68, 5, 4,  'BUY',   500.0000,   60.00, 10.00, DATE_SUB(NOW(), INTERVAL 30 DAY)),
(69, 5, 4,  'SELL',  100.0000,   65.00,  5.00, DATE_SUB(NOW(), INTERVAL 15 DAY));

-- ============================================================
-- 8. TRANSACTION_AUDIT (Audit Trail Verification)
-- If database audit triggers (trg_transactions_*) are active,
-- they already recorded INSERT audit logs for all 69 transactions.
-- To ensure full audit demonstration across INSERT, UPDATE, and DELETE:
-- 1. Perform an UPDATE on tx #1 to trigger trg_transactions_after_update.
-- 2. Insert and DELETE a draft order to trigger trg_transactions_after_delete.
-- 3. If triggers were inactive, backfill canonical audit entries.
-- ============================================================
-- Exercise UPDATE trigger
UPDATE transactions SET fee = 22.00 WHERE transaction_id = 1;
UPDATE transactions SET fee = 20.00 WHERE transaction_id = 1;

-- Exercise DELETE trigger
INSERT INTO transactions (portfolio_id, company_id, transaction_type, quantity, price_per_share, fee, transaction_date)
VALUES (1, 1, 'BUY', 10, 230.00, 2.00, DATE_SUB(NOW(), INTERVAL 83 DAY));
DELETE FROM transactions WHERE transaction_id = LAST_INSERT_ID();

-- Fallback check: if triggers were not created yet, populate audit baseline
INSERT INTO transaction_audit (transaction_id, action_type, action_date, changed_by, details)
SELECT 
    t.transaction_id,
    'INSERT',
    t.transaction_date,
    'investor@sharesync.com',
    CONCAT('Trade: ', t.transaction_type, ' ', CAST(t.quantity AS CHAR), ' shares of ', c.ticker_symbol, ' @ price ', CAST(t.price_per_share AS CHAR), ' (Fee: ', CAST(t.fee AS CHAR), ')')
FROM transactions t
JOIN companies c ON t.company_id = c.company_id
WHERE NOT EXISTS (SELECT 1 FROM transaction_audit ta WHERE ta.transaction_id = t.transaction_id);

-- ============================================================
-- 9. DIVIDENDS
-- Realistic corporate dividend history across multiple companies
-- ============================================================
INSERT INTO dividends (dividend_id, company_id, dividend_per_share, declaration_date, payment_date)
VALUES
(1,  1,   12.50, DATE_SUB(CURDATE(), INTERVAL 60 DAY), DATE_SUB(CURDATE(), INTERVAL 30 DAY)),
(2,  2,    6.00, DATE_SUB(CURDATE(), INTERVAL 75 DAY), DATE_SUB(CURDATE(), INTERVAL 45 DAY)),
(3,  3,   30.00, DATE_SUB(CURDATE(), INTERVAL 90 DAY), DATE_SUB(CURDATE(), INTERVAL 60 DAY)),
(4,  4,    2.50, DATE_SUB(CURDATE(), INTERVAL 45 DAY), DATE_SUB(CURDATE(), INTERVAL 15 DAY)),
(5,  7,    4.00, DATE_SUB(CURDATE(), INTERVAL 50 DAY), DATE_SUB(CURDATE(), INTERVAL 20 DAY)),
(6,  10,   1.50, DATE_SUB(CURDATE(), INTERVAL 40 DAY), DATE_SUB(CURDATE(), INTERVAL 10 DAY)),
(7,  14, 100.00, DATE_SUB(CURDATE(), INTERVAL 80 DAY), DATE_SUB(CURDATE(), INTERVAL 50 DAY)),
(8,  6,   14.00, DATE_SUB(CURDATE(), INTERVAL 70 DAY), DATE_SUB(CURDATE(), INTERVAL 40 DAY)),
(9,  5,    4.50, DATE_SUB(CURDATE(), INTERVAL 65 DAY), DATE_SUB(CURDATE(), INTERVAL 35 DAY)),
(10, 15,   3.20, DATE_SUB(CURDATE(), INTERVAL 55 DAY), DATE_SUB(CURDATE(), INTERVAL 25 DAY)),
(11, 3,   15.00, DATE_SUB(CURDATE(), INTERVAL 30 DAY), DATE_SUB(CURDATE(), INTERVAL  5 DAY));

-- ============================================================
-- 10. PORTFOLIO_SNAPSHOTS
-- Realistic valuation snapshots overlapping benchmark trading dates
-- ============================================================
INSERT INTO portfolio_snapshots (portfolio_id, snapshot_date, total_value)
VALUES
-- Portfolio 1 (Main Investment Portfolio)
(1, DATE_SUB(CURDATE(), INTERVAL 60 DAY), 462000.00),
(1, DATE_SUB(CURDATE(), INTERVAL 50 DAY), 498000.00),
(1, DATE_SUB(CURDATE(), INTERVAL 40 DAY), 552000.00),
(1, DATE_SUB(CURDATE(), INTERVAL 30 DAY), 614000.00),
(1, DATE_SUB(CURDATE(), INTERVAL 25 DAY), 628500.00),
(1, DATE_SUB(CURDATE(), INTERVAL 20 DAY), 642000.00),
(1, DATE_SUB(CURDATE(), INTERVAL 15 DAY), 658000.00),
(1, DATE_SUB(CURDATE(), INTERVAL 10 DAY), 675000.00),
(1, DATE_SUB(CURDATE(), INTERVAL 5 DAY),  691500.00),
(1, DATE_SUB(CURDATE(), INTERVAL 1 DAY),  704500.00),

-- Portfolio 2 (Long-Term Growth Portfolio)
(2, DATE_SUB(CURDATE(), INTERVAL 60 DAY), 385000.00),
(2, DATE_SUB(CURDATE(), INTERVAL 50 DAY), 420000.00),
(2, DATE_SUB(CURDATE(), INTERVAL 40 DAY), 485000.00),
(2, DATE_SUB(CURDATE(), INTERVAL 30 DAY), 540000.00),
(2, DATE_SUB(CURDATE(), INTERVAL 25 DAY), 555000.00),
(2, DATE_SUB(CURDATE(), INTERVAL 20 DAY), 572000.00),
(2, DATE_SUB(CURDATE(), INTERVAL 15 DAY), 589000.00),
(2, DATE_SUB(CURDATE(), INTERVAL 10 DAY), 605000.00),
(2, DATE_SUB(CURDATE(), INTERVAL 5 DAY),  622000.00),
(2, DATE_SUB(CURDATE(), INTERVAL 1 DAY),  638000.00),

-- Portfolio 3 (Dividend Income Portfolio)
(3, DATE_SUB(CURDATE(), INTERVAL 60 DAY), 410000.00),
(3, DATE_SUB(CURDATE(), INTERVAL 50 DAY), 445000.00),
(3, DATE_SUB(CURDATE(), INTERVAL 40 DAY), 510000.00),
(3, DATE_SUB(CURDATE(), INTERVAL 30 DAY), 560000.00),
(3, DATE_SUB(CURDATE(), INTERVAL 25 DAY), 575000.00),
(3, DATE_SUB(CURDATE(), INTERVAL 20 DAY), 590000.00),
(3, DATE_SUB(CURDATE(), INTERVAL 15 DAY), 608000.00),
(3, DATE_SUB(CURDATE(), INTERVAL 10 DAY), 620000.00),
(3, DATE_SUB(CURDATE(), INTERVAL 5 DAY),  631000.00),
(3, DATE_SUB(CURDATE(), INTERVAL 1 DAY),  642000.00),

-- Portfolio 4 (Global & Multi-Asset Portfolio)
(4, DATE_SUB(CURDATE(), INTERVAL 60 DAY), 210000.00),
(4, DATE_SUB(CURDATE(), INTERVAL 50 DAY), 235000.00),
(4, DATE_SUB(CURDATE(), INTERVAL 40 DAY), 280000.00),
(4, DATE_SUB(CURDATE(), INTERVAL 30 DAY), 315000.00),
(4, DATE_SUB(CURDATE(), INTERVAL 25 DAY), 328000.00),
(4, DATE_SUB(CURDATE(), INTERVAL 20 DAY), 340000.00),
(4, DATE_SUB(CURDATE(), INTERVAL 15 DAY), 352000.00),
(4, DATE_SUB(CURDATE(), INTERVAL 10 DAY), 361000.00),
(4, DATE_SUB(CURDATE(), INTERVAL 5 DAY),  370000.00),
(4, DATE_SUB(CURDATE(), INTERVAL 1 DAY),  380000.00),

-- Portfolio 5 (Primary Portfolio - User 2)
(5, DATE_SUB(CURDATE(), INTERVAL 30 DAY), 195000.00),
(5, DATE_SUB(CURDATE(), INTERVAL 15 DAY), 205000.00),
(5, DATE_SUB(CURDATE(), INTERVAL 1 DAY),  215000.00);

-- ============================================================
-- 11. ALERTS
-- ============================================================
INSERT INTO alerts (alert_id, user_id, company_id, portfolio_id, alert_type, threshold_value, is_active, created_at, triggered_at, message)
VALUES
(1, 1, 1,    NULL, 'PRICE_ABOVE',            240.00, 0, DATE_SUB(NOW(), INTERVAL 20 DAY), DATE_SUB(NOW(), INTERVAL 2 DAY), 'Grameenphone PLC (GP) crossed above target threshold ৳240.00 at ৳242.00'),
(2, 1, 3,    NULL, 'PRICE_BELOW',            215.00, 1, DATE_SUB(NOW(), INTERVAL 15 DAY), NULL,                             'Dip accumulation alert: BATBC drops below ৳215.00'),
(3, 1, NULL, 1,    'PORTFOLIO_VALUE_ABOVE', 650000.00, 0, DATE_SUB(NOW(), INTERVAL 30 DAY), DATE_SUB(NOW(), INTERVAL 5 DAY), 'Milestone reached: Main Investment Portfolio exceeded ৳650,000'),
(4, 1, 2,    NULL, 'PRICE_ABOVE',            230.00, 1, DATE_SUB(NOW(), INTERVAL 12 DAY), NULL,                             'Alert if Square Pharmaceuticals crosses above ৳230.00'),
(5, 1, 14,   NULL, 'PRICE_ABOVE',           2650.00, 0, DATE_SUB(NOW(), INTERVAL 18 DAY), DATE_SUB(NOW(), INTERVAL 3 DAY), 'Marico Bangladesh crossed target threshold ৳2650.00 at ৳2651.00'),
(6, 1, 4,    NULL, 'PRICE_BELOW',             62.00, 1, DATE_SUB(NOW(), INTERVAL 10 DAY), NULL,                             'Dip-buying opportunity alert: BRAC Bank drops below ৳62.00'),
(7, 1, NULL, 2,    'PORTFOLIO_VALUE_ABOVE', 500000.00, 0, DATE_SUB(NOW(), INTERVAL 25 DAY), DATE_SUB(NOW(), INTERVAL 8 DAY), 'Long-Term Growth Portfolio reached milestone ৳500,000'),
(8, 2, 1,    NULL, 'PRICE_ABOVE',            240.00, 0, DATE_SUB(NOW(), INTERVAL 15 DAY), DATE_SUB(NOW(), INTERVAL 3 DAY), 'Grameenphone PLC crossed threshold ৳240.00');

-- ============================================================
-- 12. NOTIFICATIONS
-- ============================================================
INSERT INTO notifications (notification_id, user_id, notification_type, title, message, is_read, created_at, related_entity_type, related_entity_id)
VALUES
(1,  1, 'SYSTEM',        'Welcome to ShareSync',                     'Your portfolio and real-time market tracker is fully configured and ready for trading.', 1, DATE_SUB(NOW(), INTERVAL 45 DAY), 'PORTFOLIO', 1),
(2,  1, 'PRICE_ALERT',   'Price Alert: GP Reached Target',           'Grameenphone PLC (GP) crossed your price threshold of ৳240.00 at ৳242.00.',               0, DATE_SUB(NOW(), INTERVAL 2 DAY),  'COMPANY',   1),
(3,  1, 'DIVIDEND',      'Dividend Declared: BATBC Interim',         'British American Tobacco Bangladesh announced an interim cash dividend of ৳15.00 per share.', 0, DATE_SUB(NOW(), INTERVAL 3 DAY),  'COMPANY',   3),
(4,  1, 'GOAL_PROGRESS', 'Goal Milestone: 90% Achieved',             'You have achieved over 90% of your Retirement Milestone 750k goal in Main Investment Portfolio.', 1, DATE_SUB(NOW(), INTERVAL 4 DAY),  'GOAL',      1),
(5,  1, 'PRICE_ALERT',   'Price Alert: MARICO Target Reached',       'Marico Bangladesh Limited (MARICO) reached ৳2651.00 crossing threshold ৳2650.00.',       0, DATE_SUB(NOW(), INTERVAL 3 DAY),  'COMPANY',   14),
(6,  1, 'DIVIDEND',      'Dividend Paid: SQUAREPHARMA',              'Dividend payment of ৳6.00 per share has been distributed for Square Pharmaceuticals PLC.',1, DATE_SUB(NOW(), INTERVAL 15 DAY), 'COMPANY',   2),
(7,  1, 'SYSTEM',        'Portfolio Milestone Exceeded',             'Long-Term Growth Portfolio has crossed ৳500,000 in total market valuation.',             1, DATE_SUB(NOW(), INTERVAL 8 DAY),  'PORTFOLIO', 2),
(8,  1, 'SYSTEM',        'Quarterly Market Performance Report',      'Your Q3 comprehensive performance report is ready for review with benchmark comparison.', 0, DATE_SUB(NOW(), INTERVAL 1 DAY),  'REPORT',    1),
(9,  1, 'GOAL_PROGRESS', 'Goal Progress: 71% Dividend Target',       'Passive Annual Dividend Target is currently at 71% completion for Dividend Income Portfolio.', 1, DATE_SUB(NOW(), INTERVAL 10 DAY), 'GOAL',      2),
(10, 1, 'PRICE_ALERT',   'Price Alert: DSE30ETF Outperforming',      'DSE 30 Shariah Index ETF crossed ৳105.00 milestone in Global & Multi-Asset Portfolio.', 1, DATE_SUB(NOW(), INTERVAL 7 DAY),  'COMPANY',   9),
(11, 2, 'SYSTEM',        'Welcome to ShareSync',                     'Your personal diversified investment portfolio is active.',                               1, DATE_SUB(NOW(), INTERVAL 25 DAY), 'PORTFOLIO', 5),
(12, 2, 'PRICE_ALERT',   'Price Alert: GP Milestone',                'Grameenphone PLC crossed threshold ৳240.00.',                                             0, DATE_SUB(NOW(), INTERVAL 3 DAY),  'COMPANY',   1);

-- ============================================================
-- 13. PORTFOLIO_GOALS
-- ============================================================
INSERT INTO portfolio_goals (goal_id, user_id, portfolio_id, goal_type, target_value, target_date, title, description, is_active, created_at)
VALUES
(1, 1, 1, 'TARGET_PORTFOLIO_VALUE', 750000.00, DATE_ADD(CURDATE(), INTERVAL 365 DAY), 'Retirement Milestone 750k',     'Target portfolio valuation for long-term compound capital growth', 1, DATE_SUB(NOW(), INTERVAL 45 DAY)),
(2, 1, 3, 'TARGET_DIVIDEND_INCOME',  45000.00, DATE_ADD(CURDATE(), INTERVAL 180 DAY), 'Passive Annual Dividend Target', 'Generate ৳45,000 passive annual cash dividends from blue-chips',   1, DATE_SUB(NOW(), INTERVAL 30 DAY)),
(3, 1, 2, 'TARGET_RETURN',           22.00,    DATE_ADD(CURDATE(), INTERVAL 120 DAY), '22% Alpha Return Target',        'Achieve 22% net return in high growth equities and manufacturers', 1, DATE_SUB(NOW(), INTERVAL 25 DAY)),
(4, 1, 4, 'TARGET_PORTFOLIO_VALUE', 500000.00, DATE_ADD(CURDATE(), INTERVAL 500 DAY), 'Multi-Asset Expansion Fund',    'Accumulate ৳500,000 across diversified global assets and ETFs',    1, DATE_SUB(NOW(), INTERVAL 20 DAY)),
(5, 2, 5, 'TARGET_PORTFOLIO_VALUE', 300000.00, DATE_ADD(CURDATE(), INTERVAL 300 DAY), 'Personal Savings Milestone',    'Secondary target valuation',                                       1, DATE_SUB(NOW(), INTERVAL 15 DAY));

-- ============================================================
-- 14. INFLATION_RATES (Historical CPI Inflation Data)
-- Source: Bangladesh Bureau of Statistics (BBS) / Bangladesh Bank & US BLS
-- ============================================================
INSERT INTO inflation_rates (country_code, year, inflation_rate, source)
VALUES
('BD', 2020,  5.65, 'Bangladesh Bureau of Statistics (BBS) / Bangladesh Bank'),
('BD', 2021,  5.56, 'Bangladesh Bureau of Statistics (BBS) / Bangladesh Bank'),
('BD', 2022,  6.15, 'Bangladesh Bureau of Statistics (BBS) / Bangladesh Bank'),
('BD', 2023,  9.02, 'Bangladesh Bureau of Statistics (BBS) / Bangladesh Bank'),
('BD', 2024,  9.73, 'Bangladesh Bureau of Statistics (BBS) / Bangladesh Bank'),
('BD', 2025, 10.20, 'Bangladesh Bureau of Statistics (BBS) / Bangladesh Bank (Est.)'),
('US', 2020,  1.23, 'U.S. Bureau of Labor Statistics (BLS)'),
('US', 2021,  4.70, 'U.S. Bureau of Labor Statistics (BLS)'),
('US', 2022,  8.00, 'U.S. Bureau of Labor Statistics (BLS)'),
('US', 2023,  4.12, 'U.S. Bureau of Labor Statistics (BLS)'),
('US', 2024,  2.97, 'U.S. Bureau of Labor Statistics (BLS)'),
('US', 2025,  2.80, 'U.S. Bureau of Labor Statistics (BLS) (Est.)');

-- ============================================================
-- 15. CURRENCY_EXCHANGE_RATES
-- Source: Bangladesh Bank Official Interbank Foreign Exchange Rate
-- ============================================================
INSERT INTO currency_exchange_rates (from_currency, to_currency, exchange_rate, effective_date, source)
VALUES
('USD', 'BDT', 121.5000, '2026-01-01', 'Bangladesh Bank Interbank Closing Rate'),
('BDT', 'USD',   0.0082, '2026-01-01', 'Bangladesh Bank Interbank Inverse Rate'),
('BDT', 'BDT',   1.0000, '2026-01-01', 'Statutory Base Currency Parity'),
('USD', 'USD',   1.0000, '2026-01-01', 'Statutory Foreign Currency Parity');

-- ============================================================
-- 16. BENCHMARK_PRICE_HISTORY (Preservation & Baseline Ingress)
-- Real historical benchmark records (DSEX, DS30, DSES) are preserved.
-- The following rows provide deterministic recent baseline observations.
-- ============================================================
INSERT IGNORE INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES
('DSEX', 'DSE Broad Index (DSEX)', DATE_SUB(CURDATE(), INTERVAL 60 DAY), 6080.00, 6060.00, 6095.00, 6050.00, 58000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSEX', 'DSE Broad Index (DSEX)', DATE_SUB(CURDATE(), INTERVAL 50 DAY), 6140.00, 6120.00, 6155.00, 6110.00, 61000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSEX', 'DSE Broad Index (DSEX)', DATE_SUB(CURDATE(), INTERVAL 40 DAY), 6195.00, 6175.00, 6210.00, 6165.00, 63000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSEX', 'DSE Broad Index (DSEX)', DATE_SUB(CURDATE(), INTERVAL 30 DAY), 6245.00, 6220.00, 6260.00, 6210.00, 65000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSEX', 'DSE Broad Index (DSEX)', DATE_SUB(CURDATE(), INTERVAL 25 DAY), 6320.00, 6250.00, 6335.00, 6240.00, 71000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSEX', 'DSE Broad Index (DSEX)', DATE_SUB(CURDATE(), INTERVAL 20 DAY), 6410.00, 6325.00, 6425.00, 6315.00, 83000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSEX', 'DSE Broad Index (DSEX)', DATE_SUB(CURDATE(), INTERVAL 15 DAY), 6520.00, 6415.00, 6535.00, 6405.00, 78000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSEX', 'DSE Broad Index (DSEX)', DATE_SUB(CURDATE(), INTERVAL 10 DAY), 6650.00, 6530.00, 6665.00, 6520.00, 92000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSEX', 'DSE Broad Index (DSEX)', DATE_SUB(CURDATE(), INTERVAL 5 DAY),  6780.00, 6660.00, 6795.00, 6650.00, 88000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSEX', 'DSE Broad Index (DSEX)', DATE_SUB(CURDATE(), INTERVAL 1 DAY),  6895.50, 6785.00, 6910.00, 6775.00, 95000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DS30', 'DSE 30 Index (DS30)',    DATE_SUB(CURDATE(), INTERVAL 60 DAY), 2120.00, 2110.00, 2130.00, 2105.00, 24000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DS30', 'DSE 30 Index (DS30)',    DATE_SUB(CURDATE(), INTERVAL 50 DAY), 2145.00, 2135.00, 2155.00, 2130.00, 26000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DS30', 'DSE 30 Index (DS30)',    DATE_SUB(CURDATE(), INTERVAL 40 DAY), 2165.00, 2150.00, 2175.00, 2145.00, 27000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DS30', 'DSE 30 Index (DS30)',    DATE_SUB(CURDATE(), INTERVAL 30 DAY), 2180.00, 2170.00, 2190.00, 2165.00, 28000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DS30', 'DSE 30 Index (DS30)',    DATE_SUB(CURDATE(), INTERVAL 25 DAY), 2210.00, 2185.00, 2215.00, 2180.00, 31000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DS30', 'DSE 30 Index (DS30)',    DATE_SUB(CURDATE(), INTERVAL 20 DAY), 2240.00, 2215.00, 2248.00, 2210.00, 35000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DS30', 'DSE 30 Index (DS30)',    DATE_SUB(CURDATE(), INTERVAL 15 DAY), 2280.00, 2245.00, 2285.00, 2240.00, 33000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DS30', 'DSE 30 Index (DS30)',    DATE_SUB(CURDATE(), INTERVAL 10 DAY), 2310.00, 2285.00, 2320.00, 2280.00, 39000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DS30', 'DSE 30 Index (DS30)',    DATE_SUB(CURDATE(), INTERVAL 5 DAY),  2350.00, 2315.00, 2360.00, 2310.00, 42000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DS30', 'DSE 30 Index (DS30)',    DATE_SUB(CURDATE(), INTERVAL 1 DAY),  2385.20, 2355.00, 2390.00, 2350.00, 45000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSES', 'DSE Shariah Index (DSES)', DATE_SUB(CURDATE(), INTERVAL 60 DAY), 1350.00, 1340.00, 1355.00, 1335.00, 15000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSES', 'DSE Shariah Index (DSES)', DATE_SUB(CURDATE(), INTERVAL 50 DAY), 1368.00, 1355.00, 1372.00, 1350.00, 16000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSES', 'DSE Shariah Index (DSES)', DATE_SUB(CURDATE(), INTERVAL 40 DAY), 1380.00, 1370.00, 1385.00, 1365.00, 17000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSES', 'DSE Shariah Index (DSES)', DATE_SUB(CURDATE(), INTERVAL 30 DAY), 1390.00, 1380.00, 1395.00, 1375.00, 18000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSES', 'DSE Shariah Index (DSES)', DATE_SUB(CURDATE(), INTERVAL 25 DAY), 1410.00, 1395.00, 1415.00, 1390.00, 21000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSES', 'DSE Shariah Index (DSES)', DATE_SUB(CURDATE(), INTERVAL 20 DAY), 1430.00, 1415.00, 1435.00, 1410.00, 24000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSES', 'DSE Shariah Index (DSES)', DATE_SUB(CURDATE(), INTERVAL 15 DAY), 1450.00, 1435.00, 1455.00, 1430.00, 22000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSES', 'DSE Shariah Index (DSES)', DATE_SUB(CURDATE(), INTERVAL 10 DAY), 1475.00, 1455.00, 1480.00, 1450.00, 26000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSES', 'DSE Shariah Index (DSES)', DATE_SUB(CURDATE(), INTERVAL 5 DAY),  1490.00, 1478.00, 1495.00, 1475.00, 29000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSES', 'DSE Shariah Index (DSES)', DATE_SUB(CURDATE(), INTERVAL 1 DAY),  1510.80, 1492.00, 1515.00, 1490.00, 31000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');
