-- ============================================================
-- ShareSync - User-Defined Function: Get Weighted Average Purchase Price
-- File: 02_fn_get_weighted_avg_price.sql
-- ============================================================

CREATE OR REPLACE FUNCTION fn_get_weighted_avg_price (
    p_portfolio_id IN NUMBER,
    p_company_id   IN NUMBER
) RETURN NUMBER
DETERMINISTIC
AS
    v_total_buy_qty     NUMBER := 0;
    v_total_buy_cost    NUMBER := 0;
BEGIN
    SELECT
        NVL(SUM(quantity), 0),
        NVL(SUM(quantity * price_per_share), 0)
    INTO
        v_total_buy_qty,
        v_total_buy_cost
    FROM transactions
    WHERE portfolio_id = p_portfolio_id
      AND company_id = p_company_id
      AND transaction_type = 'BUY';

    IF v_total_buy_qty = 0 THEN
        RETURN 0;
    END IF;

    RETURN ROUND(v_total_buy_cost / v_total_buy_qty, 2);

EXCEPTION
    WHEN OTHERS THEN
        RETURN 0;
END fn_get_weighted_avg_price;
/
