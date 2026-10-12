-- ============================================================
-- ShareSync - Complete MySQL Database Schema
-- File: 01_schema.sql
-- Description: Authoritative database schema for ShareSync on MySQL 8.x
-- ============================================================

CREATE DATABASE IF NOT EXISTS sharesync CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE sharesync;

-- Drop in reverse dependency order for clean recreation if needed
DROP VIEW IF EXISTS vw_portfolio_holdings;
DROP TABLE IF EXISTS portfolio_goals;
DROP TABLE IF EXISTS notifications;
DROP TABLE IF EXISTS alerts;
DROP TABLE IF EXISTS company_price_history;
DROP TABLE IF EXISTS portfolio_snapshots;
DROP TABLE IF EXISTS transaction_audit;
DROP TABLE IF EXISTS dividends;
DROP TABLE IF EXISTS transactions;
DROP TABLE IF EXISTS watchlist_items;
DROP TABLE IF EXISTS watchlists;
DROP TABLE IF EXISTS portfolios;
DROP TABLE IF EXISTS companies;
DROP TABLE IF EXISTS sectors;
DROP TABLE IF EXISTS app_users;
DROP TABLE IF EXISTS currency_exchange_rates;
DROP TABLE IF EXISTS inflation_rates;
DROP TABLE IF EXISTS cpi_index_observations;
DROP TABLE IF EXISTS benchmark_price_history;

-- ============================================================
-- 1. APP_USERS
-- FEATURE: Authentication and Authorization
-- Purpose: User identity, credentials, roles (INVESTOR, ADMIN), and account states.
-- ============================================================
CREATE TABLE app_users (
    user_id       INT AUTO_INCREMENT,
    name          VARCHAR(100) NOT NULL,
    email         VARCHAR(150) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    role          VARCHAR(20)  NOT NULL DEFAULT 'INVESTOR',
    is_active     TINYINT(1)   NOT NULL DEFAULT 1,
    created_at    TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_app_users
        PRIMARY KEY (user_id),

    CONSTRAINT uq_app_users_email
        UNIQUE (email),

    CONSTRAINT ck_app_users_role
        CHECK (role IN ('INVESTOR', 'ADMIN')),

    CONSTRAINT ck_app_users_active
        CHECK (is_active IN (0, 1))
) ENGINE=InnoDB;

-- ============================================================
-- 2. SECTORS
-- FEATURE: Company Catalog and Search
-- Purpose: Industry sectors classifying Dhaka Stock Exchange listed securities.
-- ============================================================
CREATE TABLE sectors (
    sector_id   INT AUTO_INCREMENT,
    sector_name VARCHAR(100) NOT NULL,
    description VARCHAR(255),

    CONSTRAINT pk_sectors
        PRIMARY KEY (sector_id),

    CONSTRAINT uq_sectors_name
        UNIQUE (sector_name)
) ENGINE=InnoDB;

-- ============================================================
-- 3. COMPANIES (Securities / Multi-Asset)
-- FEATURE: Company Catalog and Search
-- Purpose: Catalog of listed securities, ticker symbols, asset types, and market prices.
-- ============================================================
CREATE TABLE companies (
    company_id    INT AUTO_INCREMENT,
    company_name  VARCHAR(150)   NOT NULL,
    ticker_symbol VARCHAR(20)    NOT NULL,
    sector_id     INT            NOT NULL,
    current_price DECIMAL(14,2)  NOT NULL,
    market_cap    DECIMAL(20,2),
    asset_type    VARCHAR(20)    NOT NULL DEFAULT 'EQUITY',
    currency      VARCHAR(10)    NOT NULL DEFAULT 'BDT',
    is_active     TINYINT(1)     NOT NULL DEFAULT 1,
    created_at    TIMESTAMP      NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_companies
        PRIMARY KEY (company_id),

    CONSTRAINT uq_companies_ticker
        UNIQUE (ticker_symbol),

    CONSTRAINT ck_companies_price
        CHECK (current_price > 0),

    CONSTRAINT ck_companies_market_cap
        CHECK (market_cap IS NULL OR market_cap >= 0),

    CONSTRAINT ck_companies_asset_type
        CHECK (asset_type IN ('EQUITY', 'ETF', 'MUTUAL_FUND', 'CASH')),

    CONSTRAINT ck_companies_currency
        CHECK (currency IN ('BDT', 'USD')),

    CONSTRAINT ck_companies_active
        CHECK (is_active IN (0, 1)),

    CONSTRAINT fk_companies_sector
        FOREIGN KEY (sector_id)
        REFERENCES sectors (sector_id)
        ON DELETE RESTRICT
) ENGINE=InnoDB;

