-- ============================================================
-- ShareSync - MySQL Triggers
-- ============================================================

USE sharesync;


-- ============================================================
-- Trigger: Audit New Transactions
-- ============================================================

DELIMITER $$

CREATE TRIGGER trg_transaction_after_insert
AFTER INSERT ON transactions
FOR EACH ROW
BEGIN

    INSERT INTO transaction_audit
    (
        transaction_id,
        action_type,
        changed_by,
        details
    )
    VALUES
    (
        NEW.transaction_id,
        'INSERT',
        CURRENT_USER(),
        CONCAT(
            'New ',
            NEW.transaction_type,
            ' transaction: ',
            NEW.quantity,
            ' shares at price ',
            NEW.price_per_share
        )
    );

END$$

DELIMITER ;