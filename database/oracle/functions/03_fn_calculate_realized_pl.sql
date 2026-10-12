-- ============================================================
-- ShareSync - User-Defined Function: Calculate Realized Profit/Loss
-- File: 03_fn_calculate_realized_pl.sql
-- Description: LF-26 Calculates realized profit/loss using weighted-average cost rule
--              Realized P/L = (Net Sale Proceeds) - (Cost Basis of Shares Sold)
--              Where Net Sale Proceeds = (quantity * price_per_share) - fee
--              And Cost Basis = quantity * fn_get_weighted_avg_price(portfolio_id, company_id)
-- ============================================================

CREATE OR REPLACE FUNCTION fn_calculate_realized_pl (
    p_portfolio_id IN NUMBER,
    p_company_id   IN NUMBER DEFAULT NULL
) RETURN NUMBER
AS
    v_total_realized_pl NUMBER := 0;
BEGIN
    SELECT NVL(SUM(
        (t.quantity * t.price_per_share - NVL(t.fee, 0)) - 
        (t.quantity * fn_get_weighted_avg_price(t.portfolio_id, t.company_id))
    ), 0)
    INTO v_total_realized_pl
    FROM transactions t
    WHERE t.portfolio_id = p_portfolio_id
      AND t.transaction_type = 'SELL'
      AND (p_company_id IS NULL OR t.company_id = p_company_id);

    RETURN ROUND(v_total_realized_pl, 2);

EXCEPTION
    WHEN OTHERS THEN
        RETURN 0;
END fn_calculate_realized_pl;
/