-- ============================================================
-- 4. PORTFOLIOS (Multi-Currency)
-- FEATURE: Portfolio Management
-- Purpose: Investor portfolios, preferred currency, and account ownership isolation.
-- ============================================================
CREATE TABLE portfolios (
    portfolio_id   INT AUTO_INCREMENT,
    user_id        INT          NOT NULL,
    portfolio_name VARCHAR(100) NOT NULL,
    description    VARCHAR(255),
    currency       VARCHAR(10)  NOT NULL DEFAULT 'BDT',
    created_at     TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_portfolios
        PRIMARY KEY (portfolio_id),

    CONSTRAINT uq_portfolios_user_name
        UNIQUE (user_id, portfolio_name),

    CONSTRAINT ck_portfolios_currency
        CHECK (currency IN ('BDT', 'USD')),

    CONSTRAINT fk_portfolios_user
        FOREIGN KEY (user_id)
        REFERENCES app_users (user_id)
        ON DELETE RESTRICT
) ENGINE=InnoDB;

-- ============================================================
-- 5. WATCHLISTS
-- FEATURE: Watchlists
-- Purpose: Custom stock watchlists created by investors.
-- ============================================================
CREATE TABLE watchlists (
    watchlist_id   INT AUTO_INCREMENT,
    user_id        INT          NOT NULL,
    watchlist_name VARCHAR(100) NOT NULL,
    description    VARCHAR(255),
    created_at     TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_watchlists
        PRIMARY KEY (watchlist_id),

    CONSTRAINT uq_watchlists_user_name
        UNIQUE (user_id, watchlist_name),

    CONSTRAINT fk_watchlists_user
        FOREIGN KEY (user_id)
        REFERENCES app_users (user_id)
        ON DELETE RESTRICT
) ENGINE=InnoDB;

-- ============================================================
-- 6. WATCHLIST_ITEMS
-- FEATURE: Watchlists
-- Purpose: Junction table linking watchlists to monitored companies with price targets.
-- ============================================================
CREATE TABLE watchlist_items (
    watchlist_id INT           NOT NULL,
    company_id   INT           NOT NULL,
    target_price DECIMAL(14,2),
    added_at     TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_watchlist_items
        PRIMARY KEY (watchlist_id, company_id),

    CONSTRAINT ck_watchlist_items_target
        CHECK (target_price IS NULL OR target_price > 0),

    CONSTRAINT fk_watchlist_items_watchlist
        FOREIGN KEY (watchlist_id)
        REFERENCES watchlists (watchlist_id)
        ON DELETE CASCADE,

    CONSTRAINT fk_watchlist_items_company
        FOREIGN KEY (company_id)
        REFERENCES companies (company_id)
        ON DELETE RESTRICT
) ENGINE=InnoDB;

