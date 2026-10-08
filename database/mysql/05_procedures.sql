-- ============================================================
-- ShareSync - MySQL Stored Procedures
-- File: 05_procedures.sql
-- Description: Business logic procedures with validation and atomic execution
-- ============================================================

USE sharesync;

DELIMITER //

-- ============================================================
-- 1. sp_record_transaction
-- DATABASE: Stored Procedure — sp_record_transaction
-- SQL CONCEPT: PROCEDURE
-- TRANSACTION: BUY/SELL Processing
-- TRANSACTION: SELL
-- FEATURE: Realized P/L
-- BUSINESS RULE: Oversell Prevention
-- 
-- Parameters:
--   IN  p_portfolio_id     INT            : Target portfolio ID
--   IN  p_company_id       INT            : Target company ID
--   IN  p_transaction_type VARCHAR(10)    : 'BUY' or 'SELL'
--   IN  p_quantity         DECIMAL(14,4)  : Whole share quantity to transact
--   IN  p_price_per_share  DECIMAL(14,2)  : Execution unit price
--   IN  p_fee              DECIMAL(14,2)  : Brokerage/exchange commission fee
--   IN  p_changed_by       VARCHAR(100)   : Auditing principal (defaults to CURRENT_USER())
--   OUT p_transaction_id   INT            : Generated primary key on success
--   OUT p_status           VARCHAR(20)    : 'SUCCESS', 'ERROR', or 'OVERSELL_REJECTED'
--   OUT p_message          VARCHAR(255)   : Human-readable execution or rejection reason
-- 
-- Business Logic:
-- 1. Validates whole share integer quantity, strictly positive price, non-negative fee.
-- 2. Verifies portfolio and company foreign key existence.
-- 3. For SELL: computes available holding (total_bought - total_sold).
--    Rejects with 'OVERSELL_REJECTED' if p_quantity > available holding.
-- 4. Atomically inserts transaction, returning last generated auto-increment ID.
-- ============================================================
DROP PROCEDURE IF EXISTS sp_record_transaction //
CREATE PROCEDURE sp_record_transaction (
    IN  p_portfolio_id     INT,
    IN  p_company_id       INT,
    IN  p_transaction_type VARCHAR(10),
    IN  p_quantity         DECIMAL(14,4),
    IN  p_price_per_share  DECIMAL(14,2),
    IN  p_fee              DECIMAL(14,2),
    IN  p_changed_by       VARCHAR(100),
    OUT p_transaction_id   INT,
    OUT p_status           VARCHAR(20),
    OUT p_message          VARCHAR(255)
)
proc_label: BEGIN
    DECLARE v_type          VARCHAR(10);
    DECLARE v_port_count    INT DEFAULT 0;
    DECLARE v_comp_count    INT DEFAULT 0;
    DECLARE v_total_bought  DECIMAL(14,4) DEFAULT 0;
    DECLARE v_total_sold    DECIMAL(14,4) DEFAULT 0;
    DECLARE v_available_qty DECIMAL(14,4) DEFAULT 0;
    DECLARE v_user          VARCHAR(100);

    -- Default user if omitted
    SET v_user = COALESCE(p_changed_by, CURRENT_USER());

    -- 1. Validate inputs
    SET v_type = UPPER(TRIM(p_transaction_type));
    IF v_type NOT IN ('BUY', 'SELL') THEN
        SET p_status = 'ERROR';
        SET p_message = 'Invalid transaction type. Must be BUY or SELL.';
        SET p_transaction_id = NULL;
        LEAVE proc_label;
    END IF;

    IF p_quantity <= 0 OR p_quantity != FLOOR(p_quantity) THEN
        SET p_status = 'ERROR';
        SET p_message = 'Quantity must be a positive whole integer. Fractional shares are not supported.';
        SET p_transaction_id = NULL;
        LEAVE proc_label;
    END IF;

    IF p_price_per_share <= 0 THEN
        SET p_status = 'ERROR';
        SET p_message = 'Price per share must be strictly positive.';
        SET p_transaction_id = NULL;
        LEAVE proc_label;
    END IF;

    IF p_fee IS NOT NULL AND p_fee < 0 THEN
        SET p_status = 'ERROR';
        SET p_message = 'Fee cannot be negative.';
        SET p_transaction_id = NULL;
        LEAVE proc_label;
    END IF;

    -- 2. Verify existence of portfolio and company
    SELECT COUNT(*) INTO v_port_count FROM portfolios WHERE portfolio_id = p_portfolio_id;
    IF v_port_count = 0 THEN
        SET p_status = 'ERROR';
        SET p_message = 'Portfolio not found.';
        SET p_transaction_id = NULL;
        LEAVE proc_label;
    END IF;

    SELECT COUNT(*) INTO v_comp_count FROM companies WHERE company_id = p_company_id;
    IF v_comp_count = 0 THEN
        SET p_status = 'ERROR';
        SET p_message = 'Company not found.';
        SET p_transaction_id = NULL;
        LEAVE proc_label;
    END IF;

    -- 3. For SELL orders, verify available holdings to prevent overselling
    IF v_type = 'SELL' THEN
        SELECT
            COALESCE(SUM(CASE WHEN transaction_type = 'BUY' THEN quantity ELSE 0 END), 0),
            COALESCE(SUM(CASE WHEN transaction_type = 'SELL' THEN quantity ELSE 0 END), 0)
        INTO
            v_total_bought,
            v_total_sold
        FROM transactions
        WHERE portfolio_id = p_portfolio_id AND company_id = p_company_id;

        SET v_available_qty = v_total_bought - v_total_sold;

        IF p_quantity > v_available_qty THEN
            SET p_status = 'OVERSELL_REJECTED';
            SET p_message = CONCAT('Cannot execute SELL order: Requested quantity (', CAST(p_quantity AS CHAR), ') exceeds available holdings (', CAST(v_available_qty AS CHAR), ').');
            SET p_transaction_id = NULL;
            LEAVE proc_label;
        END IF;
    END IF;

    -- 4. Execute atomic transaction insert
    INSERT INTO transactions (
        portfolio_id,
        company_id,
        transaction_type,
        quantity,
        price_per_share,
        fee,
        transaction_date
    ) VALUES (
        p_portfolio_id,
        p_company_id,
        v_type,
        p_quantity,
        p_price_per_share,
        COALESCE(p_fee, 0.00),
        CURRENT_TIMESTAMP
    );

    SET p_transaction_id = LAST_INSERT_ID();
    SET p_status = 'SUCCESS';
    SET p_message = 'Transaction recorded successfully.';
