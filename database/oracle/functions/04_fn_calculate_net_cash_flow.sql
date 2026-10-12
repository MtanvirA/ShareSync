-- ============================================================
-- ShareSync - User-Defined Function: Calculate Net Cash Flow
-- File: 04_fn_calculate_net_cash_flow.sql
-- Description: LF-27 Calculates net portfolio cash flow:
--              Net Cash Flow = Total Net Sale Inflow - Total Net Purchase Outflow
--              Where Inflow = (quantity * price_per_share) - fee
--              And Outflow = (quantity * price_per_share) + fee
-- ============================================================

CREATE OR REPLACE FUNCTION fn_calculate_net_cash_flow (
    p_portfolio_id IN NUMBER
) RETURN NUMBER
AS
    v_total_sell_inflow NUMBER := 0;
    v_total_buy_outflow NUMBER := 0;
BEGIN
    SELECT 
        NVL(SUM(CASE WHEN transaction_type = 'SELL' THEN (quantity * price_per_share - NVL(fee, 0)) ELSE 0 END), 0),
        NVL(SUM(CASE WHEN transaction_type = 'BUY'  THEN (quantity * price_per_share + NVL(fee, 0)) ELSE 0 END), 0)
    INTO
        v_total_sell_inflow,
        v_total_buy_outflow
    FROM transactions
    WHERE portfolio_id = p_portfolio_id;

    RETURN ROUND(v_total_sell_inflow - v_total_buy_outflow, 2);

EXCEPTION
    WHEN OTHERS THEN
        RETURN 0;
END fn_calculate_net_cash_flow;
/