-- ============================================================
-- 7. TRANSACTIONS
-- FEATURE: Transactions
-- FEATURE: Realized and Unrealized P/L
-- Purpose: Core financial trade ledger recording BUY and SELL executions, fees, and timestamps.
-- ============================================================
CREATE TABLE transactions (
    transaction_id   INT AUTO_INCREMENT,
    portfolio_id     INT            NOT NULL,
    company_id       INT            NOT NULL,
    transaction_type VARCHAR(10)    NOT NULL,
    quantity         DECIMAL(14,4)  NOT NULL,
    price_per_share  DECIMAL(14,2)  NOT NULL,
    fee              DECIMAL(14,2)  NOT NULL DEFAULT 0.00,
    transaction_date TIMESTAMP      NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_transactions
        PRIMARY KEY (transaction_id),

    CONSTRAINT ck_transactions_type
        CHECK (transaction_type IN ('BUY', 'SELL')),

    CONSTRAINT ck_transactions_quantity
        CHECK (quantity > 0),

    CONSTRAINT ck_transactions_price
        CHECK (price_per_share > 0),

    CONSTRAINT ck_transactions_fee
        CHECK (fee >= 0),

    CONSTRAINT fk_transactions_portfolio
        FOREIGN KEY (portfolio_id)
        REFERENCES portfolios (portfolio_id)
        ON DELETE RESTRICT,

    CONSTRAINT fk_transactions_company
        FOREIGN KEY (company_id)
        REFERENCES companies (company_id)
        ON DELETE RESTRICT
) ENGINE=InnoDB;

-- ============================================================
-- 8. DIVIDENDS
-- FEATURE: Dividend Management
-- Purpose: Corporate cash dividend payouts, declaration dates, and payment records.
-- ============================================================
CREATE TABLE dividends (
    dividend_id        INT AUTO_INCREMENT,
    company_id         INT           NOT NULL,
    dividend_per_share DECIMAL(14,2) NOT NULL,
    declaration_date   DATE          NOT NULL,
    payment_date       DATE          NOT NULL,

    CONSTRAINT pk_dividends
        PRIMARY KEY (dividend_id),

    CONSTRAINT ck_dividends_amount
        CHECK (dividend_per_share > 0),

    CONSTRAINT ck_dividends_dates
        CHECK (payment_date >= declaration_date),

    CONSTRAINT fk_dividends_company
        FOREIGN KEY (company_id)
        REFERENCES companies (company_id)
        ON DELETE RESTRICT
) ENGINE=InnoDB;

-- ============================================================
-- 9. TRANSACTION_AUDIT
-- FEATURE: MySQL Database and Data Integrity
-- Purpose: Complete immutable audit log of transaction mutations created by triggers.
-- ============================================================
CREATE TABLE transaction_audit (
    audit_id       INT AUTO_INCREMENT,
    transaction_id INT,
    action_type    VARCHAR(10)   NOT NULL,
    action_date    TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    changed_by     VARCHAR(100),
    details        VARCHAR(1000),

    CONSTRAINT pk_transaction_audit
        PRIMARY KEY (audit_id),

    CONSTRAINT ck_transaction_audit_action
        CHECK (action_type IN ('INSERT', 'UPDATE', 'DELETE')),

    CONSTRAINT fk_transaction_audit_transaction
        FOREIGN KEY (transaction_id)
        REFERENCES transactions (transaction_id)
        ON DELETE SET NULL
) ENGINE=InnoDB;

-- ============================================================
-- 10. PORTFOLIO_SNAPSHOTS
-- FEATURE: Portfolio Valuation
-- Purpose: Daily point-in-time valuation snapshots for tracking historical equity curves.
-- ============================================================
CREATE TABLE portfolio_snapshots (
    snapshot_id  INT AUTO_INCREMENT,
    portfolio_id INT           NOT NULL,
    snapshot_date DATE         NOT NULL,
    total_value  DECIMAL(20,2) NOT NULL,

    CONSTRAINT pk_portfolio_snapshots
        PRIMARY KEY (snapshot_id),

    CONSTRAINT uq_portfolio_snapshot_date
        UNIQUE (portfolio_id, snapshot_date),

    CONSTRAINT ck_portfolio_snapshot_value
        CHECK (total_value >= 0),

    CONSTRAINT fk_portfolio_snapshots_portfolio
        FOREIGN KEY (portfolio_id)
        REFERENCES portfolios (portfolio_id)
        ON DELETE CASCADE
) ENGINE=InnoDB;

