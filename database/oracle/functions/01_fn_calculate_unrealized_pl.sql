-- ============================================================
-- ShareSync - User-Defined Function: Calculate Unrealized Profit/Loss
-- File: 01_fn_calculate_unrealized_pl.sql
-- ============================================================

CREATE OR REPLACE FUNCTION fn_calculate_unrealized_pl (
    p_portfolio_id IN NUMBER,
    p_company_id   IN NUMBER
) RETURN NUMBER
DETERMINISTIC
AS
    v_current_qty       NUMBER := 0;
    v_total_buy_qty     NUMBER := 0;
    v_total_buy_cost    NUMBER := 0;
    v_weighted_avg_cost NUMBER := 0;
    v_current_price     NUMBER := 0;
    v_current_market_val NUMBER := 0;
    v_total_cost_basis  NUMBER := 0;
BEGIN
    -- 1. Get current stock price
    SELECT current_price INTO v_current_price
    FROM companies
    WHERE company_id = p_company_id;

    -- 2. Aggregate quantities and buy costs
    SELECT
        NVL(SUM(CASE WHEN transaction_type = 'BUY' THEN quantity ELSE -quantity END), 0),
        NVL(SUM(CASE WHEN transaction_type = 'BUY' THEN quantity ELSE 0 END), 0),
        NVL(SUM(CASE WHEN transaction_type = 'BUY' THEN quantity * price_per_share ELSE 0 END), 0)
    INTO
        v_current_qty,
        v_total_buy_qty,
        v_total_buy_cost
    FROM transactions
    WHERE portfolio_id = p_portfolio_id AND company_id = p_company_id;

    -- If no shares held, unrealized P/L is zero
    IF v_current_qty <= 0 OR v_total_buy_qty <= 0 THEN
        RETURN 0;
    END IF;

    -- 3. Calculate weighted average buy cost
    v_weighted_avg_cost := v_total_buy_cost / v_total_buy_qty;

    -- 4. Calculate market value and cost basis
    v_current_market_val := v_current_qty * v_current_price;
    v_total_cost_basis := v_current_qty * v_weighted_avg_cost;

    RETURN ROUND(v_current_market_val - v_total_cost_basis, 2);

EXCEPTION
    WHEN NO_DATA_FOUND THEN
        RETURN 0;
    WHEN OTHERS THEN
        RETURN NULL;
END fn_calculate_unrealized_pl;
/
