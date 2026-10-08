-- ============================================================
-- ShareSync - MySQL Demo Data Reset Script
-- File: reset_demo_data.sql
-- Description: Restores pristine evaluation baseline for university demo.
--              Fully self-contained: works from any working directory.
--              Preserves canonical market history (company and benchmark series).
-- Usage:
--   mysql -u root -p sharesync < database/mysql/reset_demo_data.sql
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
TRUNCATE TABLE portfolio_goals;
TRUNCATE TABLE notifications;
TRUNCATE TABLE alerts;
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
-- investor@sharesync.com -> Password123#
-- tanvir@sharesync.com   -> Password123#
-- admin@sharesync.com    -> Admin123#
-- ============================================================
INSERT INTO app_users (user_id, name, email, password_hash, role, is_active, created_at)
VALUES
(1, 'Demo Investor',       'investor@sharesync.com', 'ozOJ8GVWyz3VJnrFM+R/zQ==.Bgq78cPlarDa7KPehH1LMC2Up9VdyNbPbLvzw45g6EA=', 'INVESTOR', 1, DATE_SUB(NOW(), INTERVAL 90 DAY)),
(2, 'Tanvir Anjum',        'tanvir@sharesync.com',   'ozOJ8GVWyz3VJnrFM+R/zQ==.Bgq78cPlarDa7KPehH1LMC2Up9VdyNbPbLvzw45g6EA=', 'INVESTOR', 1, DATE_SUB(NOW(), INTERVAL 90 DAY)),
(3, 'System Administrator', 'admin@sharesync.com',    '4ivxEgBzpuLDnOpZvtYPfw==.bPSkAXZlwYp25mGQ8+W9732+X95DUKVmzO0KBTh07ZE=', 'ADMIN',    1, DATE_SUB(NOW(), INTERVAL 120 DAY));

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
(1,  'Grameenphone Ltd.',                        'GP',          1,  242.60, 327570000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(2,  'Square Pharmaceuticals PLC',               'SQUAREPHARMA', 2,  228.40, 202450000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(3,  'British American Tobacco Bangladesh',      'BATBC',       3,  219.70, 118638000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(4,  'BRAC Bank PLC',                            'BRACBANK',    4,   66.00, 105600000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(5,  'Beximco Pharmaceuticals Ltd.',             'BXPHARMA',    2,  142.60,  63600000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(6,  'Renata PLC',                               'RENATA',      2,  445.40,  51200000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(7,  'LafargeHolcim Bangladesh Ltd.',            'LHBL',        7,   71.30,  82800000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(8,  'Walton Hi-Tech Industries PLC',            'WALTONHIL',   7,  332.30, 100650000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(9,  'DSE 30 Shariah Index ETF',                 'DSE30ETF',    4,  105.50,   1500000000.00, 'ETF',         'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(10, 'Grameen One Scheme Two Mutual Fund',       'GRAMEEN1MF',  4,   18.20,   3200000000.00, 'MUTUAL_FUND', 'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(11, 'BDT Liquid Cash Reserve',                  'CASH-BDT',    4,    1.00,   1000000000.00, 'CASH',        'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(12, 'SPDR S&P 500 ETF Trust',                   'SPY',         5,  580.00, 560000000000.00, 'ETF',         'USD', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(13, 'USD Treasury Cash Reserve',                'CASH-USD',    4,    1.00,   1000000000.00, 'CASH',        'USD', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(14, 'Marico Bangladesh Limited',                'MARICO',      3, 2654.30,  83610000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY)),
(15, 'Agricultural Marketing Company Ltd. (Pran)','AMCL(PRAN)',  3,  100.00,    800000000.00, 'EQUITY',      'BDT', 1, DATE_SUB(NOW(), INTERVAL 120 DAY));

-- ============================================================
-- 4. PORTFOLIOS (Multi-Portfolio, Multi-Currency)
-- ============================================================
INSERT INTO portfolios (portfolio_id, user_id, portfolio_name, description, currency, created_at)
VALUES
(1, 1, 'Long Term Growth',            'Blue-chip equities focused on long-term compound capital appreciation', 'BDT', DATE_SUB(NOW(), INTERVAL 75 DAY)),
(2, 1, 'Dividend Income Portfolio',   'High-dividend yield equities for passive recurring cash flows',          'BDT', DATE_SUB(NOW(), INTERVAL 60 DAY)),
(3, 1, 'Tech & High-Growth Portfolio', 'Multi-asset growth and global ETF allocation',                         'BDT', DATE_SUB(NOW(), INTERVAL 45 DAY)),
(4, 2, 'Primary Portfolio',            'Personal diversified equity tracker',                                    'BDT', DATE_SUB(NOW(), INTERVAL 70 DAY));

-- ============================================================
-- 5. WATCHLISTS
-- ============================================================
INSERT INTO watchlists (watchlist_id, user_id, watchlist_name, description, created_at)
VALUES
(1, 1, 'Blue Chip Priorities', 'Top benchmark companies tracked for attractive re-entry valuation', DATE_SUB(NOW(), INTERVAL 70 DAY)),
(2, 1, 'Healthcare & Pharma',  'Pharmaceutical leaders tracked for clinical earnings beats',         DATE_SUB(NOW(), INTERVAL 50 DAY)),
(3, 2, 'Growth & Funds',       'High potential targets and mutual funds',                            DATE_SUB(NOW(), INTERVAL 65 DAY));

-- ============================================================
-- 6. WATCHLIST_ITEMS
-- ============================================================
INSERT INTO watchlist_items (watchlist_id, company_id, target_price, added_at)
VALUES
(1, 1,   240.00, DATE_SUB(NOW(), INTERVAL 60 DAY)),
(1, 3,   215.00, DATE_SUB(NOW(), INTERVAL 55 DAY)),
(1, 4,    62.00, DATE_SUB(NOW(), INTERVAL 40 DAY)),
(1, 9,   100.00, DATE_SUB(NOW(), INTERVAL 35 DAY)),
(2, 2,   220.00, DATE_SUB(NOW(), INTERVAL 45 DAY)),
(2, 5,   138.00, DATE_SUB(NOW(), INTERVAL 30 DAY)),
(2, 6,   430.00, DATE_SUB(NOW(), INTERVAL 25 DAY)),
(3, 1,   238.00, DATE_SUB(NOW(), INTERVAL 50 DAY)),
(3, 10,   17.50, DATE_SUB(NOW(), INTERVAL 40 DAY)),
(3, 14, 2600.00, DATE_SUB(NOW(), INTERVAL 30 DAY));

-- ============================================================
-- 7. TRANSACTIONS
-- ============================================================
INSERT INTO transactions (transaction_id, portfolio_id, company_id, transaction_type, quantity, price_per_share, fee, transaction_date)
VALUES
-- Portfolio 1 (Long Term Growth - User 1): GP, SQUAREPHARMA, BRACBANK, CASH-BDT
(1,  1, 1,  'BUY',   300.0000,  235.00, 15.00, DATE_SUB(NOW(), INTERVAL 70 DAY)),
(2,  1, 1,  'BUY',   200.0000,  238.00, 10.00, DATE_SUB(NOW(), INTERVAL 50 DAY)),
(3,  1, 1,  'SELL',  100.0000,  248.00,  8.00, DATE_SUB(NOW(), INTERVAL 20 DAY)), -- Profitable SELL: Realized Gain +1,172.00
(4,  1, 2,  'BUY',   400.0000,  232.00, 12.00, DATE_SUB(NOW(), INTERVAL 65 DAY)),
(5,  1, 2,  'BUY',   200.0000,  234.00,  0.00, DATE_SUB(NOW(), INTERVAL 45 DAY)), -- Zero-fee BUY
(6,  1, 2,  'SELL',  100.0000,  222.00, 10.00, DATE_SUB(NOW(), INTERVAL 15 DAY)), -- Loss-making SELL: Realized Loss -1,076.67
(7,  1, 4,  'BUY',  1500.0000,   58.50, 20.00, DATE_SUB(NOW(), INTERVAL 60 DAY)),
(8,  1, 4,  'BUY',  1000.0000,   60.00, 15.00, DATE_SUB(NOW(), INTERVAL 30 DAY)),
(9,  1, 11, 'BUY', 25000.0000,    1.00,  0.00, DATE_SUB(NOW(), INTERVAL 60 DAY)), -- BDT Cash Reserve deposit

-- Portfolio 2 (Dividend Income Portfolio - User 1): BATBC, GRAMEEN1MF (Mutual Fund), LHBL
(10, 2, 3,  'BUY',   400.0000,  210.00, 25.00, DATE_SUB(NOW(), INTERVAL 55 DAY)),
(11, 2, 3,  'BUY',   200.0000,  215.00, 15.00, DATE_SUB(NOW(), INTERVAL 25 DAY)),
(12, 2, 10, 'BUY',  5000.0000,   16.50, 20.00, DATE_SUB(NOW(), INTERVAL 45 DAY)), -- Mutual Fund: GRAMEEN1MF
(13, 2, 7,  'BUY',  1200.0000,   68.50, 18.00, DATE_SUB(NOW(), INTERVAL 40 DAY)),

-- Portfolio 3 (Tech & High-Growth Portfolio - User 1): DSE30ETF (ETF), BXPHARMA, WALTONHIL
(14, 3, 9,  'BUY',   800.0000,   98.00, 15.00, DATE_SUB(NOW(), INTERVAL 45 DAY)), -- ETF: DSE30ETF
(15, 3, 5,  'BUY',   500.0000,  138.00, 12.00, DATE_SUB(NOW(), INTERVAL 40 DAY)),
(16, 3, 8,  'BUY',   150.0000,  315.00, 20.00, DATE_SUB(NOW(), INTERVAL 30 DAY)),

-- Portfolio 4 (Primary Portfolio - User 2): GP, MARICO
(17, 4, 1,  'BUY',   200.0000,  235.00, 10.00, DATE_SUB(NOW(), INTERVAL 65 DAY)),
(18, 4, 14, 'BUY',    50.0000, 2500.00, 25.00, DATE_SUB(NOW(), INTERVAL 50 DAY));

-- ============================================================
-- 8. TRANSACTION_AUDIT
-- ============================================================
INSERT INTO transaction_audit (transaction_id, action_type, action_date, changed_by, details)
VALUES
(1,  'INSERT', DATE_SUB(NOW(), INTERVAL 70 DAY), 'investor@sharesync.com', 'BUY 300.0000 shares of GP @ ৳235.00 (Fee: ৳15.00, Net: ৳70,515.00). Total: ৳70,500.00. Initial blue-chip position.'),
(2,  'INSERT', DATE_SUB(NOW(), INTERVAL 50 DAY), 'investor@sharesync.com', 'BUY 200.0000 shares of GP @ ৳238.00 (Fee: ৳10.00, Net: ৳47,610.00). Total: ৳47,600.00. Accumulation on dip.'),
(3,  'INSERT', DATE_SUB(NOW(), INTERVAL 20 DAY), 'investor@sharesync.com', 'SELL 100.0000 shares of GP @ ৳248.00 (Fee: ৳8.00, Net: ৳24,792.00). Total: ৳24,800.00. Profitable rebalance with realized gain ৳1,172.00.'),
(4,  'INSERT', DATE_SUB(NOW(), INTERVAL 65 DAY), 'investor@sharesync.com', 'BUY 400.0000 shares of SQUAREPHARMA @ ৳232.00 (Fee: ৳12.00, Net: ৳92,812.00). Total: ৳92,800.00. Core healthcare holding.'),
(5,  'INSERT', DATE_SUB(NOW(), INTERVAL 45 DAY), 'investor@sharesync.com', 'BUY 200.0000 shares of SQUAREPHARMA @ ৳234.00 (Fee: ৳0.00, Net: ৳46,800.00). Total: ৳46,800.00. Zero-brokerage promo buy.'),
(6,  'INSERT', DATE_SUB(NOW(), INTERVAL 15 DAY), 'investor@sharesync.com', 'SELL 100.0000 shares of SQUAREPHARMA @ ৳222.00 (Fee: ৳10.00, Net: ৳22,190.00). Total: ৳22,200.00. Loss-mitigation rebalance with realized loss -৳1,076.67.'),
(7,  'INSERT', DATE_SUB(NOW(), INTERVAL 60 DAY), 'investor@sharesync.com', 'BUY 1500.0000 shares of BRACBANK @ ৳58.50 (Fee: ৳20.00, Net: ৳87,770.00). Total: ৳87,750.00. Financial sector banking entry.'),
(8,  'INSERT', DATE_SUB(NOW(), INTERVAL 30 DAY), 'investor@sharesync.com', 'BUY 1000.0000 shares of BRACBANK @ ৳60.00 (Fee: ৳15.00, Net: ৳60,015.00). Total: ৳60,000.00. Dividend capture accumulation.'),
(9,  'INSERT', DATE_SUB(NOW(), INTERVAL 60 DAY), 'investor@sharesync.com', 'BUY 25000.0000 shares of CASH-BDT @ ৳1.00 (Fee: ৳0.00, Net: ৳25,000.00). Total: ৳25,000.00. Cash liquidity reserve allocation.'),
(10, 'INSERT', DATE_SUB(NOW(), INTERVAL 55 DAY), 'investor@sharesync.com', 'BUY 400.0000 shares of BATBC @ ৳210.00 (Fee: ৳25.00, Net: ৳84,025.00). Total: ৳84,000.00. Defensive high-dividend holding.'),
(11, 'INSERT', DATE_SUB(NOW(), INTERVAL 25 DAY), 'investor@sharesync.com', 'BUY 200.0000 shares of BATBC @ ৳215.00 (Fee: ৳15.00, Net: ৳43,015.00). Total: ৳43,000.00. Yield enhancement.'),
(12, 'INSERT', DATE_SUB(NOW(), INTERVAL 45 DAY), 'investor@sharesync.com', 'BUY 5000.0000 shares of GRAMEEN1MF @ ৳16.50 (Fee: ৳20.00, Net: ৳82,520.00). Total: ৳82,500.00. Closed-end mutual fund diversification.'),
(13, 'INSERT', DATE_SUB(NOW(), INTERVAL 40 DAY), 'investor@sharesync.com', 'BUY 1200.0000 shares of LHBL @ ৳68.50 (Fee: ৳18.00, Net: ৳82,218.00). Total: ৳82,200.00. Industrial cement holding.'),
(14, 'INSERT', DATE_SUB(NOW(), INTERVAL 45 DAY), 'investor@sharesync.com', 'BUY 800.0000 shares of DSE30ETF @ ৳98.00 (Fee: ৳15.00, Net: ৳78,415.00). Total: ৳78,400.00. Index tracking ETF investment.'),
(15, 'INSERT', DATE_SUB(NOW(), INTERVAL 40 DAY), 'investor@sharesync.com', 'BUY 500.0000 shares of BXPHARMA @ ৳138.00 (Fee: ৳12.00, Net: ৳69,012.00). Total: ৳69,000.00. Pharma exporter investment.'),
(16, 'INSERT', DATE_SUB(NOW(), INTERVAL 30 DAY), 'investor@sharesync.com', 'BUY 150.0000 shares of WALTONHIL @ ৳315.00 (Fee: ৳20.00, Net: ৳47,270.00). Total: ৳47,250.00. Domestic manufacturing leader.'),
(17, 'INSERT', DATE_SUB(NOW(), INTERVAL 65 DAY), 'tanvir@sharesync.com',   'BUY 200.0000 shares of GP @ ৳235.00 (Fee: ৳10.00, Net: ৳47,010.00). Total: ৳47,000.00. Personal portfolio entry.'),
(18, 'INSERT', DATE_SUB(NOW(), INTERVAL 50 DAY), 'tanvir@sharesync.com',   'BUY 50.0000 shares of MARICO @ ৳2500.00 (Fee: ৳25.00, Net: ৳125,025.00). Total: ৳125,000.00. Premium FMCG compounder.');

-- ============================================================
-- 9. DIVIDENDS
-- ============================================================
INSERT INTO dividends (dividend_id, company_id, dividend_per_share, declaration_date, payment_date)
VALUES
(1, 1,   12.50, DATE_SUB(CURDATE(), INTERVAL 60 DAY), DATE_SUB(CURDATE(), INTERVAL 30 DAY)),
(2, 2,    6.00, DATE_SUB(CURDATE(), INTERVAL 75 DAY), DATE_SUB(CURDATE(), INTERVAL 45 DAY)),
(3, 3,   30.00, DATE_SUB(CURDATE(), INTERVAL 90 DAY), DATE_SUB(CURDATE(), INTERVAL 60 DAY)),
(4, 4,    2.50, DATE_SUB(CURDATE(), INTERVAL 45 DAY), DATE_SUB(CURDATE(), INTERVAL 15 DAY)),
(5, 7,    4.00, DATE_SUB(CURDATE(), INTERVAL 50 DAY), DATE_SUB(CURDATE(), INTERVAL 20 DAY)),
(6, 10,   1.50, DATE_SUB(CURDATE(), INTERVAL 40 DAY), DATE_SUB(CURDATE(), INTERVAL 10 DAY)),
(7, 14, 100.00, DATE_SUB(CURDATE(), INTERVAL 80 DAY), DATE_SUB(CURDATE(), INTERVAL 50 DAY));

-- ============================================================
-- 10. PORTFOLIO_SNAPSHOTS
-- ============================================================
INSERT INTO portfolio_snapshots (portfolio_id, snapshot_date, total_value)
VALUES
-- Portfolio 1 (Long Term Growth)
(1, DATE_SUB(CURDATE(), INTERVAL 30 DAY), 382000.00),
(1, DATE_SUB(CURDATE(), INTERVAL 25 DAY), 386500.00),
(1, DATE_SUB(CURDATE(), INTERVAL 20 DAY), 384800.00),
(1, DATE_SUB(CURDATE(), INTERVAL 15 DAY), 391200.00),
(1, DATE_SUB(CURDATE(), INTERVAL 10 DAY), 396800.00),
(1, DATE_SUB(CURDATE(), INTERVAL 5 DAY),  401240.00),
(1, DATE_SUB(CURDATE(), INTERVAL 1 DAY),  401240.00),
-- Portfolio 2 (Dividend Income Portfolio)
(2, DATE_SUB(CURDATE(), INTERVAL 30 DAY), 295000.00),
(2, DATE_SUB(CURDATE(), INTERVAL 20 DAY), 299400.00),
(2, DATE_SUB(CURDATE(), INTERVAL 10 DAY), 304200.00),
(2, DATE_SUB(CURDATE(), INTERVAL 1 DAY),  308380.00),
-- Portfolio 3 (Tech & High-Growth Portfolio)
(3, DATE_SUB(CURDATE(), INTERVAL 30 DAY), 190000.00),
(3, DATE_SUB(CURDATE(), INTERVAL 15 DAY), 198500.00),
(3, DATE_SUB(CURDATE(), INTERVAL 1 DAY),  205545.00);

-- ============================================================
-- 11. ALERTS
-- ============================================================
INSERT INTO alerts (alert_id, user_id, company_id, portfolio_id, alert_type, threshold_value, is_active, created_at, triggered_at, message)
VALUES
(1, 1, 1,    NULL, 'PRICE_ABOVE',            240.00, 0, DATE_SUB(NOW(), INTERVAL 15 DAY), DATE_SUB(NOW(), INTERVAL 2 DAY), 'Grameenphone PLC (GP) crossed above threshold ৳240.00'),
(2, 1, 3,    NULL, 'PRICE_BELOW',            210.00, 1, DATE_SUB(NOW(), INTERVAL 10 DAY), NULL,                             'Alert if BATBC drops below ৳210.00'),
(3, 1, NULL, 1,    'PORTFOLIO_VALUE_ABOVE', 380000.00, 0, DATE_SUB(NOW(), INTERVAL 25 DAY), DATE_SUB(NOW(), INTERVAL 5 DAY), 'Milestone reached: Long Term Growth exceeded ৳380,000'),
(4, 2, 2,    NULL, 'PRICE_ABOVE',            225.00, 0, DATE_SUB(NOW(), INTERVAL 12 DAY), DATE_SUB(NOW(), INTERVAL 3 DAY), 'Square Pharmaceuticals crossed target ৳225.00');

-- ============================================================
-- 12. NOTIFICATIONS
-- ============================================================
INSERT INTO notifications (notification_id, user_id, notification_type, title, message, is_read, created_at, related_entity_type, related_entity_id)
VALUES
(1, 1, 'SYSTEM',        'Welcome to ShareSync',            'Your portfolio and real-time market tracker is fully configured and ready for trading.', 1, DATE_SUB(NOW(), INTERVAL 30 DAY), 'PORTFOLIO', 1),
(2, 1, 'PRICE_ALERT',   'Price Alert: GP Reached Target',  'Grameenphone PLC (GP) crossed your price threshold of ৳240.00 at ৳242.60.',               0, DATE_SUB(NOW(), INTERVAL 2 DAY),  'COMPANY',   1),
(3, 1, 'DIVIDEND',      'Dividend Declared: SQUAREPHARMA', 'Square Pharmaceuticals PLC announced a dividend of ৳6.00 per share with record date approaching.', 0, DATE_SUB(NOW(), INTERVAL 3 DAY),  'COMPANY',   2),
(4, 1, 'GOAL_PROGRESS', 'Goal Milestone: 80% Achieved',    'You have achieved over 80% of your Retirement Milestone 500k goal.',                     1, DATE_SUB(NOW(), INTERVAL 5 DAY),  'GOAL',      1),
(5, 2, 'SYSTEM',        'Welcome to ShareSync',            'Your personal diversified investment portfolio is active.',                               1, DATE_SUB(NOW(), INTERVAL 25 DAY), 'PORTFOLIO', 4);

-- ============================================================
-- 13. PORTFOLIO_GOALS
-- ============================================================
INSERT INTO portfolio_goals (goal_id, user_id, portfolio_id, goal_type, target_value, target_date, title, description, is_active, created_at)
VALUES
(1, 1, 1, 'TARGET_PORTFOLIO_VALUE', 500000.00, DATE_ADD(CURDATE(), INTERVAL 365 DAY), 'Retirement Milestone 500k', 'Target portfolio valuation for long-term compound growth', 1, DATE_SUB(NOW(), INTERVAL 30 DAY)),
(2, 1, 2, 'TARGET_DIVIDEND_INCOME',  35000.00,  DATE_ADD(CURDATE(), INTERVAL 180 DAY), 'Annual Dividend Target',  'Generate ৳35,000 passive annual cash dividends',         1, DATE_SUB(NOW(), INTERVAL 20 DAY)),
(3, 1, 3, 'TARGET_RETURN',           20.00,     DATE_ADD(CURDATE(), INTERVAL 120 DAY), '20% Alpha Return Target', 'Achieve 20% net return in high growth equities and ETFs', 1, DATE_SUB(NOW(), INTERVAL 15 DAY));

-- ============================================================
-- 14. INFLATION_RATES (Historical CPI Inflation Data)
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
-- ============================================================
INSERT INTO currency_exchange_rates (from_currency, to_currency, exchange_rate, effective_date, source)
VALUES
('USD', 'BDT', 121.5000, '2026-01-01', 'Bangladesh Bank Interbank Closing Rate'),
('BDT', 'USD',   0.0082, '2026-01-01', 'Bangladesh Bank Interbank Inverse Rate'),
('BDT', 'BDT',   1.0000, '2026-01-01', 'Statutory Base Currency Parity'),
('USD', 'USD',   1.0000, '2026-01-01', 'Statutory Foreign Currency Parity');

-- ============================================================
-- 16. BENCHMARK_PRICE_HISTORY
-- Preserved canonical benchmark history. Baseline entries inserted if missing.
-- ============================================================
INSERT IGNORE INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES
('DSEX', 'DSE Broad Index (DSEX)', DATE_SUB(CURDATE(), INTERVAL 30 DAY), 6245.00, 6220.00, 6260.00, 6210.00, 65000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSEX', 'DSE Broad Index (DSEX)', DATE_SUB(CURDATE(), INTERVAL 25 DAY), 6320.00, 6250.00, 6335.00, 6240.00, 71000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSEX', 'DSE Broad Index (DSEX)', DATE_SUB(CURDATE(), INTERVAL 20 DAY), 6410.00, 6325.00, 6425.00, 6315.00, 83000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSEX', 'DSE Broad Index (DSEX)', DATE_SUB(CURDATE(), INTERVAL 15 DAY), 6520.00, 6415.00, 6535.00, 6405.00, 78000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSEX', 'DSE Broad Index (DSEX)', DATE_SUB(CURDATE(), INTERVAL 10 DAY), 6650.00, 6530.00, 6665.00, 6520.00, 92000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSEX', 'DSE Broad Index (DSEX)', DATE_SUB(CURDATE(), INTERVAL 5 DAY),  6780.00, 6660.00, 6795.00, 6650.00, 88000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSEX', 'DSE Broad Index (DSEX)', DATE_SUB(CURDATE(), INTERVAL 1 DAY),  6895.50, 6785.00, 6910.00, 6775.00, 95000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DS30', 'DSE 30 Index (DS30)',    DATE_SUB(CURDATE(), INTERVAL 30 DAY), 2180.00, 2170.00, 2190.00, 2165.00, 28000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DS30', 'DSE 30 Index (DS30)',    DATE_SUB(CURDATE(), INTERVAL 25 DAY), 2210.00, 2185.00, 2215.00, 2180.00, 31000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DS30', 'DSE 30 Index (DS30)',    DATE_SUB(CURDATE(), INTERVAL 20 DAY), 2240.00, 2215.00, 2248.00, 2210.00, 35000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DS30', 'DSE 30 Index (DS30)',    DATE_SUB(CURDATE(), INTERVAL 15 DAY), 2280.00, 2245.00, 2285.00, 2240.00, 33000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DS30', 'DSE 30 Index (DS30)',    DATE_SUB(CURDATE(), INTERVAL 10 DAY), 2310.00, 2285.00, 2320.00, 2280.00, 39000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DS30', 'DSE 30 Index (DS30)',    DATE_SUB(CURDATE(), INTERVAL 5 DAY),  2350.00, 2315.00, 2360.00, 2310.00, 42000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DS30', 'DSE 30 Index (DS30)',    DATE_SUB(CURDATE(), INTERVAL 1 DAY),  2385.20, 2355.00, 2390.00, 2350.00, 45000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSES', 'DSE Shariah Index (DSES)', DATE_SUB(CURDATE(), INTERVAL 30 DAY), 1390.00, 1380.00, 1395.00, 1375.00, 18000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSES', 'DSE Shariah Index (DSES)', DATE_SUB(CURDATE(), INTERVAL 25 DAY), 1410.00, 1395.00, 1415.00, 1390.00, 21000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSES', 'DSE Shariah Index (DSES)', DATE_SUB(CURDATE(), INTERVAL 20 DAY), 1430.00, 1415.00, 1435.00, 1410.00, 24000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSES', 'DSE Shariah Index (DSES)', DATE_SUB(CURDATE(), INTERVAL 15 DAY), 1450.00, 1435.00, 1455.00, 1430.00, 22000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSES', 'DSE Shariah Index (DSES)', DATE_SUB(CURDATE(), INTERVAL 10 DAY), 1475.00, 1455.00, 1480.00, 1450.00, 26000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSES', 'DSE Shariah Index (DSES)', DATE_SUB(CURDATE(), INTERVAL 5 DAY),  1490.00, 1478.00, 1495.00, 1475.00, 29000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1'),
('DSES', 'DSE Shariah Index (DSES)', DATE_SUB(CURDATE(), INTERVAL 1 DAY),  1510.80, 1492.00, 1515.00, 1490.00, 31000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

SELECT '>>> Demo dataset successfully reset! All portfolios, transactions, and watchlists are restored to baseline.' AS status;