-- ============================================================
-- 11. COMPANY_PRICE_HISTORY (Investment Intelligence Engine)
-- FEATURE: Historical Price Data
-- FEATURE: Investment Intelligence
-- Purpose: Multi-year daily OHLC volume price series supporting quantitative backtesting.
-- ============================================================
CREATE TABLE company_price_history (
    price_history_id INT AUTO_INCREMENT,
    company_id       INT           NOT NULL,
    price            DECIMAL(14,2) NOT NULL,
    open_price       DECIMAL(14,2),
    high_price       DECIMAL(14,2),
    low_price        DECIMAL(14,2),
    volume           BIGINT,
    recorded_at      DATETIME      NOT NULL,
    created_at       TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    trading_date     DATE,
    source           VARCHAR(100),
    source_dataset   VARCHAR(255),
    source_doi       VARCHAR(100),
    import_batch_id  VARCHAR(100),

    CONSTRAINT pk_company_price_history
        PRIMARY KEY (price_history_id),

    CONSTRAINT ck_price_hist_price
        CHECK (price > 0),

    CONSTRAINT ck_price_hist_open
        CHECK (open_price IS NULL OR open_price > 0),

    CONSTRAINT ck_price_hist_high
        CHECK (high_price IS NULL OR high_price > 0),

    CONSTRAINT ck_price_hist_low
        CHECK (low_price IS NULL OR low_price > 0),

    CONSTRAINT ck_price_hist_volume
        CHECK (volume IS NULL OR volume >= 0),

    CONSTRAINT uq_price_hist_comp_trading_date
        UNIQUE (company_id, trading_date),

    CONSTRAINT fk_price_history_company
        FOREIGN KEY (company_id)
        REFERENCES companies (company_id)
        ON DELETE CASCADE
) ENGINE=InnoDB;

-- ============================================================
-- 12. ALERTS
-- FEATURE: Alerts and Notifications
-- Purpose: User threshold triggers for stock prices and portfolio aggregate valuations.
-- ============================================================
CREATE TABLE alerts (
    alert_id        INT AUTO_INCREMENT,
    user_id         INT           NOT NULL,
    company_id      INT,
    portfolio_id    INT,
    alert_type      VARCHAR(30)   NOT NULL,
    threshold_value DECIMAL(14,2) NOT NULL,
    is_active       INT           NOT NULL DEFAULT 1,
    created_at      TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    triggered_at    DATETIME,
    message         VARCHAR(500),

    CONSTRAINT pk_alerts
        PRIMARY KEY (alert_id),

    CONSTRAINT ck_alerts_type
        CHECK (alert_type IN ('PRICE_ABOVE', 'PRICE_BELOW', 'PORTFOLIO_VALUE_ABOVE', 'PORTFOLIO_VALUE_BELOW')),

    CONSTRAINT ck_alerts_threshold
        CHECK (threshold_value > 0),

    CONSTRAINT ck_alerts_is_active
        CHECK (is_active IN (0, 1)),

    CONSTRAINT fk_alerts_user
        FOREIGN KEY (user_id)
        REFERENCES app_users (user_id)
        ON DELETE CASCADE,

    CONSTRAINT fk_alerts_company
        FOREIGN KEY (company_id)
        REFERENCES companies (company_id)
        ON DELETE CASCADE,

    CONSTRAINT fk_alerts_portfolio
        FOREIGN KEY (portfolio_id)
        REFERENCES portfolios (portfolio_id)
        ON DELETE CASCADE
) ENGINE=InnoDB;

-- ============================================================
-- 13. NOTIFICATIONS
-- FEATURE: Alerts and Notifications
-- Purpose: User-facing notification event logs, unread states, and system announcements.
-- ============================================================
CREATE TABLE notifications (
    notification_id     INT AUTO_INCREMENT,
    user_id             INT           NOT NULL,
    notification_type   VARCHAR(30)   NOT NULL,
    title               VARCHAR(150)  NOT NULL,
    message             VARCHAR(1000) NOT NULL,
    is_read             INT           NOT NULL DEFAULT 0,
    created_at          TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    related_entity_type VARCHAR(50),
    related_entity_id   INT,

    CONSTRAINT pk_notifications
        PRIMARY KEY (notification_id),

    CONSTRAINT ck_notifications_read
        CHECK (is_read IN (0, 1)),

    CONSTRAINT fk_notifications_user
        FOREIGN KEY (user_id)
        REFERENCES app_users (user_id)
        ON DELETE CASCADE
) ENGINE=InnoDB;

