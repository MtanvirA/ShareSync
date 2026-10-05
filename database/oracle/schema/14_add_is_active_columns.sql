-- ============================================================================
-- ShareSync - Migration: Add IS_ACTIVE Column to APP_USERS and COMPANIES
-- File: 14_add_is_active_columns.sql
-- ============================================================================

BEGIN
    BEGIN
        EXECUTE IMMEDIATE 'ALTER TABLE app_users ADD (is_active NUMBER(1) DEFAULT 1 NOT NULL)';
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE != -1430 THEN -- ORA-01430: column being added already exists in table
                RAISE;
            END IF;
    END;

    BEGIN
        EXECUTE IMMEDIATE 'ALTER TABLE companies ADD (is_active NUMBER(1) DEFAULT 1 NOT NULL)';
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE != -1430 THEN
                RAISE;
            END IF;
    END;
END;
/
