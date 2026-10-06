-- ============================================================
-- ShareSync - MySQL Database Triggers: Transaction Audit Trail
-- File: 06_triggers.sql
-- Description: Audits all INSERT, UPDATE, and DELETE operations on transactions
-- ============================================================

USE sharesync;

DELIMITER //

-- ============================================================
-- 1. trg_transactions_after_insert
-- ============================================================
DROP TRIGGER IF EXISTS trg_transactions_after_insert //
CREATE TRIGGER trg_transactions_after_insert
AFTER INSERT ON transactions
FOR EACH ROW
BEGIN
    INSERT INTO transaction_audit (
        transaction_id,
        action_type,
        action_date,
        changed_by,
        details
    ) VALUES (
        NEW.transaction_id,
        'INSERT',
        CURRENT_TIMESTAMP,
        COALESCE(CURRENT_USER(), 'SYSTEM'),
        CONCAT('Trigger: ', NEW.transaction_type, ' ', CAST(NEW.quantity AS CHAR), ' shares at price ', CAST(NEW.price_per_share AS CHAR), ' for Portfolio #', CAST(NEW.portfolio_id AS CHAR), ', Company #', CAST(NEW.company_id AS CHAR))
    );
END //

-- ============================================================
-- 2. trg_transactions_after_update
-- ============================================================
DROP TRIGGER IF EXISTS trg_transactions_after_update //
CREATE TRIGGER trg_transactions_after_update
AFTER UPDATE ON transactions
FOR EACH ROW
BEGIN
    INSERT INTO transaction_audit (
        transaction_id,
        action_type,
        action_date,
        changed_by,
        details
    ) VALUES (
        NEW.transaction_id,
        'UPDATE',
        CURRENT_TIMESTAMP,
        COALESCE(CURRENT_USER(), 'SYSTEM'),
        CONCAT('Trigger: Updated tx #', CAST(OLD.transaction_id AS CHAR), '. Qty ', CAST(OLD.quantity AS CHAR), '->', CAST(NEW.quantity AS CHAR), ', Price ', CAST(OLD.price_per_share AS CHAR), '->', CAST(NEW.price_per_share AS CHAR))
    );
END //

-- ============================================================
-- 3. trg_transactions_after_delete
-- Preserves audit history with transaction_id = NULL and full detail
-- ============================================================
DROP TRIGGER IF EXISTS trg_transactions_after_delete //
CREATE TRIGGER trg_transactions_after_delete
AFTER DELETE ON transactions
FOR EACH ROW
BEGIN
    INSERT INTO transaction_audit (
        transaction_id,
        action_type,
        action_date,
        changed_by,
        details
    ) VALUES (
        NULL,
        'DELETE',
        CURRENT_TIMESTAMP,
        COALESCE(CURRENT_USER(), 'SYSTEM'),
        CONCAT('Trigger: Deleted tx #', CAST(OLD.transaction_id AS CHAR), ': ', OLD.transaction_type, ' ', CAST(OLD.quantity AS CHAR), ' shares at price ', CAST(OLD.price_per_share AS CHAR), ' (Portfolio ID ', CAST(OLD.portfolio_id AS CHAR), ', Company ID ', CAST(OLD.company_id AS CHAR), ')')
    );
END //

DELIMITER ;