END //

-- ============================================================
-- 2. sp_generate_portfolio_snapshot
-- DATABASE: Stored Procedure — sp_generate_portfolio_snapshot
-- SQL CONCEPT: PROCEDURE
-- CALCULATION: Market Value
-- Purpose: Calculates mark-to-market valuation across all active holdings and upserts daily portfolio snapshot.
-- ============================================================
DROP PROCEDURE IF EXISTS sp_generate_portfolio_snapshot //
CREATE PROCEDURE sp_generate_portfolio_snapshot (
    IN  p_portfolio_id  INT,
    IN  p_snapshot_date DATE,
    OUT p_total_value   DECIMAL(20,2),
    OUT p_status        VARCHAR(20),
    OUT p_message       VARCHAR(255)
)
proc_label: BEGIN
    DECLARE v_port_exists INT DEFAULT 0;
    DECLARE v_calc_value  DECIMAL(20,2) DEFAULT 0;
    DECLARE v_target_date DATE;

    SET v_target_date = COALESCE(p_snapshot_date, CURDATE());

    SELECT COUNT(*) INTO v_port_exists FROM portfolios WHERE portfolio_id = p_portfolio_id;
    IF v_port_exists = 0 THEN
        SET p_status = 'ERROR';
        SET p_message = 'Portfolio not found.';
        SET p_total_value = 0;
        LEAVE proc_label;
    END IF;

    -- Compute valuation from cumulative net holdings * current company price
    SELECT COALESCE(SUM(holdings.current_qty * c.current_price), 0)
    INTO v_calc_value
    FROM (
        SELECT
            company_id,
            SUM(CASE WHEN transaction_type = 'BUY' THEN quantity ELSE -quantity END) AS current_qty
        FROM transactions
        WHERE portfolio_id = p_portfolio_id
          AND DATE(transaction_date) <= v_target_date
        GROUP BY company_id
        HAVING SUM(CASE WHEN transaction_type = 'BUY' THEN quantity ELSE -quantity END) > 0
    ) holdings
    JOIN companies c ON holdings.company_id = c.company_id;

    -- Upsert snapshot into portfolio_snapshots
    INSERT INTO portfolio_snapshots (portfolio_id, snapshot_date, total_value)
    VALUES (p_portfolio_id, v_target_date, v_calc_value)
    ON DUPLICATE KEY UPDATE total_value = v_calc_value;

    SET p_total_value = v_calc_value;
    SET p_status = 'SUCCESS';
    SET p_message = CONCAT('Snapshot generated successfully with value ', FORMAT(v_calc_value, 2));
END //

DELIMITER ;
