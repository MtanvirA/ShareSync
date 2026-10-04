-- ============================================================
-- ShareSync - Stored Procedure: Generate Portfolio Valuation Snapshot
-- File: 02_sp_generate_portfolio_snapshot.sql
-- ============================================================

CREATE OR REPLACE PROCEDURE sp_generate_portfolio_snapshot (
    p_portfolio_id      IN  NUMBER,
    p_snapshot_date     IN  DATE DEFAULT TRUNC(SYSDATE),
    p_total_value       OUT NUMBER,
    p_status            OUT VARCHAR2,
    p_message           OUT VARCHAR2
)
AS
    v_portfolio_exists  NUMBER;
    v_calc_value        NUMBER := 0;
BEGIN
    SELECT COUNT(*) INTO v_portfolio_exists FROM portfolios WHERE portfolio_id = p_portfolio_id;
    IF v_portfolio_exists = 0 THEN
        p_status := 'ERROR';
        p_message := 'Portfolio not found.';
        p_total_value := 0;
        RETURN;
    END IF;

    -- Compute valuation from holdings * current company price
    SELECT NVL(SUM(holdings.current_qty * c.current_price), 0)
    INTO v_calc_value
    FROM (
        SELECT
            company_id,
            SUM(CASE WHEN transaction_type = 'BUY' THEN quantity ELSE -quantity END) AS current_qty
        FROM transactions
        WHERE portfolio_id = p_portfolio_id
          AND TRUNC(transaction_date) <= TRUNC(p_snapshot_date)
        GROUP BY company_id
        HAVING SUM(CASE WHEN transaction_type = 'BUY' THEN quantity ELSE -quantity END) > 0
    ) holdings
    JOIN companies c ON holdings.company_id = c.company_id;

    -- Upsert into portfolio_snapshots
    MERGE INTO portfolio_snapshots ps
    USING (
        SELECT p_portfolio_id AS portfolio_id, TRUNC(p_snapshot_date) AS snapshot_date FROM dual
    ) src
    ON (ps.portfolio_id = src.portfolio_id AND ps.snapshot_date = src.snapshot_date)
    WHEN MATCHED THEN
        UPDATE SET ps.total_value = v_calc_value
    WHEN NOT MATCHED THEN
        INSERT (portfolio_id, snapshot_date, total_value)
        VALUES (src.portfolio_id, src.snapshot_date, v_calc_value);

    COMMIT;

    p_total_value := v_calc_value;
    p_status := 'SUCCESS';
    p_message := 'Snapshot generated successfully with value ' || TO_CHAR(v_calc_value, 'FM999,999,990.00');

EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_status := 'FAILED';
        p_message := 'Snapshot failed: ' || SQLERRM;
        p_total_value := 0;
END sp_generate_portfolio_snapshot;
/