-- ============================================================
-- 14. PORTFOLIO_GOALS
-- FEATURE: Portfolio Goals
-- Purpose: Goal accumulation targets, target milestones, and investor progress tracking.
-- ============================================================
CREATE TABLE portfolio_goals (
    goal_id      INT AUTO_INCREMENT,
    user_id      INT           NOT NULL,
    portfolio_id INT           NOT NULL,
    goal_type    VARCHAR(40)   NOT NULL,
    target_value DECIMAL(15,2) NOT NULL,
    target_date  DATE,
    title        VARCHAR(150)  NOT NULL,
    description  VARCHAR(500),
    is_active    INT           NOT NULL DEFAULT 1,
    created_at   TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at   DATETIME,

    CONSTRAINT pk_portfolio_goals
        PRIMARY KEY (goal_id),

    CONSTRAINT ck_portfolio_goals_type
        CHECK (goal_type IN ('TARGET_PORTFOLIO_VALUE', 'TARGET_RETURN', 'TARGET_DIVIDEND_INCOME')),

    CONSTRAINT ck_portfolio_goals_target
        CHECK (target_value > 0),

    CONSTRAINT ck_portfolio_goals_active
        CHECK (is_active IN (0, 1)),

    CONSTRAINT fk_portfolio_goals_user
        FOREIGN KEY (user_id)
        REFERENCES app_users (user_id)
        ON DELETE CASCADE,

    CONSTRAINT fk_portfolio_goals_portfolio
        FOREIGN KEY (portfolio_id)
        REFERENCES portfolios (portfolio_id)
        ON DELETE CASCADE
) ENGINE=InnoDB;

-- ============================================================
-- 15. INFLATION_RATES (LF-28 Real Return Calculation)
-- FEATURE: Inflation and Real Return
-- Purpose: Official annual inflation percentages from BBS / BLS series.
-- ============================================================
CREATE TABLE inflation_rates (
    rate_id        INT AUTO_INCREMENT,
    country_code   VARCHAR(10)   NOT NULL,
    year           INT           NOT NULL,
    inflation_rate DECIMAL(6,2)  NOT NULL,
    source         VARCHAR(100)  NOT NULL,
    created_at     TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_inflation_rates
        PRIMARY KEY (rate_id),

    CONSTRAINT uq_inflation_rates_country_year
        UNIQUE (country_code, year),

    CONSTRAINT ck_inflation_rates_year
        CHECK (year >= 1900 AND year <= 2100)
) ENGINE=InnoDB;

-- ============================================================
-- 15b. CPI_INDEX_OBSERVATIONS (Monthly CPI Index Series)
-- FEATURE: Inflation and Real Return
-- Purpose: Official monthly consumer price index levels for granular inflation calculations.
-- ============================================================
CREATE TABLE cpi_index_observations (
    observation_id   INT AUTO_INCREMENT,
    country_code     VARCHAR(10)   NOT NULL,
    observation_date DATE          NOT NULL,
    cpi_index        DECIMAL(8,4)  NOT NULL,
    source           VARCHAR(150)  NOT NULL,
    source_url       VARCHAR(255)  NULL,
    created_at       TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_cpi_index_observations
        PRIMARY KEY (observation_id),

    CONSTRAINT uq_cpi_observations_country_date
        UNIQUE (country_code, observation_date),

    CONSTRAINT ck_cpi_index_positive
        CHECK (cpi_index > 0)
) ENGINE=InnoDB;

CREATE INDEX idx_cpi_observations_lookup
    ON cpi_index_observations (country_code, observation_date);

