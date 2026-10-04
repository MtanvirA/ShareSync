-- ============================================================
-- ShareSync - Database Trigger: Automatic Transaction Audit Trail
-- File: 01_trg_transactions_audit.sql
-- ============================================================

CREATE OR REPLACE TRIGGER trg_transactions_audit
AFTER INSERT OR UPDATE ON transactions
FOR EACH ROW
DECLARE
    v_action     VARCHAR2(10);
    v_user       VARCHAR2(100);
    v_details    VARCHAR2(1000);
BEGIN
    v_user := NVL(SYS_CONTEXT('USERENV', 'SESSION_USER'), USER);

    IF INSERTING THEN
        v_action  := 'INSERT';
        v_details := 'Trigger: ' || :NEW.transaction_type || ' ' || :NEW.quantity || ' shares at price ' || :NEW.price_per_share;
    ELSIF UPDATING THEN
        v_action  := 'UPDATE';
        v_details := 'Trigger: Updated tx ' || :OLD.transaction_id || '. Qty ' || :OLD.quantity || '->' || :NEW.quantity || ', Price ' || :OLD.price_per_share || '->' || :NEW.price_per_share;
    END IF;

    INSERT INTO transaction_audit (
        transaction_id,
        action_type,
        action_date,
        changed_by,
        details
    ) VALUES (
        :NEW.transaction_id,
        v_action,
        CURRENT_TIMESTAMP,
        v_user,
        v_details
    );
END;
/
