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

-- ============================================================
-- 3. fn_calculate_realized_pl
-- LF-26: Calculates realized profit/loss using weighted-average cost rule
-- Realized P/L = (Net Sale Proceeds) - (Cost Basis of Shares Sold)
-- ============================================================
DROP FUNCTION IF EXISTS fn_calculate_realized_pl //
CREATE FUNCTION fn_calculate_realized_pl (
    p_portfolio_id INT,
    p_company_id   INT
) RETURNS DECIMAL(20,2)
DETERMINISTIC
READS SQL DATA
BEGIN
    DECLARE v_total_realized_pl DECIMAL(20,2) DEFAULT 0.00;

    SELECT COALESCE(SUM(
        (t.quantity * t.price_per_share - COALESCE(t.fee, 0.00)) - 
        (t.quantity * fn_get_weighted_avg_price(t.portfolio_id, t.company_id))
    ), 0.00)
    INTO v_total_realized_pl
    FROM transactions t
    WHERE t.portfolio_id = p_portfolio_id
      AND t.transaction_type = 'SELL'
      AND (p_company_id IS NULL OR t.company_id = p_company_id);

    RETURN ROUND(v_total_realized_pl, 2);
END //

-- ============================================================
-- 4. fn_calculate_net_cash_flow
-- LF-27: Calculates net cash flow for a portfolio:
-- Net Cash Flow = Total Net Sale Inflow - Total Net Purchase Outflow
-- ============================================================
DROP FUNCTION IF EXISTS fn_calculate_net_cash_flow //
CREATE FUNCTION fn_calculate_net_cash_flow (
    p_portfolio_id INT
) RETURNS DECIMAL(20,2)
DETERMINISTIC
READS SQL DATA
BEGIN
    DECLARE v_total_sell_inflow  DECIMAL(20,2) DEFAULT 0.00;
    DECLARE v_total_buy_outflow  DECIMAL(20,2) DEFAULT 0.00;

    SELECT
        COALESCE(SUM(CASE WHEN transaction_type = 'SELL' THEN (quantity * price_per_share - COALESCE(fee, 0.00)) ELSE 0.00 END), 0.00),
        COALESCE(SUM(CASE WHEN transaction_type = 'BUY'  THEN (quantity * price_per_share + COALESCE(fee, 0.00)) ELSE 0.00 END), 0.00)
    INTO
        v_total_sell_inflow,
        v_total_buy_outflow
    FROM transactions
    WHERE portfolio_id = p_portfolio_id;

    RETURN ROUND(v_total_sell_inflow - v_total_buy_outflow, 2);
END //

-- ============================================================
-- 5. fn_calculate_real_return
-- LF-28: Calculates inflation-adjusted (real) return percentage
-- Real Return = (((1 + Nominal Return / 100) / (1 + Inflation Rate / 100)) - 1) * 100
-- ============================================================
DROP FUNCTION IF EXISTS fn_calculate_real_return //
CREATE FUNCTION fn_calculate_real_return (
    p_nominal_return DECIMAL(14,4),
    p_inflation_rate DECIMAL(6,2)
) RETURNS DECIMAL(14,4)
DETERMINISTIC
CONTAINS SQL
BEGIN
    IF p_inflation_rate IS NULL OR p_nominal_return IS NULL THEN
        RETURN NULL;
    END IF;

    IF (1.0000 + (p_inflation_rate / 100.0000)) = 0 THEN
        RETURN 0.0000;
    END IF;

    RETURN ROUND((((1.0000 + (p_nominal_return / 100.0000)) / (1.0000 + (p_inflation_rate / 100.0000))) - 1.0000) * 100.0000, 4);
END //

DELIMITER ;
