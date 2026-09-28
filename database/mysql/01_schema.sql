-- ============================================================
-- ShareSync - MySQL Database Schema
-- ============================================================

CREATE DATABASE IF NOT EXISTS sharesync;

USE sharesync;


-- ============================================================
-- 1. APP_USERS
-- ============================================================

CREATE TABLE app_users (
    user_id INT AUTO_INCREMENT,
    name VARCHAR(100) NOT NULL,
    email VARCHAR(150) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,

    CONSTRAINT pk_app_users
        PRIMARY KEY (user_id),

    CONSTRAINT uq_app_users_email
        UNIQUE (email)
);


-- ============================================================
-- 2. SECTORS
-- ============================================================

CREATE TABLE sectors (
    sector_id INT AUTO_INCREMENT,
    sector_name VARCHAR(100) NOT NULL,
    description VARCHAR(255),

    CONSTRAINT pk_sectors
        PRIMARY KEY (sector_id),

    CONSTRAINT uq_sectors_name
        UNIQUE (sector_name)
);


-- ============================================================
-- 3. COMPANIES
-- ============================================================

CREATE TABLE companies (
    company_id INT AUTO_INCREMENT,
    company_name VARCHAR(150) NOT NULL,
    ticker_symbol VARCHAR(20) NOT NULL,
    sector_id INT NOT NULL,
    current_price DECIMAL(14,2) NOT NULL,
    market_cap DECIMAL(20,2),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,

    CONSTRAINT pk_companies
        PRIMARY KEY (company_id),

    CONSTRAINT uq_companies_ticker
        UNIQUE (ticker_symbol),

    CONSTRAINT ck_companies_price
        CHECK (current_price > 0),

    CONSTRAINT ck_companies_market_cap
        CHECK (market_cap IS NULL OR market_cap >= 0),

    CONSTRAINT fk_companies_sector
        FOREIGN KEY (sector_id)
        REFERENCES sectors (sector_id)
);


-- ============================================================
-- 4. PORTFOLIOS
-- ============================================================

CREATE TABLE portfolios (
    portfolio_id INT AUTO_INCREMENT,
    user_id INT NOT NULL,
    portfolio_name VARCHAR(100) NOT NULL,
    description VARCHAR(255),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,

    CONSTRAINT pk_portfolios
        PRIMARY KEY (portfolio_id),

    CONSTRAINT uq_portfolios_user_name
        UNIQUE (user_id, portfolio_name),

    CONSTRAINT fk_portfolios_user
        FOREIGN KEY (user_id)
        REFERENCES app_users (user_id)
);


-- ============================================================
-- 5. WATCHLISTS
-- ============================================================

CREATE TABLE watchlists (
    watchlist_id INT AUTO_INCREMENT,
    user_id INT NOT NULL,
    watchlist_name VARCHAR(100) NOT NULL,
    description VARCHAR(255),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,

    CONSTRAINT pk_watchlists
        PRIMARY KEY (watchlist_id),

    CONSTRAINT uq_watchlists_user_name
        UNIQUE (user_id, watchlist_name),

    CONSTRAINT fk_watchlists_user
        FOREIGN KEY (user_id)
        REFERENCES app_users (user_id)
);


-- ============================================================
-- 6. WATCHLIST_ITEMS
-- ============================================================

CREATE TABLE watchlist_items (
    watchlist_id INT NOT NULL,
    company_id INT NOT NULL,
    target_price DECIMAL(14,2),
    added_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,

    CONSTRAINT pk_watchlist_items
        PRIMARY KEY (watchlist_id, company_id),

    CONSTRAINT ck_watchlist_items_target_price
        CHECK (target_price IS NULL OR target_price > 0),

    CONSTRAINT fk_watchlist_items_watchlist
        FOREIGN KEY (watchlist_id)
        REFERENCES watchlists (watchlist_id),

    CONSTRAINT fk_watchlist_items_company
        FOREIGN KEY (company_id)
        REFERENCES companies (company_id)
);


-- ============================================================
-- 7. TRANSACTIONS
-- ============================================================

CREATE TABLE transactions (
    transaction_id INT AUTO_INCREMENT,
    portfolio_id INT NOT NULL,
    company_id INT NOT NULL,
    transaction_type VARCHAR(10) NOT NULL,
    quantity DECIMAL(14,4) NOT NULL,
    price_per_share DECIMAL(14,2) NOT NULL,
    transaction_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,

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
        REFERENCES portfolios (portfolio_id),

    CONSTRAINT fk_transactions_company
        FOREIGN KEY (company_id)
        REFERENCES companies (company_id)
);


-- ============================================================
-- 8. DIVIDENDS
-- ============================================================

CREATE TABLE dividends (
    dividend_id INT AUTO_INCREMENT,
    company_id INT NOT NULL,
    dividend_per_share DECIMAL(14,2) NOT NULL,
    declaration_date DATE NOT NULL,
    payment_date DATE NOT NULL,

    CONSTRAINT pk_dividends
        PRIMARY KEY (dividend_id),

    CONSTRAINT ck_dividends_amount
        CHECK (dividend_per_share > 0),

    CONSTRAINT ck_dividends_dates
        CHECK (payment_date >= declaration_date),

    CONSTRAINT fk_dividends_company
        FOREIGN KEY (company_id)
        REFERENCES companies (company_id)
);


-- ============================================================
-- 9. TRANSACTION_AUDIT
-- ============================================================

CREATE TABLE transaction_audit (
    audit_id INT AUTO_INCREMENT,
    transaction_id INT NOT NULL,
    action_type VARCHAR(10) NOT NULL,
    action_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
    changed_by VARCHAR(100),
    details VARCHAR(1000),

    CONSTRAINT pk_transaction_audit
        PRIMARY KEY (audit_id),

    CONSTRAINT ck_transaction_audit_action
        CHECK (action_type IN ('INSERT', 'UPDATE', 'DELETE')),

    CONSTRAINT fk_transaction_audit_transaction
        FOREIGN KEY (transaction_id)
        REFERENCES transactions (transaction_id)
);


-- ============================================================
-- 10. PORTFOLIO_SNAPSHOTS
-- ============================================================

CREATE TABLE portfolio_snapshots (
    snapshot_id INT AUTO_INCREMENT,
    portfolio_id INT NOT NULL,
    snapshot_date DATE NOT NULL,
    total_value DECIMAL(20,2) NOT NULL,

    CONSTRAINT pk_portfolio_snapshots
        PRIMARY KEY (snapshot_id),

    CONSTRAINT uq_portfolio_snapshot_date
        UNIQUE (portfolio_id, snapshot_date),

    CONSTRAINT ck_portfolio_snapshot_value
        CHECK (total_value >= 0),

    CONSTRAINT fk_portfolio_snapshots_portfolio
        FOREIGN KEY (portfolio_id)
        REFERENCES portfolios (portfolio_id)
);


-- ============================================================
-- Verification
-- ============================================================

SHOW TABLES;