-- ============================================================
-- ShareSync - MySQL Stored Functions
-- File: 04_functions.sql
-- Description: Financial analytical user-defined functions
-- ============================================================

USE sharesync;

-- ============================================================
-- 1. fn_get_weighted_avg_price
-- Calculates the volume-weighted average buy price for holdings
-- ============================================================
DELIMITER //

DROP FUNCTION IF EXISTS fn_get_weighted_avg_price //
CREATE FUNCTION fn_get_weighted_avg_price (
    p_portfolio_id INT,
    p_company_id   INT
) RETURNS DECIMAL(14,2)
DETERMINISTIC
READS SQL DATA
BEGIN
    DECLARE v_total_buy_qty  DECIMAL(14,4) DEFAULT 0;
    DECLARE v_total_buy_cost DECIMAL(20,4) DEFAULT 0;

    SELECT
        COALESCE(SUM(quantity), 0),
        COALESCE(SUM(quantity * price_per_share), 0)
    INTO
        v_total_buy_qty,
        v_total_buy_cost
    FROM transactions
    WHERE portfolio_id = p_portfolio_id
      AND company_id = p_company_id
      AND transaction_type = 'BUY';

    IF v_total_buy_qty = 0 THEN
        RETURN 0.00;
    END IF;

    RETURN ROUND(v_total_buy_cost / v_total_buy_qty, 2);
END //

-- ============================================================
-- 2. fn_calculate_unrealized_pl
-- Calculates mark-to-market unrealized profit/loss for a holding
-- ============================================================
DROP FUNCTION IF EXISTS fn_calculate_unrealized_pl //
CREATE FUNCTION fn_calculate_unrealized_pl (
    p_portfolio_id INT,
    p_company_id   INT
) RETURNS DECIMAL(20,2)
DETERMINISTIC
READS SQL DATA
BEGIN
    DECLARE v_current_qty       DECIMAL(14,4) DEFAULT 0;
    DECLARE v_total_buy_qty     DECIMAL(14,4) DEFAULT 0;
    DECLARE v_total_buy_cost    DECIMAL(20,4) DEFAULT 0;
    DECLARE v_weighted_avg_cost DECIMAL(14,4) DEFAULT 0;
    DECLARE v_current_price     DECIMAL(14,2) DEFAULT 0;
    DECLARE v_current_market_val DECIMAL(20,2) DEFAULT 0;
    DECLARE v_total_cost_basis  DECIMAL(20,2) DEFAULT 0;

    -- 1. Get current stock price
    SELECT current_price INTO v_current_price
    FROM companies
    WHERE company_id = p_company_id
    LIMIT 1;

    -- 2. Aggregate quantities and buy costs
    SELECT
        COALESCE(SUM(CASE WHEN transaction_type = 'BUY' THEN quantity ELSE -quantity END), 0),
        COALESCE(SUM(CASE WHEN transaction_type = 'BUY' THEN quantity ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN transaction_type = 'BUY' THEN quantity * price_per_share ELSE 0 END), 0)
    INTO
        v_current_qty,
        v_total_buy_qty,
        v_total_buy_cost
    FROM transactions
    WHERE portfolio_id = p_portfolio_id AND company_id = p_company_id;

    -- If no net shares held or no buy history, unrealized P/L is 0
    IF v_current_qty <= 0 OR v_total_buy_qty <= 0 THEN
        RETURN 0.00;
    END IF;

    -- 3. Calculate weighted average buy cost
    SET v_weighted_avg_cost = v_total_buy_cost / v_total_buy_qty;

    -- 4. Calculate market value and cost basis
    SET v_current_market_val = v_current_qty * v_current_price;
    SET v_total_cost_basis   = v_current_qty * v_weighted_avg_cost;

    RETURN ROUND(v_current_market_val - v_total_cost_basis, 2);
END //

DELIMITER ;