-- ============================================================
-- 16. CURRENCY_EXCHANGE_RATES (LF-29 Multi-Currency Valuation)
-- FEATURE: Currency Conversion
-- Purpose: Interbank currency exchange rates for multi-asset normalization into BDT.
-- ============================================================
CREATE TABLE currency_exchange_rates (
    rate_id        INT AUTO_INCREMENT,
    from_currency  VARCHAR(10)   NOT NULL,
    to_currency    VARCHAR(10)   NOT NULL,
    exchange_rate  DECIMAL(14,4) NOT NULL,
    effective_date DATE          NOT NULL,
    source         VARCHAR(100)  NOT NULL,
    created_at     TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_currency_exchange_rates
        PRIMARY KEY (rate_id),

    CONSTRAINT uq_currency_exchange_rates
        UNIQUE (from_currency, to_currency, effective_date),

    CONSTRAINT ck_currency_exchange_rates_rate
        CHECK (exchange_rate > 0)
) ENGINE=InnoDB;

-- ============================================================
-- 17. BENCHMARK_PRICE_HISTORY (LF-30 Database-Backed Benchmark History)
-- FEATURE: Benchmark Analysis
-- Purpose: Historical index levels for Dhaka Stock Exchange benchmarks (DSEX, DS30, DSES).
-- ============================================================
CREATE TABLE benchmark_price_history (
    benchmark_price_history_id INT AUTO_INCREMENT,
    benchmark_code             VARCHAR(20)   NOT NULL,
    benchmark_name             VARCHAR(100)  NOT NULL,
    trading_date               DATE          NOT NULL,
    close_value                DECIMAL(14,2) NOT NULL,
    open_value                 DECIMAL(14,2),
    high_value                 DECIMAL(14,2),
    low_value                  DECIMAL(14,2),
    volume                     BIGINT,
    source                     VARCHAR(100)  NOT NULL DEFAULT 'Dhaka Stock Exchange',
    source_dataset             VARCHAR(255),
    source_doi                 VARCHAR(100),
    created_at                 TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_benchmark_price_history
        PRIMARY KEY (benchmark_price_history_id),

    CONSTRAINT uq_bench_code_date
        UNIQUE (benchmark_code, trading_date),

    CONSTRAINT ck_bench_close
        CHECK (close_value > 0),

    CONSTRAINT ck_bench_open
        CHECK (open_value IS NULL OR open_value > 0),

    CONSTRAINT ck_bench_high
        CHECK (high_value IS NULL OR high_value > 0),

    CONSTRAINT ck_bench_low
        CHECK (low_value IS NULL OR low_value > 0),

    CONSTRAINT ck_bench_volume
        CHECK (volume IS NULL OR volume >= 0)
) ENGINE=InnoDB;

-- ============================================================
-- 18. PERFORMANCE & REPORTING INDEXES
-- FEATURE: MySQL Database and Data Integrity
-- Purpose: Strategic secondary indexes optimizing JOINs, time-series range scans, and user lookups.
-- ============================================================
CREATE INDEX idx_transactions_portfolio_date ON transactions (portfolio_id, transaction_date);
CREATE INDEX idx_transactions_company ON transactions (company_id);
CREATE INDEX idx_dividends_company_dates ON dividends (company_id, payment_date);
CREATE INDEX idx_price_hist_comp_date ON company_price_history (company_id, recorded_at);
CREATE INDEX idx_bench_code_date ON benchmark_price_history (benchmark_code, trading_date);
CREATE INDEX idx_alerts_user ON alerts (user_id, is_active);
CREATE INDEX idx_alerts_price_eval ON alerts (is_active, company_id, alert_type);
CREATE INDEX idx_alerts_port_eval ON alerts (is_active, portfolio_id, alert_type);
CREATE INDEX idx_notifications_user ON notifications (user_id, is_read, created_at);
CREATE INDEX idx_notifications_entity ON notifications (user_id, related_entity_type, related_entity_id);
CREATE INDEX idx_portfolio_goals_user ON portfolio_goals (user_id, is_active, created_at);
CREATE INDEX idx_portfolio_goals_portfolio ON portfolio_goals (portfolio_id, is_active);

