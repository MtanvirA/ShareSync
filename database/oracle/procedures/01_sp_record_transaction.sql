-- ============================================================
-- ShareSync - Stored Procedure: Record Transaction with Validation & Audit
-- File: 01_sp_record_transaction.sql
-- ============================================================

CREATE OR REPLACE PROCEDURE sp_record_transaction (
    p_portfolio_id      IN  NUMBER,
    p_company_id        IN  NUMBER,
    p_transaction_type  IN  VARCHAR2,
    p_quantity          IN  NUMBER,
    p_price_per_share   IN  NUMBER,
    p_changed_by        IN  VARCHAR2 DEFAULT USER,
    p_transaction_id    OUT NUMBER,
    p_status            OUT VARCHAR2,
    p_message           OUT VARCHAR2
)
AS
    v_type               VARCHAR2(10);
    v_portfolio_count    NUMBER;
    v_company_count      NUMBER;
    v_total_bought       NUMBER := 0;
    v_total_sold         NUMBER := 0;
    v_available_qty      NUMBER := 0;
    v_new_tx_id          NUMBER;
    v_audit_details      VARCHAR2(1000);
BEGIN
    -- 1. Validate inputs
    v_type := UPPER(TRIM(p_transaction_type));
    IF v_type NOT IN ('BUY', 'SELL') THEN
        p_status := 'ERROR';
        p_message := 'Invalid transaction type. Must be BUY or SELL.';
        p_transaction_id := NULL;
        RETURN;
    END IF;

    IF p_quantity <= 0 THEN
        p_status := 'ERROR';
        p_message := 'Quantity must be strictly positive.';
        p_transaction_id := NULL;
        RETURN;
    END IF;

    IF p_price_per_share <= 0 THEN
        p_status := 'ERROR';
        p_message := 'Price per share must be strictly positive.';
        p_transaction_id := NULL;
        RETURN;
    END IF;

    -- 2. Verify existence of portfolio and company
    SELECT COUNT(*) INTO v_portfolio_count FROM portfolios WHERE portfolio_id = p_portfolio_id;
    IF v_portfolio_count = 0 THEN
        p_status := 'ERROR';
        p_message := 'Portfolio not found.';
        p_transaction_id := NULL;
        RETURN;
    END IF;

    SELECT COUNT(*) INTO v_company_count FROM companies WHERE company_id = p_company_id;
    IF v_company_count = 0 THEN
        p_status := 'ERROR';
        p_message := 'Company not found.';
        p_transaction_id := NULL;
        RETURN;
    END IF;

    -- 3. For SELL orders, calculate available holdings to prevent oversell
    IF v_type = 'SELL' THEN
        SELECT
            NVL(SUM(CASE WHEN transaction_type = 'BUY' THEN quantity ELSE 0 END), 0),
            NVL(SUM(CASE WHEN transaction_type = 'SELL' THEN quantity ELSE 0 END), 0)
        INTO
            v_total_bought,
            v_total_sold
        FROM transactions
        WHERE portfolio_id = p_portfolio_id AND company_id = p_company_id;

        v_available_qty := v_total_bought - v_total_sold;

        IF p_quantity > v_available_qty THEN
            p_status := 'OVERSELL_REJECTED';
            p_message := 'Cannot execute SELL order: Requested quantity (' || p_quantity || ') exceeds available holdings (' || v_available_qty || ').';
            p_transaction_id := NULL;
            RETURN;
        END IF;
    END IF;

    -- 4. Execute atomic transaction insert
    SAVEPOINT sp_before_insert;

    INSERT INTO transactions (
        portfolio_id,
        company_id,
        transaction_type,
        quantity,
        price_per_share,
        transaction_date
    ) VALUES (
        p_portfolio_id,
        p_company_id,
        v_type,
        p_quantity,
        p_price_per_share,
        CURRENT_TIMESTAMP
    )
    RETURNING transaction_id INTO v_new_tx_id;

    -- 5. Insert audit log
    v_audit_details := 'Executed ' || v_type || ' of ' || p_quantity || ' shares at price ' || p_price_per_share;

    INSERT INTO transaction_audit (
        transaction_id,
        action_type,
        action_date,
        changed_by,
        details
    ) VALUES (
        v_new_tx_id,
        'INSERT',
        CURRENT_TIMESTAMP,
        NVL(p_changed_by, USER),
        v_audit_details
    );

    COMMIT;

    p_transaction_id := v_new_tx_id;
    p_status := 'SUCCESS';
    p_message := 'Transaction recorded and audited successfully.';

EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO sp_before_insert;
        p_status := 'FAILED';
        p_message := 'SQL Exception: ' || SQLERRM;
        p_transaction_id := NULL;
END sp_record_transaction;
/
