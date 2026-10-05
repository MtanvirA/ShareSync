-- ============================================================
-- ShareSync - Database Trigger: Automatic Transaction Audit Trail
-- File: 01_trg_transactions_audit.sql
-- Audits INSERT, UPDATE, and DELETE operations on TRANSACTIONS
-- ============================================================

CREATE OR REPLACE TRIGGER trg_transactions_audit
AFTER INSERT OR UPDATE OR DELETE ON transactions
FOR EACH ROW
DECLARE
    v_action     VARCHAR2(10);
    v_user       VARCHAR2(100);
    v_details    VARCHAR2(1000);
    v_tx_id      NUMBER;
BEGIN
    v_user := NVL(SYS_CONTEXT('USERENV', 'SESSION_USER'), USER);

    IF INSERTING THEN
        v_action  := 'INSERT';
        v_tx_id   := :NEW.transaction_id;
        v_details := 'Trigger: ' || :NEW.transaction_type || ' ' || :NEW.quantity || ' shares at price ' || :NEW.price_per_share;
    ELSIF UPDATING THEN
        v_action  := 'UPDATE';
        v_tx_id   := :NEW.transaction_id;
        v_details := 'Trigger: Updated tx #' || :OLD.transaction_id || '. Qty ' || :OLD.quantity || '->' || :NEW.quantity || ', Price ' || :OLD.price_per_share || '->' || :NEW.price_per_share;
    ELSIF DELETING THEN
        v_action  := 'DELETE';
        v_tx_id   := NULL;
        v_details := 'Trigger: Deleted tx #' || :OLD.transaction_id || ': ' || :OLD.transaction_type || ' ' || :OLD.quantity || ' shares at price ' || :OLD.price_per_share || ' (Portfolio ID ' || :OLD.portfolio_id || ', Company ID ' || :OLD.company_id || ')';
    END IF;

    INSERT INTO transaction_audit (
        transaction_id,
        action_type,
        action_date,
        changed_by,
        details
    ) VALUES (
        v_tx_id,
        v_action,
        CURRENT_TIMESTAMP,
        v_user,
        v_details
    );
END;
/
