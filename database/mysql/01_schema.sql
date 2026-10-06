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

-- ============================================================
-- 1. APP_USERS
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
-- 3. COMPANIES
-- ============================================================
CREATE TABLE companies (
    company_id    INT AUTO_INCREMENT,
    company_name  VARCHAR(150)   NOT NULL,
    ticker_symbol VARCHAR(20)    NOT NULL,
    sector_id     INT            NOT NULL,
    current_price DECIMAL(14,2)  NOT NULL,
    market_cap    DECIMAL(20,2),
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

    CONSTRAINT ck_companies_active
        CHECK (is_active IN (0, 1)),

    CONSTRAINT fk_companies_sector
        FOREIGN KEY (sector_id)
        REFERENCES sectors (sector_id)
        ON DELETE RESTRICT
) ENGINE=InnoDB;

-- ============================================================
-- 4. PORTFOLIOS
-- ============================================================
CREATE TABLE portfolios (
    portfolio_id   INT AUTO_INCREMENT,
    user_id        INT          NOT NULL,
    portfolio_name VARCHAR(100) NOT NULL,
    description    VARCHAR(255),
    created_at     TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_portfolios
        PRIMARY KEY (portfolio_id),

    CONSTRAINT uq_portfolios_user_name
        UNIQUE (user_id, portfolio_name),

    CONSTRAINT fk_portfolios_user
        FOREIGN KEY (user_id)
        REFERENCES app_users (user_id)
        ON DELETE RESTRICT
) ENGINE=InnoDB;

-- ============================================================
-- 5. WATCHLISTS
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
-- ============================================================
CREATE TABLE transactions (
    transaction_id   INT AUTO_INCREMENT,
    portfolio_id     INT            NOT NULL,
    company_id       INT            NOT NULL,
    transaction_type VARCHAR(10)    NOT NULL,
    quantity         DECIMAL(14,4)  NOT NULL,
    price_per_share  DECIMAL(14,2)  NOT NULL,
    transaction_date TIMESTAMP      NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_transactions
        PRIMARY KEY (transaction_id),

    CONSTRAINT ck_transactions_type
        CHECK (transaction_type IN ('BUY', 'SELL')),

    CONSTRAINT ck_transactions_quantity
        CHECK (quantity > 0),

    CONSTRAINT ck_transactions_price
        CHECK (price_per_share > 0),

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
-- 15. PERFORMANCE & REPORTING INDEXES
-- ============================================================
CREATE INDEX idx_transactions_portfolio_date ON transactions (portfolio_id, transaction_date);
CREATE INDEX idx_transactions_company ON transactions (company_id);
CREATE INDEX idx_dividends_company_dates ON dividends (company_id, payment_date);
CREATE INDEX idx_price_hist_comp_date ON company_price_history (company_id, recorded_at);
CREATE INDEX idx_alerts_user ON alerts (user_id, is_active);
CREATE INDEX idx_alerts_price_eval ON alerts (is_active, company_id, alert_type);
CREATE INDEX idx_alerts_port_eval ON alerts (is_active, portfolio_id, alert_type);
CREATE INDEX idx_notifications_user ON notifications (user_id, is_read, created_at);
CREATE INDEX idx_notifications_entity ON notifications (user_id, related_entity_type, related_entity_id);
CREATE INDEX idx_portfolio_goals_user ON portfolio_goals (user_id, is_active, created_at);
CREATE INDEX idx_portfolio_goals_portfolio ON portfolio_goals (portfolio_id, is_active);

-- ============================================================
-- 16. VIEW: VW_PORTFOLIO_HOLDINGS
-- Real-time holdings valuation & weighted average purchase price
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