-- ============================================================
-- 19. VIEW: VW_PORTFOLIO_HOLDINGS
-- DATABASE: View — vw_portfolio_holdings
-- SQL CONCEPT: VIEW
-- FEATURE: Portfolio Valuation
-- FEATURE: Realized and Unrealized P/L
-- HOLDINGS: Current Quantity
-- CALCULATION: Weighted Average Cost
-- CALCULATION: Market Value
-- CALCULATION: Unrealized P/L
-- SQL CONCEPT: GROUP BY
-- SQL CONCEPT: HAVING
-- 
-- Formula:
-- Current Quantity = SUM(BUY) - SUM(SELL)
-- Weighted Average Buy Price = SUM(BUY Qty * BUY Price) / SUM(BUY Qty)
-- Market Value = Current Quantity * Current Market Price
-- Unrealized P/L = Market Value - (Current Quantity * Weighted Average Buy Price)
-- 
-- Filters out fully liquidated positions (HAVING current_quantity > 0).
-- ============================================================
CREATE OR REPLACE VIEW vw_portfolio_holdings AS
SELECT
    t.portfolio_id,
    p.portfolio_name,
    t.company_id,
    c.company_name,
    c.ticker_symbol,
    SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity WHEN t.transaction_type = 'SELL' THEN -t.quantity ELSE 0 END) AS current_quantity,
    c.current_price,
    SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity WHEN t.transaction_type = 'SELL' THEN -t.quantity ELSE 0 END) * c.current_price AS current_market_value,
    CASE
        WHEN SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE 0 END) > 0
        THEN SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity * t.price_per_share ELSE 0 END) / SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE 0 END)
        ELSE 0
    END AS weighted_average_buy_price,
    (
        SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity WHEN t.transaction_type = 'SELL' THEN -t.quantity ELSE 0 END) * c.current_price
    ) - (
        SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity WHEN t.transaction_type = 'SELL' THEN -t.quantity ELSE 0 END) *
        CASE
            WHEN SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE 0 END) > 0
            THEN SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity * t.price_per_share ELSE 0 END) / SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE 0 END)
            ELSE 0
        END
    ) AS unrealized_profit_loss
FROM transactions t
JOIN portfolios p ON t.portfolio_id = p.portfolio_id
JOIN companies c ON t.company_id = c.company_id
GROUP BY t.portfolio_id, p.portfolio_name, t.company_id, c.company_name, c.ticker_symbol, c.current_price
HAVING SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity WHEN t.transaction_type = 'SELL' THEN -t.quantity ELSE 0 END) > 0;

-- ============================================================
-- 17. STAGING TABLES (Historical Data Reconciliation - Phase 2)
-- ============================================================
CREATE TABLE IF NOT EXISTS historical_price_staging (
    staging_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    trading_code VARCHAR(50) NOT NULL,
    company_id INT NULL,
    trading_date DATE NOT NULL,
    open_price DECIMAL(14,2) NULL,
    high_price DECIMAL(14,2) NULL,
    low_price DECIMAL(14,2) NULL,
    close_price DECIMAL(14,2) NOT NULL,
    volume BIGINT NULL,
    source VARCHAR(100) NOT NULL,
    source_dataset VARCHAR(255) NULL,
    source_doi VARCHAR(100) NULL,
    import_batch_id VARCHAR(100) NOT NULL,
    validation_status VARCHAR(50) NOT NULL DEFAULT 'PENDING',
    validation_notes VARCHAR(255) NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_hps_code (trading_code),
    INDEX idx_hps_status (validation_status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS benchmark_price_staging (
    staging_id BIGINT AUTO_INCREMENT PRIMARY KEY,
    benchmark_code VARCHAR(50) NOT NULL,
    trading_date DATE NOT NULL,
    open_value DECIMAL(14,2) NULL,
    high_value DECIMAL(14,2) NULL,
    low_value DECIMAL(14,2) NULL,
    close_value DECIMAL(14,2) NOT NULL,
    volume BIGINT NULL,
    source VARCHAR(100) NOT NULL,
    source_dataset VARCHAR(255) NULL,
    source_doi VARCHAR(100) NULL,
    import_batch_id VARCHAR(100) NOT NULL,
    validation_status VARCHAR(50) NOT NULL DEFAULT 'PENDING',
    validation_notes VARCHAR(255) NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_bps_code (benchmark_code),
    INDEX idx_bps_status (validation_status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;